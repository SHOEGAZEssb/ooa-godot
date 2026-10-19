using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShadowHagBugPublicationRom()
    {
        int ties=0,finishes=0,moves=0;
        foreach(int form in new[]{0,1,2})
        foreach(int variant in form==0?Enumerable.Range(0,32):form==1?Enumerable.Range(0,64):new[]{0,1})
        foreach(int ownerCount in form==2?new[]{0,1}:new[]{1})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x4a);
            var template=_entities.Entities<ShadowHagBoss>().Single();var parent=new ShadowHagBoss();
            parent.Initialize(template.Record,_currentRoom,new(72,80),_random,
                SomariaPrivate<Action<int>>(template,"_playSound"),SomariaPrivate<Func<bool>>(template,"_shuttersClosed"),
                SomariaPrivate<Action>(template,"_disableLink"),SomariaPrivate<Action>(template,"_enableLink"),
                SomariaPrivate<Action>(template,"_restoreRoomMusic"),SomariaPrivate<Action<int,string,Vector2>>(template,"_showDialogue"),
                SomariaPrivate<Func<bool>>(template,"_dialogueOpen"),SomariaPrivate<string>(template,"_introMessage"));_entities.Clear();
            F1Set(parent,"_initialized",true);F1Set(parent,"_introActive",false);F1Set(parent,"_state",ShadowHagState.ShadowsConverging);
            F1Set(parent,"_counter1",255);F1Set(parent,"_counter2",1);F1Set(parent,"_bugsAlive",ownerCount);
            var parentAdapter=new ShadowHagBossRoomEntity(parent,Vector2I.Zero);F1Set(parentAdapter,"_initialized",true);
            _entities.RegisterEnemySlot(parentAdapter,5);_entities.AddEntity(parentAdapter);
            int angle=variant&31,gate=form==0?angle&3:0;bool late=form==1&&variant>=32;
            if(form==0)
            {
                var current=_random.CaptureState();var trial=new OracleRandom();bool found=false;
                for(int value=0x100;value<65536;value++)
                {
                    var candidate=current with{Rng1=(byte)value,Rng2=(byte)(value>>8)};trial.RestoreState(candidate);trial.Next();
                    if((trial.Next().Value&31)!=angle)continue;_random.RestoreState(candidate);found=true;break;
                }
                FailIf(!found,"Bug init fixture must select every source angle after the common initialization draw.");
            }
            Vector2 initial=new(120,88);if(fractional)initial+=new Vector2(.25f,.5f);
            var floor=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_collision.Collides(p) select p).ToArray();
            Vector2 link=floor.OrderBy(p=>p.DistanceSquaredTo(new(120,144))).First();
            if(form==1)
            {
                var trial=new OracleRandom();trial.RestoreState(_random.CaptureState());var result=trial.Next();
                var angleRom=new ObjectMotionRom();angleRom.Word(ObjectMotionRom.Object+10,(int)(initial.Y*256));angleRom.Word(ObjectMotionRom.Object+12,(int)(initial.X*256));
                Vector2 preferred=initial+OracleObjectMovement.Shared.Direction(angle^16)*40;bool found=false;
                foreach(var p in floor.Where(p=>p.DistanceTo(initial)>=24).OrderBy(p=>p.DistanceSquaredTo(preferred)))
                {
                    angleRom.Call(ObjectMotionRom.RelativeAngle,targetY:((int)p.Y+(result.High&15)-8)&255,targetX:((int)p.X+(result.Low&15)-8)&255);
                    if(angleRom.Result!=(angle^16))continue;link=p;found=true;break;
                }
                FailIf(!found,"Bug opposite-angle fixture must resolve an actual clear Link position through the original angle helper.");
            }
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var actor=new ShadowHagBug();actor.Initialize(new DungeonBossDatabase().Enemy(0x42),parent,_random,initial);
            int z=fractional?128:0,c1=form==1?1:form==2?5:0,c2=form==1?4:form==2?variant:0;
            F1Set(actor,"_zFixed",z);
            if(form!=0)
            {
                F1Set(actor,"_initialized",true);F1Set(actor,"_state",ShadowHagBugState.Chasing);F1Set(actor,"_angle",angle);
                F1Set(actor,"_counter1",c1);F1Set(actor,"_counter2",c2);actor.Visible=true;actor.Animation.SetAnimation(0);
            }
            var adapter=new ShadowHagBugRoomEntity(actor,_sound.PlaySound);_entities.RegisterEnemySlot(adapter,10);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x7a*4];native[0xcc0a]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x42*4];
            const int owner=0xd580,at=0xda80;
            native[owner]=1;native[owner+1]=0x7a;native[owner+4]=0x0b;native[owner+6]=255;native[owner+7]=1;
            native[owner+11]=80;native[owner+13]=72;native[owner+0x29]=(byte)parent.Health;native[owner+0x30]=(byte)ownerCount;
            native[at]=1;native[at+1]=0x42;native[at+4]=(byte)(form==0?0:9);native[at+6]=(byte)c1;native[at+7]=(byte)c2;
            native[at+9]=(byte)(form==0?0:angle);native[at+16]=(byte)(form==0?0:15);native[at+0x29]=2;native[at+0x1a]=(byte)(form==0?0:0x82);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+14,z);native.Word(at+0x16,owner);
            if(form!=0)native.SetEnemyAnimation(10,0);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var before=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(late&&updates==0){_player.SetScriptedCoordinateHigh(horizontal:true,200);_player.SetScriptedCoordinateHigh(horizontal:false,144);}
            });
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var after=new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldX=native.Word(at+12),oldY=native.Word(at+10);bool alive=native[at]!=0;byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(late&&updates==0){native[0xd00d]=200;native[0xd00b]=144;}});updates++;
                if(form==1&&updates==1){ties++;FailIf(native[at+9]!=((angle+1)&31),"Original bug steering must increment on the declared exact half-turn.");}
                if(alive&&native[at]==0)finishes++;
                if(alive&&native[at]!=0&&(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)))moves++;
                int state=actor.InitializationPending?0:SomariaPrivate<ShadowHagBugState>(actor,"_state")==ShadowHagBugState.Airborne?8:9;
                FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(state!=native[at+4]||actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (actor.ZFixed&65535)!=native.Word(at+14)||(SomariaPrivate<int>(actor,"_speedZ")&65535)!=native.Word(at+0x14)||
                    SomariaPrivate<int>(actor,"_angle")!=native[at+9]||SomariaPrivate<int>(actor,"_counter1")!=native[at+6]||SomariaPrivate<int>(actor,"_counter2")!=native[at+7]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||actor.AnimationParameter!=native[at+0x21]||SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20])||
                    parent.BugsAlive!=native[owner+0x30]||probes!=1||!observed.SequenceEqual(expected),
                    $"Shadow Hag ENEMY$42/form{form}/angle${angle:x2}/late{late}/ownerCount{ownerCount}/fraction{fractional}/update{updates}: dead{actor.IsDead}/{native[at]==0}, state{state}/{native[at+4]}, C{SomariaPrivate<int>(actor,"_counter1")},{SomariaPrivate<int>(actor,"_counter2")}/{native[at+6]},{native[at+7]}, angle{SomariaPrivate<int>(actor,"_angle")}/{native[at+9]}, parentCount{parent.BugsAlive}/{native[owner+0x30]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                if(!actor.IsDead)
                {
                    var offset=(Vector2)actor.GetType().GetProperty("AnimationDrawOffset",F1Hidden)!.GetValue(actor)!;
                    FailIf(offset-actor.Animation.CurrentOffset!=Vector2.Down*unchecked((sbyte)native[at+15]),
                        "Bug presentation must add the original integer high-Z byte, retaining fractional Z only in physics.");
                }
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Bug initialization/landing/steering must retain ordered common/AI RNG and cues.");probes=0;
            });
            int mask=gate==2?5:1;_runtimeState.SetWramByte(0xcc8a,(byte)mask);native[0xcc8a]=(byte)mask;
            if(gate==1){_dialogue.ShowMessage("Bug common init hold.",104);native[0xcba0]=1;}
            if(gate==3){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Step(1);if(form==0)Step(2);
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            _dialogue.ShowMessage("Bug movement hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!actor.IsDead&&updates<260)Step(1);
            FailIf(!actor.IsDead,"Every healthy bug must finish its original lifetime before the bounded trace ends.");Step(2);
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(ties!=256||finishes!=400||moves==0,"Bug matrix must execute every opposite-angle pair and complete all fresh/declared lifetimes.");
    }
}
