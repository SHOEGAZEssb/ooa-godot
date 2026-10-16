using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Shared INTERAC_FALLING_ROCK $92:$00/$01/$02. The native spawner and its
// debris retain independent INTERACTION allocations and update eligibility.
internal sealed class FallingRockRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _owner;
    private readonly FallingRockDatabase _data;
    private readonly NpcCharacter? _actor;
    private Vector2 _position;
    internal int SubId {get;}
    internal int Var03 {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    private int _z,_speedZ;
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node {get;}
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0||SubId==2;
    public bool UpdatesDuringRoomEntityFreeze=>State==0||SubId==2;
    internal FallingRockRoomEntity(RoomEntityManager owner,FallingRockDatabase data,int subid,int var03,Vector2 position,int counter=0,int angle=0)
    {
        if(subid is <0 or >2)throw new NotSupportedException($"fallingRock.s: INTERAC $92:${subid:x2} native dispatch is not represented.");
        _owner=owner;_data=data;SubId=subid;Var03=var03;_position=position.Floor();Counter=counter;Angle=angle;
        _actor=subid==0?null:data.Actor(subid,position);Node=_actor is null?new Node2D():_actor;
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor?.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(SubId==0)
        {
            if(State==0){State=1;Counter2=1;}
            if(_owner.RuntimeState.ReadWramByte(0xcfdf)!=0){Finish();return;}
            Counter2=(Counter2-1)&255;
            if(Counter2==0){Counter2=20;_owner.TryCreateFallingRock(1,Var03,Vector2.Zero);}
            return;
        }
        if(State==0)
        {
            State=1;_actor!.ShowInitializedPresentation();
            if(SubId==1)
            {
                if(Var03!=0)_actor.SetBasePalette(4);
                var positions=_data.Bytes(Var03==0?"rock-positions-0":"rock-positions-1");int at=(_owner.Random.Next().Value&15)*2;
                _position=new(positions[at+1],positions[at]);_z=unchecked((sbyte)(-positions[at]-8))<<8;
            }
            else
            {
                int at=(Counter+(Var03==0?0:4))*4+Angle;var angles=_data.Bytes("rock-angles");
                if((uint)at>=angles.Length)throw new NotSupportedException($"fallingRock.s:$92:$02 angle offset ${at:x2} exceeds its source table (counter=${Counter:x2}, var03=${Var03:x2}).");
                Angle=angles[at];Speed=Var03==0?0x3c:0x28;_speedZ=Var03==0?-0xe8:-0xe4;Present();return;
            }
        }
        if(SubId==1)
        {
            if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x10))
            {
                HazardType hazard=_owner.ActiveRoom.GetTerrainInfo(_position.Floor()+new Vector2(0,5)).Hazard;
                if(hazard!=HazardType.None)throw new NotSupportedException($"fallingRock.s:$92:$01 objectReplaceWithAnimationIfOnHazard {hazard} in room ${_owner.ActiveRoom.Group:x1}:{_owner.ActiveRoom.Id:x2} is not represented.");
                _owner.OnSoundRequested(SoundId.SndBreakRock);int variant=_owner.Random.Next().Value&3;
                for(int angle=0;angle<4;angle++)if(!_owner.TryCreateFallingRock(2,0,_position,variant,angle))break;
                _owner.BeginScreenShake(4);Finish();return;
            }
        }
        else
        {
            if(_owner.RuntimeState.ReadWramByte(0xcfde)!=0||OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x18)){Finish();return;}
            _actor!.Position=NativeObjectMovement.ApplySpeed(_owner.RuntimeState,ref _position,Speed,Angle);
        }
        Present();
    }
    private void Present(){_actor!.Position=_position.Floor();_actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));}
    private void Finish(){Finished=true;Node.Visible=false;}
}
