using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinGhostSeekRom()
    {
        int arrivals=0,moves=0;
        int[] offsets=[-9,-8,0,8,9];
        foreach(bool wrapped in new[]{false,true})
        foreach(int dx in offsets)
        foreach(int dy in offsets)
        foreach(int mode in new[]{0,1,2})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            bool held=mode==1;
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);
            var parent=_entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single();var boss=(PumpkinHeadBoss)parent.Node;
            F1Retain(parent);F1Set(parent,"_initialized",true);F1Set(boss,"_initialized",true);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,144));_player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Pumpkin seek fixture must keep Link on room$4:$13's actual floor.");
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            Vector2 target=wrapped?new(2,2):new(120,88),body=new(72,80);
            Vector2 initial=new(((int)target.X+dx)&255,((int)target.Y+dy)&255);
            if(fractional)initial+=new Vector2(.5f,.25f);
            boss.Position=body;F1Set(boss,"_precisePosition",body);F1Set(boss,"_ghostPosition",initial);
            F1Set(boss,"_ghostTarget",target);F1Set(boss,"_ghostAngle",8);F1Set(boss,"_ghostZ",0);F1Set(boss,"_bodyZFixed",0);
            F1Set(boss,"_headPosition",target);F1Set(boss,"_headZ",0);F1Set(boss,"_headHeld",held);
            F1Set(boss,"_ghostVisible",true);F1Set(boss,"_state",BossState.HeadExposed);F1Set(boss,"_exposedPhase",ExposedPhase.GhostSeek);
            if(mode==2)
            {
                // Declared released-head state02 identity. Its distant arc
                // cannot hit the ghost; no real Bracelet handoff is claimed.
                F1Set(boss,"_headThrown",true);F1Set(boss,"_headPosition",new Vector2(200,144));F1Set(boss,"_headZ",-32);
                F1Set(boss,"_thrownHeadXFixed",200*256);F1Set(boss,"_thrownHeadYFixed",144*256);
                F1Set(boss,"_thrownHeadZFixed",-32*256);F1Set(boss,"_throwDirection",Vector2I.Right);F1Set(boss,"_thrownHeadRadiusX",6);
            }
            var animation=SomariaPrivate<EnemyAnimationPlayer>(boss,"_ghost");animation.SetAnimation(0x0a);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,144)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x78*4];
            const int at=0xd180;
            // Declared inactive healthy body retains the source related pointers.
            // The head is either state02/held or state16/grabbable; no native
            // Bracelet parent is claimed by this handler-boundary fixture.
            native[0xd0a9]=8;native[0xd08b]=80;native[0xd08d]=72;native.Word(0xd098,0xd280);
            native[at]=1;native[at+1]=0x78;native[at+2]=2;native[at+4]=0x17;native[at+9]=8;native[at+16]=0x32;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+0x16,0xd080);
            native[at+0x29]=8;native[at+0x1a]=0xc2;native[at+0x33]=(byte)target.Y;native[at+0x34]=(byte)target.X;native.SetEnemyAnimation(1,0x0a);
            native[0xd280]=1;native[0xd281]=0x78;native[0xd282]=3;native[0xd284]=(byte)(mode!=0?2:0x16);native[0xd285]=(byte)(mode==2?2:1);
            native[0xd28b]=(byte)target.Y;native[0xd28d]=(byte)target.X;native[0xd2a9]=8;native.Word(0xd296,0xd080);native.SetEnemyAnimation(2,4);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var stage=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(stage,15);_entities.AddEntity(stage);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(oldState==0x17&&native[at+4]==0x0b)arrivals++;
                if(oldState==0x17&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                bool done=native[at+4]==0x0b;
                FailIf((boss.State==BossState.Regenerating)!=done||boss.GhostPosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(boss,"_ghostAngle")!=native[at+9]||SomariaPrivate<bool>(boss,"_ghostVisible")!=((native[at+0x1a]&128)!=0)||
                    animation.CurrentParameter!=native[at+0x21]||SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20]||
                    done&&SomariaPrivate<int>(boss,"_counter")!=native[0xd286]||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Pumpkin ghost$78/seek/wrap{wrapped}/offset{dx},{dy}/mode{mode}/fraction{fractional}/update{updates}: state{boss.State}/${native[at+4]:x2}, XY{boss.GhostPosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{SomariaPrivate<int>(boss,"_ghostAngle")}/{native[at+9]}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Pumpkin seek boundaries must preserve source RNG and cues.");observedCapture.Samples=0;
            });
            Step(3);
            _dialogue.ShowMessage("Pumpkin ghost seek hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(3);
            FailIf(mode!=0?boss.State!=BossState.HeadExposed:boss.State!=BossState.Regenerating,
                "Source ghost arrival must wait for held/released state02 and enter regeneration only for the declared grabbable head.");
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(arrivals!=200||moves==0,"Pumpkin seek must cross the high-byte box edges and complete all unheld entries.");
    }
}
