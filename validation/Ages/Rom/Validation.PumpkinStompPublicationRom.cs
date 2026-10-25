using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinStompPublicationRom()
    {
        int landings=0,moves=0;
        foreach(int direction in Enumerable.Range(0,32))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);
            var parent=_entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single();var boss=(PumpkinHeadBoss)parent.Node;
            F1Retain(parent);F1Set(parent,"_initialized",true);F1Set(boss,"_initialized",true);
            Vector2 initial=new(120,88);if(fractional)initial+=new Vector2(.25f,.5f);
            var targets=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_collision.Collides(p)&&
                    OracleObjectMovement.Shared.RelativeAngle(initial,p)==direction&&p.DistanceTo(initial)>32
                orderby p.DistanceSquaredTo(initial) select p).ToArray();
            FailIf(targets.Length==0,$"Pumpkin stomp fixture needs actual clear Link floor for angle${direction:x2}.");
            Vector2 link=targets[0];_inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            boss.Position=initial.Floor();F1Set(boss,"_precisePosition",initial);F1Set(boss,"_headPosition",initial.Floor());
            F1Set(boss,"_ghostPosition",initial.Floor());F1Set(boss,"_bodyZFixed",0);F1Set(boss,"_headZ",-16);F1Set(boss,"_ghostZ",0);
            if(fractional)
            {
                // Declared retained follower words, independently of body lows.
                F1Set(boss,"_ghostPosition",initial.Floor()+new Vector2(.75f,.25f));
                F1Set(boss,"_headPosition",initial.Floor()+new Vector2(.5f,.75f));
                F1Set(boss,"_stompGhostZFixed",0x40);F1Set(boss,"_stompHeadZFixed",-0x1000+0x80);
            }
            F1Set(boss,"_state",BossState.PreparingStomp);F1Set(boss,"_counter",1);F1Set(boss,"_stompTimer",90);
            F1Set(boss,"_stompsRemaining",2);F1Set(boss,"_angle",16);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[AgesWramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x78*4];
            const int at=0xd080;
            foreach(int slot in new[]{0,1,2})
            {
                int p=at+slot*256;native[p]=1;native[p+1]=0x78;native[p+2]=(byte)(slot+1);native[p+4]=(byte)(slot==0?0x0d:slot==1?0x0b:0x0a);
                native[p+0x29]=8;native[p+0x1a]=0x83;native[p+11]=(byte)initial.Y;native[p+13]=(byte)initial.X;
                native.Word(p+0x16,at);native.SetEnemyAnimation(slot,slot==0?0x0d:slot==1?0x0a:4);
            }
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native[at+6]=1;
            native[at+7]=90;native[at+9]=16;native[at+16]=0x14;native[at+0x30]=2;
            native.Word(at+0x16,0xd180);native.Word(at+0x18,0xd280);native[0xd28f]=0xf0;
            if(fractional)
            {native[0xd18c]=0xc0;native[0xd18a]=0x40;native[0xd28c]=0x80;native[0xd28a]=0xc0;native[0xd18e]=0x40;native[0xd28e]=0x80;}
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,stompMoves=0;
            var stage=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(stage,15);_entities.AddEntity(stage);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});
                native.AdvanceScreenShake();updates++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)){moves++;if(oldState==0x0e)stompMoves++;}
                if(oldState==0x0e&&native[at+4]==0x0f)
                {
                    landings++;
                    FailIf(oldX!=native.Word(at+12)||oldY!=native.Word(at+10),"Source Pumpkin landing must skip planar movement.");
                }
                int state=boss.State switch{BossState.Active=>0x0b,BossState.PreparingStomp=>0x0d,BossState.Stomping=>0x0e,BossState.StompLanded=>0x0f,_=>-1};
                int c=SomariaPrivate<int>(boss,state==0x0b?"_walkCounter":"_counter");
                FailIf(state!=native[at+4]||c!=native[at+6]||SomariaPrivate<int>(boss,"_stompTimer")!=native[at+7]||
                    SomariaPrivate<Vector2>(boss,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (SomariaPrivate<int>(boss,"_bodyZFixed")&65535)!=native.Word(at+14)||
                    (SomariaPrivate<int>(boss,"_bodySpeedZ")&65535)!=native.Word(at+0x14)||boss.Angle!=native[at+9]||
                    boss.GhostPosition!=new Vector2(native.Word(0xd18c)/256f,native.Word(0xd18a)/256f)||
                    boss.HeadPosition!=new Vector2(native.Word(0xd28c)/256f,native.Word(0xd28a)/256f)||
                    SomariaPrivate<int>(boss,"_ghostZ")!=unchecked((sbyte)native[0xd18f])||boss.HeadZ!=unchecked((sbyte)native[0xd28f])||
                    (SomariaPrivate<int>(boss,"_stompGhostZFixed")&65535)!=native.Word(0xd18e)||
                    (SomariaPrivate<int>(boss,"_stompHeadZFixed")&65535)!=native.Word(0xd28e)||
                    observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Pumpkin stomp$78/angle${direction:x2}/fraction{fractional}/update{updates}: state{boss.State}/${native[at+4]:x2}, C{c}/{native[at+6]}, angle{boss.Angle}/{native[at+9]}, XY{SomariaPrivate<Vector2>(boss,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{SomariaPrivate<int>(boss,"_bodyZFixed")}/{unchecked((short)native.Word(at+14))}, followers{boss.GhostPosition}/{boss.HeadPosition} Z{SomariaPrivate<int>(boss,"_ghostZ")},{boss.HeadZ}/{unchecked((sbyte)native[0xd18f])},{unchecked((sbyte)native[0xd28f])}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Pumpkin repeated stomps must retain native decision/shake RNG and ordered cues.");observedCapture.Samples=0;
            });
            Step(4);
            _dialogue.ShowMessage("Pumpkin stomp hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(boss.State!=BossState.Active&&updates<170)Step(1);
            FailIf(boss.State!=BossState.Active||stompMoves==0,"Two complete source stomps must return to walking.");Step(2);
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(landings!=256||moves==0,"Pumpkin stomp matrix must complete two landings for every angle/fraction/host.");
    }
}
