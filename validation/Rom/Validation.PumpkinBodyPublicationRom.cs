using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinBodyPublicationRom()
    {
        int moves=0,choices=0,stomps=0;
        foreach(int form in Enumerable.Range(0,7))
        foreach(int angle in new[]{0,8,16,24})
        foreach(bool lateTarget in form==3?new[]{false,true}:new[]{false})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);
            var parent=_entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single();var boss=(PumpkinHeadBoss)parent.Node;
            F1Retain(parent);F1Set(parent,"_initialized",true);F1Set(boss,"_initialized",true);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,144));
            FailIf(_collision.Collides(_player.Position),"Pumpkin movement fixture must hold Link on room$4:$13's actual floor.");
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            bool Blocked(Vector2I p)=>p.X<0||p.Y<0||p.X>=_currentRoom.Width||p.Y>=_currentRoom.Height||
                _currentRoom.IsSolidForEnemyMovement(p,holesAreWalls:true);
            var points=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_currentRoom.IsSolidForEnemyMovement(p,holesAreWalls:true)
                let bits=EnemyAdjacentWallResolver.Shared.Probe(p,angle,Blocked).Bitset
                where form==1?(angle is 0 or 16?(bits&12)==12:(bits&3)==3):
                    form==2?(angle is 0 or 16?(bits&12) is 4 or 8:(bits&3) is 1 or 2):bits==0
                orderby p.DistanceSquaredTo(new(120,88)) select p).ToArray();
            FailIf(points.Length==0,$"Pumpkin source room must provide actual walking probe form{form}/angle${angle:x2}.");
            Vector2 initial=points[0]+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            int counter=form==3?1:form==6?0:40,timer=form==4?1:form==5?0:90;
            boss.Position=initial.Floor();F1Set(boss,"_precisePosition",initial);F1Set(boss,"_headPosition",initial.Floor());
            F1Set(boss,"_ghostPosition",initial.Floor());F1Set(boss,"_bodyZFixed",0);F1Set(boss,"_headZ",-16);
            F1Set(boss,"_state",BossState.Active);F1Set(boss,"_walkCounter",counter);F1Set(boss,"_stompTimer",timer);
            F1Set(boss,"_stompsRemaining",2);F1Set(boss,"_angle",angle);
            var animation=SomariaPrivate<EnemyAnimationPlayer>(boss,"_body");animation.SetAnimation(0x0b+(angle>>3));
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,144)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x78*4];
            const int at=0xd080;
            foreach(int slot in new[]{0,1,2})
            {
                int p=at+slot*256;native[p]=1;native[p+1]=0x78;native[p+2]=(byte)(slot+1);native[p+4]=(byte)(slot==0?0x0b:slot==1?0x0b:0x0a);
                native[p+0x29]=8;native[p+0x1a]=0x83;native[p+11]=(byte)initial.Y;native[p+13]=(byte)initial.X;
                native.Word(p+0x16,at);native.SetEnemyAnimation(slot,slot==0?0x0b+(angle>>3):slot==1?0x0a:(angle>>2)&6);
            }
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native[at+6]=(byte)counter;native[at+7]=(byte)timer;native[at+9]=(byte)angle;native[at+16]=0x14;
            native[at+0x30]=2;native.Word(at+0x16,0xd180);native.Word(at+0x18,0xd280);native[0xd28f]=0xf0;
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(lateTarget&&boss.State==BossState.Active&&SomariaPrivate<int>(boss,"_walkCounter")==1)
                    _player.SetScriptedCoordinateHigh(horizontal:true,200);
            });
            _entities.RegisterEnemySlot(stage,15);_entities.AddEntity(stage);
            // The production composite follows physical ENEMY slots and still
            // precedes PART. There are no projectiles in this bounded trace.
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldX=native.Word(at+12),oldY=native.Word(at+10),oldCounter=native[at+6];byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(lateTarget&&native[at+4]==0x0b&&native[at+6]==1)native[0xd00d]=200;});updates++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10))moves++;
                if(native[at+4]==0x0d)stomps++;
                if(native[at+4]==0x0c||native[at+4]==0x0b&&native[at+6]>oldCounter&&oldCounter!=0)choices++;
                int state=boss.State switch{BossState.Active=>0x0b,BossState.Firing=>0x0c,BossState.PreparingStomp=>0x0d,_=>-1};
                int c=SomariaPrivate<int>(boss,state==0x0b?"_walkCounter":"_counter");
                FailIf(state!=native[at+4]||c!=native[at+6]||SomariaPrivate<int>(boss,"_stompTimer")!=native[at+7]||
                    SomariaPrivate<Vector2>(boss,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    boss.Position!=new Vector2(native[at+13],native[at+11])||boss.Angle!=native[at+9]||
                    animation.CurrentParameter!=native[at+0x21]||SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20]||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Pumpkin body$78/form{form}/angle${angle:x2}/fraction{fractional}/update{updates}: state{boss.State}/${native[at+4]:x2}, C{c}/{native[at+6]}, timer{SomariaPrivate<int>(boss,"_stompTimer")}/{native[at+7]}, XY{SomariaPrivate<Vector2>(boss,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Pumpkin body walk decisions must preserve ordered native RNG and sound.");observedCapture.Samples=0;
            });
            Step(1);
            _dialogue.ShowMessage("Pumpkin walk hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(8);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(moves==0||choices==0||stomps==0,"Pumpkin walking must exercise terrain motion, action selection and the global-frame stomp gate.");
    }
}
