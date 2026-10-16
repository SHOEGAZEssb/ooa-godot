using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $99; subids $01/$02 are checked child allocations in original order.
internal sealed class ExplosionWithDebrisRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _owner;
    private readonly ExplosionWithDebrisDatabase _data;
    private readonly byte[] _positions,_speeds;
    private readonly NpcCharacter _actor;
    private readonly int? _creatorDrawByte;
    private Vector2 _precise;
    private int _z,_speedZ,_consumedFrame=-1;
    internal int SubId {get;}
    internal int Var03 {get;}
    internal int State {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal Vector2 PrecisePosition=>_precise;
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    internal NpcCharacter Actor=>_actor;
    internal int AnimationParameter=>_consumedFrame==_actor.CurrentAnimationFrame?0:_actor.CurrentAnimationParameter;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal ExplosionWithDebrisRoomEntity(RoomEntityManager owner,ExplosionWithDebrisDatabase data,byte[] positions,byte[] speeds,
        int subid,int var03,Vector2 position,int z=0,int? creatorDrawByte=null)
    {
        _owner=owner;_data=data;_positions=positions;_speeds=speeds;SubId=subid;Var03=var03;_precise=position.Floor();_z=z<<8;
        if(subid==2&&creatorDrawByte is null)throw new NotSupportedException("explosionWithDebris.s:$99:$02 requires its creator's var38 draw byte.");
        _creatorDrawByte=creatorDrawByte;
        _actor=data.Actor(subid,position);
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_actor.ShowInitializedPresentation();
            if(SubId==2)
            {
                int visible=_creatorDrawByte!.Value&~0x40;
                _actor.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible));_actor.Visible=(visible&128)!=0;
            }
            if(SubId==0)
            {
                if(Var03!=0)
                {
                    int index=(_owner.Random.Next().Value&3)*2;
                    int y=(_positions[index]+(_owner.Random.Next().Value&7)-3)&255;
                    int x=(_positions[index+1]+(_owner.Random.Next().Value&7)-3)&255;
                    _precise=new(x,y);
                }
            }
            else
            {
                Angle=((Var03*8)+(_owner.Random.Next().Value&3)+2)&31;
                Speed=_speeds[_owner.Random.Next().Value&3];_speedZ=-0x180;
            }
        }
        else if(SubId==0)
        {
            _actor.AdvanceAnimationUpdates(1);int parameter=AnimationParameter;
            if(parameter==255)Finished=true;
            else if(parameter!=0)
            {
                _consumedFrame=_actor.CurrentAnimationFrame;
                for(int child=4;child!=0;child--)
                {
                    if(!_owner.InteractionSlotAvailable)break;
                    _owner.AddEntity(new ExplosionWithDebrisRoomEntity(_owner,_data,_positions,_speeds,1,child,_precise,_z>>8));
                }
            }
        }
        else
        {
            NativeObjectMovement.ApplySpeed(_owner.RuntimeState,ref _precise,Speed,Angle);
            if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x28))Finished=true;
        }
        _actor.Position=_precise.Floor();_actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
}
