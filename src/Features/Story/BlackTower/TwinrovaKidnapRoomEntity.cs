using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_TWINROVA_3 $bc. Each witch owns its native target-list cursor;
// completion rendezvous uses the shared $cfc6/$cfc0 bytes.
internal sealed class TwinrovaKidnapRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,int subid,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor=data.Actor(0xbc,subid,position);
    private Vector2 _position=position;
    private int _state,_substate,_counter,_target,_path,_direction=255,_angle,_z;
    internal NpcCharacter Actor=>_actor;
    internal int Substate=>_substate;
    internal int Target=>_target;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    private int Read(int address)=>context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    private void Animation(int animation)=>_actor.SetScriptAnimation(data.Animation(0xbc,animation));
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    private void Signal()
    {Write(0xcfc6,Read(0xcfc6)+1);if(Read(0xcfc6)==2){Write(0xcfc6,0);Write(0xcfc0,Read(0xcfc0)|1);}}
    private Vector2 TargetPosition()
    {var path=data.Table($"witch-path{_path}");return new(path[_target*2+1],path[_target*2]);}
    private void Move()
    {
        _angle=OracleObjectMovement.Shared.RelativeAngle(_position.Floor(),TargetPosition());
        _actor.Position=NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _position,0x50,_angle);
    }
    private bool Next()
    {
        var target=TargetPosition();
        if((((int)target.Y-(int)_position.Y+1)&255)>=5||(((int)target.X-(int)_position.X+1)&255)>=5)return false;
        _position=target;_actor.Position=target;
        if(++_target<data.Table($"witch-path{_path}").Length/2)return false;
        _target=0;return true;
    }
    private void Direction()
    {int direction=((_angle>>4)&1)^1;if(direction!=_direction){_direction=direction;Animation(direction);}}
    private void Float(int frame)
    {if((frame&7)==0)_z=unchecked((sbyte)(_z+data.Table("witch-z")[(frame&0x38)>>3]));}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_state==0)
        {
            _state=1;_actor.SetBasePalette(subid switch {0=>2,1=>1,2=>0,_=>1});
            _actor.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(0xc0));_actor.ShowInitializedPresentation();
            _z=subid<2?-5:subid==2?-16:-8;_path=subid<2?subid:2;
            if(subid>=2)Animation(subid==2?4:1);
        }
        bool animate=true;
        if(subid>=2&&_substate==0)
        {
            Float(frame.Counter);_actor.AdvanceAnimationUpdates(1);animate=false;
            if((Read(0xcfc0)&1)!=0){_substate=3;_direction=255;}
        }
        else switch(_substate)
        {
            case 0:
                Move();Direction();if(Next()){_substate=1;_counter=40;Animation(subid==0?0:1);}break;
            case 1:
                Float(frame.Counter);_actor.AdvanceAnimationUpdates(1);animate=false;
                if(--_counter==0){_substate=2;_counter=40;Signal();}break;
            case 2:
                Float(frame.Counter);_actor.AdvanceAnimationUpdates(1);animate=false;
                if((Read(0xcfc0)&1)==0&&--_counter==0){_substate=3;_direction=255;_path=subid+4;}break;
            case 3:
                Move();bool loop=Next();
                if(!loop){Move();if(subid!=2)Direction();loop=Next();}
                if(loop)
                {
                    Signal();animate=false;
                    if(subid>=2){Finished=true;_actor.SetActive(false);}else _substate=4;
                }
                break;
            case 4:break;
        }
        if(animate)_actor.AdvanceAnimationUpdates(1);
        _actor.SetScriptDrawOffset(new(0,_z));
    }
}
