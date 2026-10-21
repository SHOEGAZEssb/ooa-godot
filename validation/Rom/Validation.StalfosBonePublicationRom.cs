using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateStalfosBoneFlightPublicationRom()
    {
        int finishes=0,moves=0,bounces=0,lateTargets=0;
        foreach(int form in new[]{0,1,2,3})
        foreach(int variant in form==0?Enumerable.Range(0,64):form==1?Enumerable.Range(0,64):Enumerable.Range(0,4))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);_entities.Clear();
            int angle=form==0?variant&31:form==1?variant/16*8:variant*8;
            bool late=form==0&&variant>=32;int gate=variant&3;
            Vector2 initial=form==1?new(112+(variant%4%2)*8,80+(variant%4/2)*8):form==3?angle switch
                {0=>new(120,1),8=>new(166,88),16=>new(120,134),_=>new(1,88)}:new(120,88);
            if(fractional)initial+=new Vector2(.25f,.5f);
            var floor=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_collision.Collides(p)&&p.DistanceTo(initial)>=32 select p).ToArray();
            var motion=new ObjectMotionRom();motion.Word(ObjectMotionRom.Object+10,(int)(initial.Y*256));motion.Word(ObjectMotionRom.Object+12,(int)(initial.X*256));
            Vector2 link=default;bool found=false;
            foreach(var p in floor.OrderBy(p=>p.DistanceSquaredTo(initial+OracleObjectMovement.Shared.Direction(angle)*40)))
            {
                motion.Call(ObjectMotionRom.RelativeAngle,targetY:(int)p.Y,targetX:(int)p.X);
                if(motion.Result!=angle)continue;link=p;found=true;break;
            }
            // Edge observedCapture.Samples can aim outwards only through declared initialized flight.
            if(form==3){link=floor.OrderBy(p=>p.DistanceSquaredTo(new(120,144))).First();found=true;}
            FailIf(!found,"Bone flight fixture requires a clear source-derived aim for every declared angle.");
            Vector2 lateLink=floor.OrderByDescending(p=>p.DistanceSquaredTo(link)).First();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            if(form is 1 or 2)_currentRoom.SetPositionTileAndCollision(initial,_currentRoom.GetMetatile(initial),form==2?(byte)255:(byte)(1<<(variant/4%4)),0);
            var actor=_entities.Spawn<StalfosBoneProjectile>(new StalfosBoneSpawn(initial));
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[WramAddress.wWarpsDisabled]=1;native[0xcc08]=0x9b;
            native[0xcd16]=0;native[0xcd17]=0xd0;
            Vector2 camera=-_entities.WorldToScreen(Vector2.Zero);native[0xffaa]=(byte)camera.Y;native[0xffac]=(byte)camera.X;
            const int at=0xd0c0;native[at]=1;native[at+1]=0x1c;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;bool scrollHeld=gate==3;
            var before=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(late&&updates==0){_player.SetScriptedCoordinateHigh(true,(int)lateLink.X);_player.SetScriptedCoordinateHigh(false,(int)lateLink.Y);}
            });
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);bool alive=native[at]!=0;byte[] expected=new byte[4];
                // Declared raw hold input is reapplied each update; the native
                // camera epilogue sets bit7. This does not model a real scroll.
                if(scrollHeld)native[0xcd00]=8;
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(late&&updates==0){native[0xd00d]=(byte)lateLink.X;native[0xd00b]=(byte)lateLink.Y;}});
                native.AdvanceCameraPosition();updates++;
                if(alive&&native[at]==0)finishes++;
                if(alive&&native[at]!=0&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                if(alive&&oldState==1&&native[at]!=0&&native[at+4]==2)bounces++;
                if(late&&updates==1){lateTargets++;FailIf(native[at+9]!=angle,"Original PART$1c must aim at the pre-ENEMY Link target.");}
                int state=actor.State==HostileProjectileState.Bouncing?2:(int)actor.State;
                var animation=SomariaPrivate<EnemyAnimationPlayer>(actor,"_animation");camera=-_entities.WorldToScreen(Vector2.Zero);
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    state!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (actor.ZFixed&65535)!=native.Word(at+14)||(SomariaPrivate<int>(actor,"_speedZ")&65535)!=native.Word(at+0x14)||
                    actor.CollisionEnabled!=((native[at+0x24]&128)!=0)||actor.Visible!=((native[at+0x1a]&128)!=0)||
                    SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20])||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected)||
                    camera!=new Vector2(native[0xffac],native[0xffaa]),
                    $"Stalfos bone flight form{form}/variant{variant}/fraction{fractional}/host{batched}/update{updates}: dead{actor.Finished}/{native[at]==0}, state{state}/{native[at+4]}, C{actor.Counter}/{native[at+6]}, angle{actor.Angle}/{native[at+9]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, camera{camera}/{native[0xffac]},{native[0xffaa]}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, CEC[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Bone flight must retain native RNG and cue consumption.");observedCapture.Samples=0;
            });
            if(gate==1){_dialogue.ShowMessage("Bone state0 hold.",104);native[0xcba0]=1;}
            if(gate==2){_runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;}
            if(gate==3){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Step(2);
            if(form==3){F1Set(actor,"<Angle>k__BackingField",angle);native[at+9]=(byte)angle;}
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            scrollHeld=false;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(2);
            _dialogue.ShowMessage("Bone flight hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            scrollHeld=true;_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);scrollHeld=false;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!actor.Finished&&updates<260)Step(1);
            FailIf(!actor.Finished,"Every bone flight must retire at its original screen/terrain/bounce boundary.");
            Step(2);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(finishes!=544||moves==0||bounces==0||lateTargets!=128,"Bone flight matrix must complete544 flows, all128 late-target observedCapture.Samples, terrain bounces and movement.");
    }

    private void ValidateStalfosBoneBouncePublicationRom()
    {
        int finishes=0,moves=0;
        foreach(int angle in Enumerable.Range(0,32))
        foreach(int entry in new[]{-1,0,1,32})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,144));_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var actor=_entities.Spawn<StalfosBoneProjectile>(new StalfosBoneSpawn(new(120,88)));
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,144)
                {HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[WramAddress.wWarpsDisabled]=1;native[0xcc08]=0x9b;
            const int at=0xd0c0;native[at]=1;native[at+1]=0x1c;native[at+11]=88;native[at+13]=120;
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var before=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldX=native.Word(at+12),oldY=native.Word(at+10);bool alive=native[at]!=0;byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(alive&&native[at]==0)finishes++;
                if(alive&&native[at]!=0&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                int state=actor.State==HostileProjectileState.Bouncing?2:(int)actor.State;
                var animation=SomariaPrivate<EnemyAnimationPlayer>(actor,"_animation");
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    state!=native[at+4]||actor.Counter!=native[at+6]||actor.Angle!=native[at+9]||
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (actor.ZFixed&65535)!=native.Word(at+14)||(SomariaPrivate<int>(actor,"_speedZ")&65535)!=native.Word(at+0x14)||
                    actor.CollisionEnabled!=((native[at+0x24]&128)!=0)||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20])||
                    observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Stalfos bone bounce angle${angle:x2}/entry{entry}/fraction{fractional}/host{batched}/update{updates}: dead{actor.Finished}/{native[at]==0}, state{state}/{native[at+4]}, C{actor.Counter}/{native[at+6]}, angle{actor.Angle}/{native[at+9]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{actor.ZFixed}/{native.Word(at+14)}, speedZ{SomariaPrivate<int>(actor,"_speedZ")}/{native.Word(at+0x14)}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, CEC[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Bone bounce must retain native RNG and cue consumption.");observedCapture.Samples=0;
            });
            Step(1);
            // Declared post-initialization words; bounce ignores terrain/screen bounds.
            actor.Position=fractional?new(.25f,.5f):Vector2.Zero;
            native.Word(at+10,(int)(actor.Position.Y*256));native.Word(at+12,(int)(actor.Position.X*256));
            F1Set(actor,"<Angle>k__BackingField",angle);native[at+9]=(byte)angle;
            int z=fractional?-2*256+128:0;F1Set(actor,"_zFixed",z);native.Word(at+14,z);
            if(entry<0)
            {
                FailIf(!actor.DeflectWithSword(),"Declared source collision input requires a live bone.");native[at+0x2a]=0x81;
                Step(1);FailIf(native[at+4]!=2||native[at+6]!=32,"Original collision dispatch must initialize bounce without movement.");
            }
            else
            {
                F1Set(actor,"<State>k__BackingField",HostileProjectileState.Bouncing);F1Set(actor,"<Counter>k__BackingField",entry);
                F1Set(actor,"_speedZ",-0xe0);native[at+4]=2;native[at+6]=(byte)entry;native[at+16]=0x0a;
                native.Word(at+0x14,-0xe0);native[at+0x24]&=0x7f;
            }
            _dialogue.ShowMessage("Bone bounce hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,9);native[0xcc8a]=9;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!actor.Finished&&updates<50)Step(1);
            FailIf(!actor.Finished,"Bone bounce must delete on the original saturating counter boundary.");
            Step(2);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(finishes!=512||moves==0,"Bone bounce matrix must complete all512 declared lifetimes and execute wrapped movement.");
    }
}
