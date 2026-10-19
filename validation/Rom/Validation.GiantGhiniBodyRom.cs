using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGiantGhiniBodyRom()
    {
        foreach(int scenario in Enumerable.Range(0,10))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0x18);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            Vector2 camera=scenario==3?new(80,48):Vector2.Zero;
            Vector2 link=scenario switch{2=>new(120,88),3=>new(152,104),6=>new(120,88),7=>new(232,168),9=>new(120,88),_=>new(80,40)};
            _player.WarpTo(link);
            Vector2 initial=scenario==0?new(80,48):new(80,88);
            if(fractional)initial+=new Vector2(.25f,.5f);
            bool charging=scenario is 6 or 7 or 9;
            int counter2=scenario switch{4=>0,5=>1,6=>150,7=>0,9=>4,_=>100};
            int speed=scenario==7?0x50:charging?5:0x1e;
            int angle=charging?8:0;
            int targetY=link.Y-camera.Y<72?104:40;if(scenario==1)targetY=40;
            var record=new DungeonBossDatabase().Enemy(0x70);var actor=new GiantGhiniBoss();
            actor.Initialize(record,_currentRoom,initial,_random,_sound.PlaySound,()=>false,
                _entities.DisableLinkCollisionsAndMenu,()=>{},_entities.ToScreen);
            F1Set(actor,"_initialized",true);F1Set(actor,"_state",charging?GiantGhiniBossBossState.Charging:GiantGhiniBossBossState.Moving);
            F1Set(actor,"_nudgeCounter",1);F1Set(actor,"_childRespawnCounter",counter2);F1Set(actor,"_counter",counter2);
            F1Set(actor,"_speed",speed);F1Set(actor,"_angle",angle);F1Set(actor,"_targetScreenY",targetY);
            F1Set(actor,"_targetPacked",((int)link.Y&0xf0)|((int)link.X>>4));F1Set(actor,"_childrenAlive",1);
            F1Set(actor,"_childRequestsCharge",scenario is 8 or 9);
            var adapter=new GiantGhiniBossRoomEntity(actor,Vector2I.Zero);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,zeroVisits=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            for(int slot=2;slot<15;slot++)
            {var held=new EnemyAiSlotReservation();_entities.RegisterEnemySlot(held,slot);_entities.AddEntity(held);}
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xad;
            native[0xffaa]=(byte)camera.Y;native[0xffac]=(byte)camera.X;
            // Declared no-op occupied slots isolate body publication and prevent
            // accidental child allocation from hiding it later in the walk.
            foreach(int slot in Enumerable.Range(0,16).Where(slot=>slot!=1))
            {int held=0xd080+slot*256;native[held]=1;native[held+1]=0x35;native[held+4]=2;native[held+0x29]=17;}
            const int at=0xd180;native[at]=1;native[at+1]=0x70;native[at+4]=(byte)(charging?10:9);
            native[at+6]=1;native[at+7]=(byte)counter2;native[at+9]=(byte)angle;native[at+16]=(byte)speed;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native.Word(at+14,0xf800);native.Word(at+0x14,0x40);native[at+0x33]=16;
            native[at+0x29]=(byte)record.Health;native[at+0x30]=1;native[at+0x34]=(byte)targetY;
            native[at+0x32]=(byte)(scenario is 8 or 9?1:0);native[at+0x35]=(byte)(((int)link.Y&0xf0)|((int)link.X>>4));
            actor.Animation.SetAnimation(charging?1:0);native.SetEnemyAnimation(1,charging?1:0);
            var projection=_entities.WorldToScreen;_entities.WorldToScreen=point=>point-camera;
            try
            {
                void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                    native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                    {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                    int state=actor.State==GiantGhiniBossBossState.Moving?9:10;
                    int c2=SomariaPrivate<int>(actor,state==9?"_childRespawnCounter":"_counter");
                    if(native[at+4]==10&&native[at+7]==0)zeroVisits++;
                    FailIf(state!=native[at+4]||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        SomariaPrivate<int>(actor,"_angle")!=native[at+9]||SomariaPrivate<int>(actor,"_speed")!=native[at+16]||
                        SomariaPrivate<int>(actor,"_nudgeCounter")!=native[at+6]||c2!=native[at+7]||
                        (int)(SomariaPrivate<float>(actor,"_z")*256)!=unchecked((short)native.Word(at+14))||
                        SomariaPrivate<int>(actor,"_floatCounter")!=native[at+0x33]||
                        SomariaPrivate<int>(actor,"_targetScreenY")!=native[at+0x34]||SomariaPrivate<int>(actor,"_targetPacked")!=native[at+0x35]||
                        SomariaPrivate<bool>(actor,"_childRequestsCharge")!=(native[at+0x32]!=0)||
                        SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||
                        probes!=1||!observed.SequenceEqual(expected),
                        $"Giant Ghini/scenario{scenario}/fraction{fractional}/update{updates}: original body differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{state}/{native[at+4]}, angle{SomariaPrivate<int>(actor,"_angle")}/{native[at+9]}, C1{SomariaPrivate<int>(actor,"_nudgeCounter")}/{native[at+6]}, C2{c2}/{native[at+7]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                    var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                        $"Giant Ghini/scenario{scenario}/update{updates}: original live-child respawn RNG differs: calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");probes=0;
                });
                Step(2);_dialogue.ShowMessage("Giant Ghini hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
                if(charging||scenario==8)
                {
                    int remaining=400;while(actor.State==GiantGhiniBossBossState.Charging&&remaining-->0)Step(1);
                    FailIf(actor.State!=GiantGhiniBossBossState.Moving||scenario==7&&zeroVisits<3,
                        "Giant Ghini charge must reach its source short-position target, retain zero saturation and return through the real timer reset.");
                }
                Step(12);
                _entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
            }
            finally{_entities.WorldToScreen=projection;}
        }
    }
}
