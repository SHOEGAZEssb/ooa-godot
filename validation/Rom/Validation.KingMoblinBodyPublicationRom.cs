using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinBodyPublicationRom()
    {
        int landings=0, births=0, throws=0;
        foreach(int scenario in new[]{8,0,1,2,3,4,5,6,7})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xaf);
            var world=_entities.Entities<KingMoblinBoss>().Single().World;_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(80,104));
            FailIf(_collision.Collides(_player.Position),"King body comparison must hold Link on the keep's actual clear floor.");
            // The original category mask holds initialized PARTs while the
            // ENEMY body can publish pickup/raise/throw writes into them.
            _runtimeState.SetWramByte(0xcc8a,9);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            int state=scenario<3?16:scenario<5?17:scenario==5?12:scenario<8?13:18;
            int angle=scenario is 1 or 4?24:scenario==8?0:8;
            int speed=scenario==8?0x78:0x1e,counter=state is 12 or 13?1:0;
            int target=scenario==4?64:100;
            Vector2 initial=new(scenario switch{0=>60,1=>100,3=>60,4=>110,_=>80},32);
            if(fractional)initial+=new Vector2(.25f,.5f);
            var actor=new KingMoblinBoss();actor.Initialize(world,initial);actor.Visible=true;
            F1Set(actor,"<State>k__BackingField",state);F1Set(actor,"<Counter>k__BackingField",counter);
            F1Set(actor,"<Speed>k__BackingField",speed);F1Set(actor,"_angle",angle);F1Set(actor,"_targetX",target);
            if(scenario==8){actor.Health=1;F1Set(actor,"_deathSequence",true);}
            int animation=state==16||state==17?(angle==24?3:1):state==12?4:state==13?2:6;
            actor.Animation.SetAnimation(animation);
            var adapter=new KingMoblinRoomEntity(actor);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,80,104)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=9;native[WramAddress.wWarpsDisabled]=1;
            var rom=ValidationRom.LoadCleanUs().Span;native[0xcc08]=rom[0xfdd4b+0x7f*4];native[0xcc0a]=0xab;
            const int at=0xd180,part=0xd0c0;
            native[at]=1;native[at+1]=0x7f;native[at+4]=(byte)state;native[at+7]=(byte)counter;
            native[at+9]=(byte)angle;native[at+16]=(byte)speed;native[at+0x29]=(byte)actor.Health;native[at+0x32]=(byte)target;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.SetEnemyAnimation(1,animation);
            if(scenario is >=3 and <8)
            {
                var bomb=new KingMoblinBomb();bomb.Initialize(actor,null);bomb.Position=new(target+.75f,40.25f);
                F1Set(bomb,"<State>k__BackingField",scenario<5?4:1);F1Set(bomb,"_speed",0x55);
                F1Set(bomb,"_motion",new CarriedObjectMotion(bomb.Position){ZFixed=0x80});
                var child=new KingMoblinBombRoomEntity(bomb);F1Parts.Add(child,0);_entities.AddEntity(child);
                native[part]=1;native[part+1]=0x3f;native[part+4]=(byte)bomb.State;native[part+16]=0x55;native[part+9]=(byte)actor.Angle;
                native.Word(part+10,(int)(bomb.Position.Y*256));native.Word(part+12,(int)(bomb.Position.X*256));native.Word(part+14,0x80);
                native.Word(at+0x18,part);native.Word(part+0x16,at);
            }
            byte[] observed=new byte[4];int probes=0,updates=0;bool lateWritten=false;
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(scenario==7&&!lateWritten){lateWritten=true;_player.SetScriptedCoordinateHigh(horizontal:true,136);}
            });_entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var after=new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int priorState=native[at+4];byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(scenario==7&&updates==0)native[0xd00d]=136;});
                native.AdvanceScreenShake();updates++;
                if(priorState==19&&native[at+4]==20)landings++;
                if(priorState==11&&native[at+4]==12)births++;
                if(priorState==13&&native[at+4]==14)throws++;
                int expectedCounter=native[at+(native[at+4]==20?6:7)];
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.State!=native[at+4]||actor.Counter!=expectedCounter||actor.Speed!=native[at+16]||
                    SomariaPrivate<int>(actor,"_angle")!=native[at+9]||(SomariaPrivate<int>(actor,"_z")&65535)!=native.Word(at+14)||
                    (SomariaPrivate<int>(actor,"_speedZ")&65535)!=native.Word(at+0x14)||
                    SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||actor.AnimationParameter!=native[at+0x21]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"King body/scenario{scenario}/fraction{fractional}/update{updates}: state{actor.State}/{native[at+4]}, C{actor.Counter}/{expectedCounter}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{SomariaPrivate<int>(actor,"_z")}/{unchecked((short)native.Word(at+14))}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                if(actor.Bomb is { } bomb)
                {
                    var motion=SomariaPrivate<CarriedObjectMotion>(bomb,"_motion");
                    FailIf(native[part]==0||bomb.State!=native[part+4]||
                        bomb.Position!=new Vector2(native.Word(part+12)/256f,native.Word(part+10)/256f)||
                        (bomb.ZFixed&65535)!=native.Word(part+14)||SomariaPrivate<int>(bomb,"_angle")!=native[part+9]||
                        (motion.SpeedZ&65535)!=native.Word(part+0x14),
                        $"King body/scenario{scenario}/fraction{fractional}/update{updates}: related bomb state{bomb.State}/{native[part+4]}, XY{bomb.Position}/{native.Word(part+12)/256f},{native.Word(part+10)/256f}, Z{bomb.ZFixed}/{unchecked((short)native.Word(part+14))}, angle{SomariaPrivate<int>(bomb,"_angle")}/{native[part+9]}.");
                }
                else FailIf(native[part]!=0,"The source-born King bomb must appear in the actual PART pass.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                    !sounds.Requests.SequenceEqual(native.Sounds)||_entities.ScreenShakeCounter!=native[0xcd18]||_entities.HorizontalScreenShakeCounter!=native[0xcd19],
                    $"King body/scenario{scenario}/update{updates}: source birth/shake RNG or ordered cues differ.");probes=0;
            });
            Step(1);
            _dialogue.ShowMessage("King body hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,13);native[0xcc8a]=13;Step(3);_runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            int terminal=scenario<3?12:scenario<8?15:20;
            while(native[at+4]!=terminal&&updates<220)Step(1);
            FailIf(native[at+4]!=terminal,"King body must complete its source recenter/birth, pickup/throw or full ascent/bounce route.");
            Step(2);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(landings!=4||births!=12||throws!=20,"King body comparisons must observe each source handoff through both host schedules and retained low bytes.");
    }
}
