using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinMinionHandoffRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool scent in new[]{false,true})
        foreach(bool lateTarget in new[]{false,true})
        foreach(int gate in new[]{0,1,2,3})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xaf);
            var parent=new KingMoblinBoss();parent.Initialize(_entities.Entities<KingMoblinBoss>().Single().World,new(80,32));_entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel,1);_inventory.GiveTreasure(TreasureId.ScentSeeds,0x20);
            _inventory.SelectSatchelSeeds(1);_inventory.EquipA(TreasureId.SeedSatchel);_inventory.EquipB(0);
            _player.WarpTo(new(80,104));_player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position),"King minion handoff must begin on the keep's actual clear floor.");
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,1,80,104)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[WramAddress.wWarpsDisabled]=1;
            void PrepareStep(bool press=false)=>StepGameplayUpdates(1,Vector2.Zero,press?["attack"]:[],press?["attack"]:[],batched,()=>
            {native.UpdateGameplay(press?1:0,press?1:0,255,_entities.FrameCounter);});
            if(scent)
            {
                PrepareStep(true);
                for(int wait=0;(_entities.ActiveScentSeedTarget() is null||_player.IsUsingSeedSatchel)&&wait<50;wait++)PrepareStep();
                FailIf(_entities.ActiveScentSeedTarget() is not { } target||native[0xccd9]==0||target!=new Vector2(native[0xffb3],native[0xffb2]),
                    "Actual Satchel/ITEM updates must publish matching Scent targets before the minion exists.");
            }
            else PrepareStep();
            _player.WarpTo(new(16,120));native.Word(0xd00a,120*256);native.Word(0xd00c,16*256);
            _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1; // Declared stationary Link; other object categories remain live.
            var minion=new KingMoblinMinion();minion.Initialize(parent,0);minion.Position=new(24,8);minion.Visible=true;
            F1Set(minion,"<State>k__BackingField",4);F1Set(minion,"_counter",1);F1Set(minion,"_direction",3);minion.Animation.SetAnimation(2);
            var minionAdapter=new KingMoblinMinionRoomEntity(minion);_entities.RegisterEnemySlot(minionAdapter,1);_entities.AddEntity(minionAdapter);
            var bomb=new KingMoblinBomb();bomb.Initialize(parent,minion);var bombAdapter=new KingMoblinBombRoomEntity(bomb);F1Parts.Add(bombAdapter,0);_entities.AddEntity(bombAdapter);
            const int enemy=0xd180,at=0xd0c0;
            native[0xd0a9]=6;native[enemy]=1;native[enemy+1]=0x56;native[enemy+4]=4;native[enemy+6]=1;native[enemy+8]=3;
            native[enemy+11]=8;native[enemy+13]=24;native[enemy+0x29]=1;native[enemy+0x1a]=0xc2;
            native.Word(enemy+0x16,0xd080);native.Word(enemy+0x18,at);native.SetEnemyAnimation(1,2);
            native[0xcc08]=0x90;native[0xcc0a]=0x78;native[at]=1;native[at+1]=0x47;native[at+11]=8;native[at+13]=24;native.Word(at+0x16,enemy);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,beforeState=0,beforeZ=0,flight=0;
            var startRandom=_random.CaptureState();int nativeRandom=native.RandomCalls,nativeSounds=native.Sounds.Count;var sounds=_sound.AttachPlayRequestAudit();
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                // Declared earlier-ENEMY writer after the shared target sample;
                // it tests target lifetime, not a naturally placed keep actor.
                if(lateTarget&&minion.State==5&&SomariaPrivate<int>(minion,"_speedZ")==-0x20)
                    _player.SetScriptedCoordinateHigh(horizontal:true,136);
            });
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var enemyProbe=new PartMovementScratchProbe(()=>{beforeState=bomb.State;beforeZ=bomb.ZFixed;});
            _entities.RegisterEnemySlot(enemyProbe,15);_entities.AddEntity(enemyProbe);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int expectedState=0,expectedZ=0;
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>
                {expectedState=native[at+4];expectedZ=unchecked((short)native.Word(at+14));},beforeEnemies:()=>
                {
                    if(lateTarget&&native[enemy+4]==5&&native.Word(enemy+0x14)==0xffe0)native[0xd00d]=136;
                });updates++;
                if(bomb.State==2)flight++;
                var motion=SomariaPrivate<CarriedObjectMotion>(bomb,"_motion");
                FailIf(minion.State!=native[enemy+4]||(minion.ZFixed&65535)!=native.Word(enemy+14)||
                    SomariaPrivate<int>(minion,"_counter")!=native[enemy+6]||
                    SomariaPrivate<int>(minion.Animation,"_frameCounter")!=native[enemy+0x20]||
                    beforeState!=expectedState||beforeZ!=expectedZ||bomb.State!=native[at+4]||
                    bomb.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (bomb.ZFixed&65535)!=native.Word(at+14)||(motion.SpeedZ&65535)!=native.Word(at+0x14)||
                    SomariaPrivate<int>(bomb,"_angle")!=native[at+9]||observedCapture.Samples!=1||
                    Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"King minion/Scent{scent}/late{lateTarget}/gate{gate}/update{updates}: minion{minion.State}/{native[enemy+4]}, Z{minion.ZFixed}/{unchecked((short)native.Word(enemy+14))}, bomb{bomb.State}/{native[at+4]}, Z{bomb.ZFixed}/{unchecked((short)native.Word(at+14))}, angle{SomariaPrivate<int>(bomb,"_angle")}/{native[at+9]}, beforePART{beforeState}/{expectedState}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                FailIf(RandomDiffers(startRandom, native[0xff94], native[0xff95], native.RandomCalls - nativeRandom)||!sounds.Requests.SequenceEqual(native.Sounds.Skip(nativeSounds)),
                    "King minion handoff must retain shared item/AI RNG and ordered cues.");observedCapture.Samples=0;
            });
            try
            {
                Step(5);
                if(gate==1){_runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(4);}
                if(gate==2){_runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;Step(9);}
                if(gate==3){_dialogue.ShowMessage("Minion handoff hold.",104);native[0xcba0]=1;Step(4);_dialogue.Close();native[0xcba0]=0;}
                _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;Step(16);
                FailIf(bomb.State!=2||flight<5,"The actual ENEMY jump must release its held PART at the source apex and retain independent flight.");
                _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
            }
            finally{parent.Free();}
        }
    }
}
