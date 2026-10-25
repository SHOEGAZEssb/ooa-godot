using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMoldormPublicationRom()
    {
        int bounces=0,reversals=0,moves=0;
        foreach(int angle in Enumerable.Range(0,32))
        foreach(bool boundary in new[]{false,true})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x91);_entities.Clear();
            bool Clear(Vector2 point)=>_currentRoom.GetTerrainInfo(point) is {Collision:0,Type:TerrainType.Normal,Hazard:HazardType.None}&&
                new[]{point+new Vector2(-1,5),point+new Vector2(1,5)}.All(p=>_currentRoom.GetTerrainInfo(p).Hazard==HazardType.None);
            var points=(from y in Enumerable.Range(2,_currentRoom.Height/4-4) from x in Enumerable.Range(2,_currentRoom.Width/4-4)
                let p=new Vector2(x*4,y*4) where Clear(p) select p).ToArray();
            bool Wall(Vector2I point)=>point.X<0||point.X>=_currentRoom.Width||point.Y<0||point.Y>=_currentRoom.Height||
                _currentRoom.IsSolidForEnemyMovement(point,holesAreWalls:true);
            var candidates=points.Where(p=>(EnemyAdjacentWallResolver.Shared.Probe(p,(angle+2)&31,Wall).Bitset!=0)==boundary).ToArray();
            FailIf(candidates.Length==0,$"Moldorm room4:91 has no clear-floor declared entry for angle${angle:x2}/boundary{boundary}.");
            Vector2 initial=candidates.OrderBy(p=>p.DistanceSquaredTo(new(120,80))).First();
            Vector2 link=points.OrderBy(p=>p.DistanceSquaredTo(new(120,112))).First();
            if(fractional)initial+=new Vector2(.25f,.5f);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(link);
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            var data=EnemyDatabase.Shared;var head=new MoldormCharacter();head.Initialize(data.ImportedEnemy(0x4f,1),_currentRoom,initial,_random);
            var source=data.EnemyHandlers.ResolveHandler(0x4f,0,"moldorm.s:head/tail publication").CombatSource(0,0,"moldorm.s:head/tail publication");
            var adapter=new MoldormRoomEntity(head,source with{SubId=1},_sound.PlaySound);_entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var tails=new MoldormTailCharacter[2];
            for(int index=0;index<2;index++)
            {
                var tail=new MoldormTailCharacter{Head=head};tail.Initialize(data.ImportedEnemy(0x4f,index+2),_currentRoom,initial.Floor(),_random,index==0?head:tails[0]);tails[index]=tail;
                if(index==0)head.Tail1=tail;else head.Tail2=tail;
                var child=new MoldormTailRoomEntity(tail,source with{SubId=index+2,ObjectFlags=0,KillableEnemyIndex=0},_sound.PlaySound);
                _entities.RegisterEnemySlot(child,index+2);_entities.AddEntity(child);
            }
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,(int)link.X,(int)link.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc8a]=1;native[AgesWramAddress.wWarpsDisabled]=1;
            native[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x4f*4];
            const int at=0xd180;
            for(int index=0;index<3;index++)
            {
                int slot=at+index*256;Vector2 p=index==0?initial:initial.Floor();native[slot]=1;native[slot+1]=0x4f;native[slot+2]=(byte)(index+1);
                native.Word(slot+10,(int)(p.Y*256));native.Word(slot+12,(int)(p.X*256));
                if(index!=0)native.Word(slot+0x16,slot-256);
            }
            native[at+0x30]=0xd2;native[at+0x31]=0xd3;
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var stage=new PartMovementScratchProbe(()=>{for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(stage,0);_entities.AddEntity(stage);
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldState=native[at+4],oldAngle=native[at+9],oldCounter=native[at+6],oldTurn=native[at+0x33],oldX=native.Word(at+12),oldY=native.Word(at+10);byte[] expected=new byte[4];
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,beforeParts:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(oldState==9&&oldTurn!=native[at+0x33])reversals++;
                int turnedAngle=oldCounter==1?(oldAngle+unchecked((sbyte)oldTurn))&31:oldAngle;
                if(oldState==9&&turnedAngle!=native[at+9])bounces++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10))moves++;
                Vector2 precise=SomariaPrivate<Vector2>(head,"_preciseHeadPosition");
                FailIf(head.State!=native[at+4]||precise!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    head.TurnCounter!=native[at+6]||head.Angle!=native[at+9]||head.State==9&&head.AngularSpeed!=unchecked((sbyte)native[at+0x33])||
                    head.AnimationParameter!=native[at+0x21]||SomariaPrivate<int>(head.Animation,"_frameCounter")!=native[at+0x20]||
                    observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Moldorm$4f:01/angle${angle:x2}/boundary{boundary}/fraction{fractional}/update{updates}: state{head.State}/{native[at+4]}, XY{precise}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{head.Angle}/{native[at+9]}, C{head.TurnCounter}/{native[at+6]}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                for(int index=0;index<2;index++)
                {
                    var tail=tails[index];int slot=at+(index+1)*256;
                    FailIf(tail.State!=native[slot+4]||tail.Position!=new Vector2(native.Word(slot+12)/256f,native.Word(slot+10)/256f)||
                        SomariaPrivate<int>(tail,"_index")!=native[slot+0x30]||SomariaPrivate<int>(tail,"_lastY")!=native[slot+0x31]||SomariaPrivate<int>(tail,"_lastX")!=native[slot+0x32]||
                        !SomariaPrivate<byte[]>(tail,"_offsets").SequenceEqual(Enumerable.Range(0,8).Select(i=>native[slot+0x33+i])),
                        $"Moldorm tail$4f:{index+2:x2}/angle${angle:x2}/fraction{fractional}/update{updates}: source delay buffer/retained coordinates differ: XY{tail.Position}/{native.Word(slot+12)/256f},{native.Word(slot+10)/256f}.");
                }
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "Moldorm common/head-angle/turn reversal RNG must preserve native ENEMY order.");observedCapture.Samples=0;
            });
            Step(2);
            F1Set(head,"_turnCounter",1);F1Set(head,"_angle",angle);F1Set(head,"_angularSpeed",2);
            native[at+6]=1;native[at+9]=(byte)angle;native[at+0x33]=2;
            int animation=((angle+2)&0x1c)>>2;head.Animation.SetAnimation(animation);native.SetEnemyAnimation(1,animation);native[at+0x32]=(byte)animation;
            if(fractional)
                for(int index=0;index<2;index++)
                {
                    tails[index].Position+=new Vector2(.75f,.25f);int slot=at+(index+1)*256;
                    native.Word(slot+10,native.Word(slot+10)|0x40);native.Word(slot+12,native.Word(slot+12)|0xc0);
                }
            var current=_random.CaptureState();var trial=new OracleRandom();bool found=false;
            for(int value=0x100;value<65536;value++)
            {
                var candidate=current with{Rng1=(byte)value,Rng2=(byte)(value>>8)};trial.RestoreState(candidate);
                if(((trial.Next().Value&15)==0)!=((angle&1)!=0))continue;
                _random.RestoreState(candidate);native[0xff94]=candidate.Rng1;native[0xff95]=candidate.Rng2;found=true;break;
            }
            FailIf(!found,"Moldorm turn fixture must cover source accepted and rejected reversal rolls.");
            Step(3);_dialogue.ShowMessage("Moldorm turn hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,5);native[0xcc8a]=5;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(32);
            _entities.Clear();_runtimeState.SetWramByte(0xcc8a,0);StepGameplayUpdates(2,Vector2.Zero);
        }
        FailIf(bounces==0||reversals==0||moves==0,"Moldorm comparisons must execute actual bounces, angular reversals and physical movement.");
    }
}
