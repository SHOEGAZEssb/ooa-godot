using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateStalfosWalkPublicationRom() => CompareStalfosPublicationRom([0,2]);
    private void ValidateStalfosDodgePublicationRom() => CompareStalfosPublicationRom([1,3]);

    private void CompareStalfosPublicationRom(int[] forms)
    {
        int moves=0,turns=0,jumps=0,landings=0,zeroWraps=0,aims=0,starts=0;
        foreach(int form in forms)
        foreach(int variant in Enumerable.Range(0,form==0?96:form==1?64:form==2?32:12))
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x13);_entities.Clear();
            int angle=form==0?variant/3:form==3?variant/3*8:variant&31;
            int terrain=form==0?variant%3:0,sub=form is 1 or 3?2:form==0?(angle&1)*2:0;
            bool late=form==2||form==3||form==1&&variant>=32;
            var floor=(from y in Enumerable.Range(4,_currentRoom.Height/2-8) from x in Enumerable.Range(4,_currentRoom.Width/2-8)
                let p=new Vector2(x*2,y*2) where !_currentRoom.IsSolid(p) select p).ToArray();
            var linkFloor=floor.Where(p=>!_collision.Collides(p)).ToArray();
            Vector2 initial=new(120,88);
            if(form==0)
            {
                var candidates=floor.Where(p=>
                {
                    int bits=EnemyAdjacentWallResolver.Shared.Probe(p,angle,q=>_currentRoom.IsSolidForEnemyMovement(q,holesAreWalls:true)).Bitset;
                    if(terrain==0)return bits==0;
                    if(terrain==1)return bits!=0&&((bits&12)==0||(bits&3)==0);
                    // Cardinal source octants probe only their moving axis.
                    // Select geometric corners independently of that angle.
                    int nearby=0;for(int direction=0;direction<32;direction+=8)
                        nearby|=EnemyAdjacentWallResolver.Shared.Probe(p,direction,q=>_currentRoom.IsSolidForEnemyMovement(q,holesAreWalls:true)).Bitset;
                    return (nearby&12)!=0&&(nearby&3)!=0;
                }).OrderBy(p=>p.DistanceSquaredTo(initial)).ToArray();
                FailIf(candidates.Length==0,$"Stalfos fixture lacks original room terrain{terrain} for angle${angle:x2}.");initial=candidates[0];
            }
            if(fractional)initial+=new Vector2(.25f,.5f);
            var motion=new ObjectMotionRom();motion.Word(ObjectMotionRom.Object+10,(int)(initial.Y*256));motion.Word(ObjectMotionRom.Object+12,(int)(initial.X*256));
            Vector2 link=linkFloor.OrderBy(p=>p.DistanceSquaredTo(new(120,144))).First();
            if(form!=0)
            {
                bool found=false;
                foreach(var p in linkFloor.OrderBy(p=>p.DistanceSquaredTo(initial.Floor()+OracleObjectMovement.Shared.Direction(angle)*32)))
                {
                    if(Math.Abs(p.X-Mathf.Floor(initial.X))+Math.Abs(p.Y-Mathf.Floor(initial.Y)) is <24 or >=44)continue;
                    if(_collision.Collides(initial.Floor()*2-p))continue;
                    motion.Call(ObjectMotionRom.RelativeAngle,targetY:(int)p.Y,targetX:(int)p.X);
                    if(motion.Result!=angle)continue;link=p;found=true;break;
                }
                FailIf(!found,"Stalfos source-derived aim requires actual clear floor within the strict dodge distance.");
            }
            Vector2 lateLink=form==3?initial.Floor()+OracleObjectMath.StrictCardinalVector(angle)*(43+variant%3):initial.Floor()*2-link;
            if(form==3)FailIf(_collision.Collides(lateLink),"Stalfos strict distance fixture must remain on actual clear floor.");
            _inventory.SetScriptedEquippedItems(0,0);
            if(form is 1 or 3){_inventory.GiveTreasure(TreasureId.Sword,1);_inventory.EquipA(TreasureId.Sword);_inventory.ApplyDamage(1);}
            _player.WarpTo(link);int facing=((angle+(late?16:0)+4)&24)>>3;
            _player.Face(new Vector2I[]{Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left}[facing]);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var data=EnemyDatabase.Shared;var source=new RoomObjectRecord(4,0x13,0,RoomObjectKind.FixedEnemy,0x31,sub,2,1,88,120,0x57,0);
            var adapter=new RoomEnemyFactory(_entities,data,new RoomEntityResources(_rooms),_rooms)
                .CreateOrderedEnemy(data.EnemyHandlers.ResolveHandler(source),source,_currentRoom,initial,0,0,EnemyPlacementContext.Unrestricted)
                ??throw new InvalidOperationException("Missing Stalfos source handler.");
            var actor=(StalfosCharacter)adapter.Node;_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            int z=fractional?128:0;F1Set(actor,"_zFixed",z);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,facing,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[AgesWramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x31*4];native[0xcc0a]=0x9b;
            const int at=0xd180;native[at]=1;native[at+1]=0x31;native[at+2]=(byte)sub;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native.Word(at+14,z);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,writerAt=form==2?1:0;bool attack=false;
            var before=new PartMovementScratchProbe(()=>
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                if(late&&updates==writerAt){_player.SetScriptedCoordinateHigh(true,(int)lateLink.X);_player.SetScriptedCoordinateHigh(false,(int)lateLink.Y);}
            });_entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var sounds=_sound.AttachPlayRequestAudit();
            void Step(int count,bool pressed=false)
            {
                int edge=pressed?1:0;attack=pressed;
                StepGameplayUpdates(count,Vector2.Zero,attack?["attack"]:[],pressed?["attack"]:[],batched,()=>
                {
                    for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                    int oldState=native[at+4],oldAngle=native[at+9],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                    native.UpdateGameplay(edge,attack?1:0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];},beforeEnemies:()=>
                    {if(late&&updates==writerAt){native[0xd00d]=(byte)lateLink.X;native[0xd00b]=(byte)lateLink.Y;}});updates++;edge=0;
                    if(native.Word(at+12)!=oldX||native.Word(at+10)!=oldY)moves++;
                    if(oldState==9&&native[at+9]!=oldAngle)turns++;
                    if(oldState!=11&&native[at+4]==11)jumps++;
                    if(oldState==11&&native[at+4]==8)landings++;
                    bool eligible=native[0xcba0]==0&&(native[0xcc8a]&0x84)==0&&native[0xcd00]!=8;
                    if(eligible&&(native[0xcc5f]&0xf0)!=0)starts++;
                    FailIf((int)actor.State!=native[at+4]||actor.Counter1!=native[at+6]||actor.Angle!=native[at+9]||actor.Health!=native[at+0x29]||
                        actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||(actor.ZFixed&65535)!=native.Word(at+14)||
                        (actor.SpeedZ&65535)!=native.Word(at+0x14)||actor.CollisionEnabled!=((native[at+0x24]&128)!=0)||
                        actor.Animation.CurrentParameter!=native[at+0x21]||SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[at+0x20]||
                        eligible&&_player.StartedItemAnimationThisUpdate!=((native[0xcc5f]&0xf0)!=0)||
                        _player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f)||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                        $"Stalfos/form{form}/variant{variant}/fraction{fractional}/host{batched}/update{updates}: state{actor.State}/{native[at+4]}, C{actor.Counter1}/{native[at+6]}, angle{actor.Angle}/{native[at+9]}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{actor.ZFixed}/{native.Word(at+14)}, clock{SomariaPrivate<int>(actor.Animation,"_frameCounter")}/{native[at+0x20]}, start{_player.StartedItemAnimationThisUpdate}/${native[0xcc5f]:x2}, CEC[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                    FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls)||
                        !sounds.Requests.SequenceEqual(native.Sounds),$"Stalfos form{form}/update{updates}: RNG/cues differ.");observedCapture.Samples=0;
                });
            }
            if(form==0)
            {
                Step(1);F1Set(actor,"_state",StalfosState.Walking);F1Set(actor,"_counter1",1);F1Set(actor,"_angle",angle);
                actor.Animation.SetAnimation(0);native[at+4]=9;native[at+6]=1;native[at+9]=(byte)angle;native.SetEnemyAnimation(1,0);
                Step(1);FailIf(native[at+4]!=8||native.Word(at+12)==(int)(initial.X*256)&&native.Word(at+10)==(int)(initial.Y*256),
                    "Final walking visit must move before completing state08.");
                Step(2);
                if(terrain==0&&(angle&7)==0)
                {
                    F1Set(actor,"_state",StalfosState.Walking);F1Set(actor,"_counter1",0);native[at+4]=9;native[at+6]=0;
                    Step(1);zeroWraps++;FailIf(native[at+6]!=255,"Original zero counter must wrap toff, not saturate.");Step(255);
                    FailIf(native[at+4]!=8,"Wrapped walking counter must complete after256 moving visits.");
                }
            }
            else if(form==2)
            {
                Step(1);var current=_random.CaptureState();var trial=new OracleRandom();bool found=false;
                for(int value=0x100;value<65536;value++)
                {
                    var candidate=current with{Rng1=(byte)value,Rng2=(byte)(value>>8)};trial.RestoreState(candidate);trial.Next();
                    if((trial.Next().Low&15)!=1)continue;_random.RestoreState(candidate);native[0xff94]=candidate.Rng1;native[0xff95]=candidate.Rng2;found=true;break;
                }
                FailIf(!found,"Stalfos aimed walk requires a source random low-nibble01 branch.");Step(1);aims++;
                FailIf(native[at+9]!=angle,"Original aimed random walk must use the cached Link target.");
            }
            else
            {
                Step(1,pressed:true);
                bool dodge=form==1||variant%3==0;
                FailIf((native[at+4]==11)!=dodge,"Original dodge gate must use live high-byte Manhattan distance strictly below$2c.");
                if(!dodge){Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);continue;}
            }
            _dialogue.ShowMessage("Stalfos hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(form is 1 or 3)
            {
                while(actor.State==StalfosState.Jumping&&updates<60)Step(1);
                FailIf(actor.State!=StalfosState.Deciding,"Actual item-start dodge must land before another action.");
                // A subsequent fresh swing must publish another real parent pulse.
                _player.WarpTo(link);native.Word(0xd00a,(int)link.Y*256);native.Word(0xd00c,(int)link.X*256);
                actor.Position=initial;native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
                Step(1,pressed:true);FailIf(native[at+4]!=11,"A completed dodge must accept a second actual item start.");
                while(actor.State==StalfosState.Jumping&&updates<110)Step(1);
                FailIf(actor.State!=StalfosState.Deciding,"Repeated dodge must land and release its collision gate.");
            }
            Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(moves==0||forms.Contains(0)&&(turns==0||zeroWraps!=16||aims!=128)||forms.Contains(1)&&(jumps!=544||landings!=544||starts<544),
            "Stalfos matrix must execute terrain turns, all zero/aim observedCapture.Samples or both actual item-start/landing cycles.");
    }
}
