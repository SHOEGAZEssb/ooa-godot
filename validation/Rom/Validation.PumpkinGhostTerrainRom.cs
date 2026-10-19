using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePumpkinGhostTerrainRom()
    {
        int moves=0,finalMoves=0,waitChoices=0;
        foreach(int form in Enumerable.Range(0,7))
        foreach(int variant in form==0?Enumerable.Range(0,64):form==1?Enumerable.Range(0,96):form is 2 or 6?Enumerable.Range(0,32):new[]{0})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);
            var parent=_entities.EntityAdapters<PumpkinHeadBossRoomEntity>().Single();var boss=(PumpkinHeadBoss)parent.Node;
            F1Retain(parent);F1Set(parent,"_initialized",true);F1Set(boss,"_initialized",true);
            bool lateTarget=form==0&&variant>=32,held=form is not (3 or 4);
            int angle=form==0?variant%32:form==1?variant/3:form is 2 or 6?variant:8,terrain=form==1?variant%3:0;
            bool Blocked(Vector2I p)=>p.X<0||p.Y<0||p.X>=_currentRoom.Width||p.Y>=_currentRoom.Height||
                _currentRoom.IsSolidForEnemyMovement(p,holesAreWalls:true);
            var points=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                let p=new Vector2(x*4,y*4) where !_currentRoom.IsSolidForEnemyMovement(p,holesAreWalls:true)
                let bits=EnemyAdjacentWallResolver.Shared.Probe(p,angle,Blocked).Bitset
                where terrain==0?bits==0:terrain==1?(bits&12)==12||(bits&3)==3:(bits&12) is 4 or 8||(bits&3) is 1 or 2
                orderby p.DistanceSquaredTo(new(120,88)) select p).ToArray();
            FailIf(points.Length==0,$"Pumpkin ghost requires actual terrain form{terrain}/angle${angle:x2}.");
            Vector2 initial=(form==0?new Vector2(120,88):points[0])+(fractional?new Vector2(.25f,.5f):Vector2.Zero),link=new(120,144);
            if(form==0)
            {
                var targets=(from y in Enumerable.Range(1,_currentRoom.Height/4-2) from x in Enumerable.Range(1,_currentRoom.Width/4-2)
                    let p=new Vector2(x*4,y*4) where !_collision.Collides(p)&&p.DistanceTo(initial)>32&&
                        OracleObjectMovement.Shared.RelativeAngle(initial,p)==angle orderby p.DistanceSquaredTo(initial) select p).ToArray();
                FailIf(targets.Length==0,$"Ghost flee fixture needs actual Link floor for source angle${angle:x2}.");link=targets[0];
            }
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);_player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Ghost terrain fixture must keep Link on actual source-room floor.");
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);_entities.AddEntity(new ProjectileMotionContactSuppression());
            int counter=form is 3 or 6?1:form==4?2:form==5?0:20;
            int z=form is 0 or 2?-0x800:0;if(fractional)z|=0x80;
            int speedZ=form==0?0x90:0,state=form==0?0x13:form is 3 or 4 or 5?0x16:0x14;
            int initialAngle=form==0?0:angle,anim=state is 0x13 or 0x16?9:0x0a;
            boss.Position=new(72,80);F1Set(boss,"_precisePosition",boss.Position);F1Set(boss,"_ghostPosition",initial);
            F1Set(boss,"_ghostAngle",initialAngle);F1Set(boss,"_ghostTarget",Vector2.Zero);F1Set(boss,"_ghostZ",z>>8);
            F1Set(boss,"_exposedGhostZFixed",z);F1Set(boss,"_ghostSpeedZ",speedZ);F1Set(boss,"_bodyZFixed",0);
            F1Set(boss,"_headHeld",held);F1Set(boss,"_headPosition",new Vector2(180.5f,88.25f));F1Set(boss,"_headZ",0);
            F1Set(boss,"_ghostVisible",true);F1Set(boss,"_state",BossState.HeadExposed);F1Set(boss,"_counter",counter);
            F1Set(boss,"_exposedPhase",state==0x13?ExposedPhase.GhostFleeInit:state==0x16?ExposedPhase.GhostWait:ExposedPhase.GhostFlee);
            var animation=SomariaPrivate<EnemyAnimationPlayer>(boss,"_ghost");animation.SetAnimation(anim);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[WramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x78*4];
            const int at=0xd180;native[0xd0a9]=8;native.Word(0xd098,0xd280);
            native[at]=1;native[at+1]=0x78;native[at+2]=2;native[at+4]=(byte)state;native[at+6]=(byte)counter;
            native[at+9]=(byte)initialAngle;native[at+16]=0x32;native[at+0x29]=8;native[at+0x1a]=0xc2;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+14,z);
            native.Word(at+0x14,speedZ);native.Word(at+0x16,0xd080);native.SetEnemyAnimation(1,anim);
            native[0xd280]=1;native[0xd281]=0x78;native[0xd282]=3;native[0xd284]=(byte)(held?2:0x16);native[0xd285]=1;
            native.Word(0xd28a,88*256+0x40);native.Word(0xd28c,180*256+0x80);native[0xd2a9]=8;native.Word(0xd296,0xd080);native.SetEnemyAnimation(2,4);
            byte[] observed=new byte[4];int probes=0,updates=0;
            var stage=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(lateTarget&&SomariaPrivate<ExposedPhase>(boss,"_exposedPhase")==ExposedPhase.GhostFleeInit)
                    _player.SetScriptedCoordinateHigh(horizontal:true,200);
            });
            _entities.RegisterEnemySlot(stage,15);_entities.AddEntity(stage);
            _entities.AddEntity(new PartMovementScratchProbe(()=>{probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldCounter=native[at+6],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                {if(lateTarget&&native[at+4]==0x13)native[0xd00d]=200;});updates++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10)){moves++;if(oldState==0x14&&oldCounter==1)finalMoves++;}
                if(oldState==0x16&&native[at+4]==0x14)waitChoices++;
                if(form==0&&updates==1)FailIf(native[at+9]!=(angle^16),"Despite its name, the source away helper preserves all32 aim directions.");
                int current=SomariaPrivate<ExposedPhase>(boss,"_exposedPhase") switch
                {ExposedPhase.GhostFleeInit=>0x13,ExposedPhase.GhostFlee or ExposedPhase.GhostRoam=>0x14,ExposedPhase.GhostWaitInit=>0x15,ExposedPhase.GhostWait=>0x16,ExposedPhase.GhostSeek=>0x17,_=>-1};
                FailIf(current!=native[at+4]||SomariaPrivate<int>(boss,"_counter")!=native[at+6]||
                    boss.GhostPosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||SomariaPrivate<int>(boss,"_ghostAngle")!=native[at+9]||
                    (SomariaPrivate<int>(boss,"_exposedGhostZFixed")&65535)!=native.Word(at+14)||
                    (SomariaPrivate<int>(boss,"_ghostSpeedZ")&65535)!=native.Word(at+0x14)||
                    SomariaPrivate<Vector2>(boss,"_ghostTarget")!=new Vector2(native[at+0x34],native[at+0x33])||
                    animation.CurrentParameter!=native[at+0x21]||SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20]||probes!=1||!observed.SequenceEqual(expected),
                    $"Pumpkin ghost$78/terrain/form{form}/variant{variant}/fraction{fractional}/update{updates}: state${current:x2}/${native[at+4]:x2}, C{SomariaPrivate<int>(boss,"_counter")}/{native[at+6]}, angle{SomariaPrivate<int>(boss,"_ghostAngle")}/{native[at+9]}, XY{boss.GhostPosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{SomariaPrivate<int>(boss,"_exposedGhostZFixed")}/{unchecked((short)native.Word(at+14))}, clock{SomariaPrivate<int>(animation,"_frameCounter")}/{native[at+0x20]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||
                    !sounds.Requests.SequenceEqual(native.Sounds),"Ghost flee/wait/roam must preserve native RNG and ordered cues.");probes=0;
            });
            Step(1);
            _dialogue.ShowMessage("Pumpkin ghost terrain hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(form==2?34:12);_entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(moves==0||finalMoves==0||waitChoices==0,"Ghost terrain checks must exercise movement, final flee visits and wait-counter priority.");
    }
}
