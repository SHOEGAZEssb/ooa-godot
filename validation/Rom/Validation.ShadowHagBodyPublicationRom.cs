using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShadowHagBodyPublicationRom()
    {
        int moves=0,ends=0,lateMoves=0;
        Vector2I[] directions=[Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left];
        foreach(int form in Enumerable.Range(0,9))
        foreach(int variant in form==0?Enumerable.Range(0,3):form==7?new[]{0}:form==4?Enumerable.Range(0,8):Enumerable.Range(0,4))
        foreach(int count2 in form==0||form==8?new[]{2}:form==7?new[]{4}:form==5?new[]{0,1,2}:new[]{1,2})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x4a);
            var template=_entities.Entities<ShadowHagBoss>().Single();var actor=new ShadowHagBoss();
            actor.Initialize(template.Record,_currentRoom,Vector2.Zero,_random,
                SomariaPrivate<Action<int>>(template,"_playSound"),SomariaPrivate<Func<bool>>(template,"_shuttersClosed"),
                SomariaPrivate<Action>(template,"_disableLink"),SomariaPrivate<Action>(template,"_enableLink"),
                SomariaPrivate<Action>(template,"_restoreRoomMusic"),SomariaPrivate<Action<int,string,Vector2>>(template,"_showDialogue"),
                SomariaPrivate<Func<bool>>(template,"_dialogueOpen"),SomariaPrivate<string>(template,"_introMessage"));_entities.Clear();
            int angle=form==0?24:variant%4*8;
            Vector2 initial=form==0?new Vector2(new[]{119,120,145}[variant],96):form is 5 or 6?new(120,88):angle switch
                {0=>new(120,130),8=>new(40,80),16=>new(120,30),_=>new(200,80)};
            if(form==4)initial=new Vector2[]{new(23,88),new(24,88),new(215,88),new(216,88),new(120,17),new(120,18),new(120,143),new(120,144)}[variant];
            if(fractional)initial+=new Vector2(.25f,.5f);
            var points=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_collision.Collides(p) orderby p.DistanceSquaredTo(form==8?new(120,88):new(120,152)) select p).ToArray();
            Vector2 link=points[0];int lookedDirection=((OracleObjectMovement.Shared.RelativeAngle(initial,link)+20)&24)>>3;
            int facing=form==8?variant:form==5?lookedDirection:form==6?3:(lookedDirection+1)&3;
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.Face(directions[facing]);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            actor.Position=initial;actor.Visible=form is not (0 or 7 or 8);F1Set(actor,"_initialized",true);F1Set(actor,"_introActive",form==0);
            var state=form==0?ShadowHagState.IntroMovingToCenter:form==1?ShadowHagState.ChargeTell:form==7?ShadowHagState.ShadowsChasing:
                form==8?ShadowHagState.WaitingBehindLink:ShadowHagState.Charging;
            int c1=form==0||form==3?0:form is 1 or 8?30:form is 2 or 7?1:60,z=(form==0?-256:0)+(fractional?128:0);
            F1Set(actor,"_state",state);F1Set(actor,"_counter1",c1);F1Set(actor,"_counter2",count2);F1Set(actor,"_angle",angle);F1Set(actor,"_zFixed",z);
            actor.Animation.SetAnimation(form is 0 or 7?0:form==8?4:angle>>3);
            var adapter=new ShadowHagBossRoomEntity(actor,Vector2I.Zero);F1Set(adapter,"_initialized",true);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,facing,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x7a*4];
            const int at=0xd180;native[at]=1;native[at+1]=0x7a;native[at+4]=(byte)(form==0?8:form==1?0x11:form==7?0x0a:form==8?0x0f:0x12);
            native[at+5]=(byte)(form==0?1:0);native[at+6]=(byte)c1;native[at+7]=(byte)count2;native[at+9]=(byte)angle;
            native[at+16]=(byte)(form==0?0x14:0x3c);native[at+0x29]=(byte)actor.Health;native[at+0x1a]=(byte)(form==0?0:0x82);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+14,z);
            native.Word(at+0x18,0xd0c0); // Declared inactive shadow reference at the intro completion boundary.
            native.SetEnemyAnimation(1,form is 0 or 7?0:form==8?4:angle>>3);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var before=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(form==6&&updates==0){_player.SetScriptedCoordinateHigh(horizontal:true,200);_player.SetScriptedCoordinateHigh(horizontal:false,88);}
            });
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var after=new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(form==6&&updates==0){native[0xd00d]=200;native[0xd00b]=88;}});updates++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)){moves++;if(form==6&&updates==1)lateMoves++;}
                if(oldState is 0x11 or 0x12&&native[at+4] is 9 or 0x13)ends++;
                var mapped=actor.State switch
                {
                    ShadowHagState.IntroMovingToCenter=>(8,1),ShadowHagState.IntroEmerging=>(8,2),ShadowHagState.IntroDialogueDelay=>(8,3),
                    ShadowHagState.ChargeTell=>(0x11,0),ShadowHagState.Charging=>(0x12,0),ShadowHagState.ReturnDelay=>(0x13,0),
                    ShadowHagState.GroundEyes=>(9,0),ShadowHagState.ShadowsChasing=>(0x0a,0),ShadowHagState.ShadowsConverging=>(0x0b,0),
                    ShadowHagState.BugSpawnDelay=>(0x0c,0),ShadowHagState.WaitingBehindLink=>(0x0f,0),ShadowHagState.ChargeEmerging=>(0x10,0),_=>(-1,-1)
                };
                FailIf(mapped.Item1!=native[at+4]||mapped.Item1==8&&mapped.Item2!=native[at+5]||actor.Counter1!=native[at+6]||actor.Counter2!=native[at+7]||
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (SomariaPrivate<int>(actor,"_zFixed")&65535)!=native.Word(at+14)||SomariaPrivate<int>(actor,"_angle")!=native[at+9]||
                    actor.AnimationParameter!=native[at+0x21]||SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||
                    probes!=1||!observed.SequenceEqual(expected),
                    $"Shadow Hag$7a/form{form}/variant{variant}/C2{count2}/fraction{fractional}/update{updates}: state{actor.State}/${native[at+4]:x2},{native[at+5]}, C{actor.Counter1},{actor.Counter2}/{native[at+6]},{native[at+7]}, XYZ{actor.Position}/{SomariaPrivate<int>(actor,"_zFixed")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}/{unchecked((short)native.Word(at+14))}, param{actor.AnimationParameter}/{native[at+0x21]}, clock{SomariaPrivate<int>(actor.Animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Shadow Hag motion must preserve native RNG and ordered cues.");probes=0;
            });
            Step(1);
            _dialogue.ShowMessage("Shadow Hag motion hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(form==0)
            {
                while(actor.State is ShadowHagState.IntroMovingToCenter or ShadowHagState.IntroEmerging&&updates<180)Step(1);
                FailIf(actor.State!=ShadowHagState.IntroDialogueDelay,"Shadow Hag intro motion must finish emergence before the declared textbox boundary.");Step(3);
            }
            else if(form==7)
            {
                while(actor.State==ShadowHagState.ShadowsChasing&&updates<30)Step(1);
                FailIf(actor.State!=ShadowHagState.ShadowsConverging,"Hag convergence must consume the source RNG row and copy high YX while retaining its own lows.");Step(3);
            }
            else if(form==8)
            {
                while(actor.State==ShadowHagState.WaitingBehindLink&&updates<70)Step(1);
                FailIf(actor.State is not (ShadowHagState.ChargeEmerging or ShadowHagState.BugSpawnDelay),"Hag spawn must copy its source high YX or take the original failed-position continuation.");Step(3);
            }
            else
            {
                while(actor.State is ShadowHagState.ChargeTell or ShadowHagState.Charging&&updates<180)Step(1);
                FailIf(actor.State is not (ShadowHagState.ReturnDelay or ShadowHagState.GroundEyes),"Shadow Hag must finish its source charge before returning.");Step(20);
            }
            _entities.Clear();_entities.EnableLinkCollisionsAndMenu();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(moves==0||ends==0||lateMoves!=32,"Shadow Hag movement must execute intro/charge and survive the first declared late-Link write in every cached-target case.");
    }
}
