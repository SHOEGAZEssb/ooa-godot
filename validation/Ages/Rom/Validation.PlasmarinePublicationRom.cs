using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePlasmarinePublicationRom()
    {
        int arrivals=0,charges=0,moves=0;
        foreach(int form in Enumerable.Range(0,5))
        foreach(int variant in form==0?Enumerable.Range(0,32):form==1?Enumerable.Range(0,4):form>=3?new[]{1,2}:new[]{0})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);_inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            LoadValidationRoom(5,0x62);
            var template=_entities.Entities<PlasmarineCharacter>().Single();
            var record=template.Record;var world=SomariaPrivate<PlasmarineEnvironment>(template,"_world");_entities.Clear();
            bool scent=form==1&&(variant&1)!=0,lateTarget=form==1&&(variant&2)!=0;
            _inventory.GiveTreasure(TreasureId.SeedSatchel,1);_inventory.GiveTreasure(TreasureId.ScentSeeds,0x20);
            _inventory.SelectSatchelSeeds(1);_inventory.EquipA(TreasureId.SeedSatchel);_inventory.EquipB(0);
            _player.WarpTo(new(120,152));_player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Plasmarine target fixture must start on room$5:$62's actual clear floor.");
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,152)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[AgesWramAddress.wWarpsDisabled]=1;
            void PrepareStep(bool press=false)=>StepGameplayUpdates(1,Vector2.Zero,press?["attack"]:[],press?["attack"]:[],batched,()=>
            {native.UpdateGameplay(press?1:0,press?1:0,255,_entities.FrameCounter);});
            if(scent)
            {
                PrepareStep(true);
                for(int wait=0;(_entities.ActiveScentSeedTarget() is null||_player.IsUsingSeedSatchel)&&wait<50;wait++)PrepareStep();
                FailIf(_entities.ActiveScentSeedTarget() is not { } target||native[0xccd9]==0||target!=new Vector2(native[0xffb3],native[0xffb2]),
                    "Actual Satchel/ITEM updates must publish matching Scent targets before the Plasmarine charge fixture exists.");
            }
            else PrepareStep();
            // Declared initialized entries isolate movement and target publication;
            // the existing encounter and Hook tests cover actual state0/room entry.
            _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            Vector2 initial=form switch{0=>new(80,0),3=>new(116,84),4=>new(254,254),_=>new(120,88)};
            if(fractional)initial+=new Vector2(.25f,.5f);
            Vector2 targetPosition=form==2?new(200,88):form==4?new(2,2):new(120,88);
            int state=form==0?8:form==1?10:form==2?11:12;
            int counter=1,counter2=form>=3?variant:2,speed=form==2?5:100,angle=form==0?variant:8;
            var boss=new PlasmarineCharacter();boss.Initialize(record,initial,world);boss.Visible=true;
            F1Set(boss,"<State>k__BackingField",state);F1Set(boss,"<Counter1>k__BackingField",counter);
            F1Set(boss,"<Counter2>k__BackingField",counter2);F1Set(boss,"<Speed>k__BackingField",speed);
            F1Set(boss,"<Angle>k__BackingField",angle);F1Set(boss,"<Target>k__BackingField",targetPosition);
            F1Set(boss,"_collision",state!=8);boss.Animation.SetAnimation(0);
            var adapter=new PlasmarineRoomEntity(boss,world);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] headers=[0xcb,0xcc,0xcd,0x8e,0xa7,0x78];
            for(int i=0;i<headers.Length;i++)native[0xcc08+i*2]=headers[i];
            const int at=0xd180;native[at]=1;native[at+1]=0x7e;native[at+4]=(byte)state;native[at+6]=(byte)counter;
            native[at+7]=(byte)counter2;native[at+9]=(byte)angle;native[at+16]=(byte)speed;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native[at+0x30]=(byte)targetPosition.Y;native[at+0x31]=(byte)targetPosition.X;
            native[at+0x29]=7;native[at+0x1a]=0x83;native[at+0x1b]=1;native[at+0x1c]=1;
            native[at+0x24]=(byte)(state==8?0x7e:0xfe);native[at+0x25]=0x4f;native.SetEnemyAnimation(1,0);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;bool latePublished=false;
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                // Explicit earlier-ENEMY writer after the shared Link sample.
                if(lateTarget&&boss.State==10&&boss.Counter1==1&&(boss.AnimationParameter&1)!=0)
                {_player.SetScriptedCoordinateHigh(horizontal:true,200);latePublished=true;}
            });
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var startRandom=_random.CaptureState();int nativeRandom=native.RandomCalls,nativeSounds=native.Sounds.Count;
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(lateTarget&&native[at+4]==10&&native[at+6]==1&&(native[at+0x21]&1)!=0)native[0xd00d]=200;});updates++;
                if(oldState==8&&native[at+4]==9)arrivals++;
                if(oldState==10&&native[at+4]==11)
                {
                    charges++;
                    FailIf(native[at+0x30]!=152||native[at+0x31]!=120,
                        "Plasmarine must capture the pre-ENEMY Link sample, independently of actual Scent and an earlier enemy's coordinate write.");
                }
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10))moves++;
                FailIf(boss.State!=native[at+4]||boss.Counter1!=native[at+6]||boss.Counter2!=native[at+7]||
                    boss.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||boss.Angle!=native[at+9]||boss.Speed!=native[at+16]||
                    boss.Target!=new Vector2(native[at+0x31],native[at+0x30])||boss.Palette!=native[at+0x1b]||boss.CurrentOamFlags!=native[at+0x1c]||
                    boss.CollisionMode!=native[at+0x25]||boss.ProjectileCount!=native[at+0x34]||boss.AnimationParameter!=native[at+0x21]||
                    SomariaPrivate<int>(boss.Animation,"_frameCounter")!=native[at+0x20]||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Plasmarine$7e/form{form}/variant{variant}/fraction{fractional}/update{updates}: state{boss.State}/{native[at+4]}, C{boss.Counter1},{boss.Counter2}/{native[at+6]},{native[at+7]}, XY{boss.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle/speed{boss.Angle},{boss.Speed}/{native[at+9]},{native[at+16]}, target{boss.Target}/{native[at+0x31]},{native[at+0x30]}, clock{SomariaPrivate<int>(boss.Animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(startRandom, native[0xff94], native[0xff95], native.RandomCalls - nativeRandom)||
                    !sounds.Requests.SequenceEqual(native.Sounds.Skip(nativeSounds)),"Plasmarine motion must preserve shared ITEM/AI RNG and ordered sound cues.");observedCapture.Samples=0;
            });
            Step(1);
            _dialogue.ShowMessage("Plasmarine movement hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(form==0)
            {
                while(boss.State==8&&updates<300)Step(1);
                // Other declared angles can orbit this target in the original;
                // only the actual placed angle$08 is required to arrive.
                FailIf(variant==8&&boss.State!=9,"The actual Plasmarine intro angle$08 must reach the source target.");Step(2);
            }
            else if(form==1)
            {
                while(boss.State!=13&&updates<300)Step(1);
                FailIf(boss.State!=13||lateTarget&&!latePublished,"Plasmarine must capture its target, decelerate, arrive and enter shock state$0d.");
                Step(66);FailIf(boss.State!=10,"Plasmarine must finish shock and resume the source wait before another charge.");
            }
            else if(form==2)Step(5); // Declared zero-speed publication after the last deceleration.
            else Step(2); // Observe both arrival branches without advancing to projectile creation.
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(arrivals<4||charges!=16||moves==0,"Plasmarine movement must exercise source intro arrival and all real-Scent/cached-Link charge combinations.");
    }
}
