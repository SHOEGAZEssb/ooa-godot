using Godot;
using System.Collections.Generic;

namespace oracleofages;

// PART_JABU_JABUS_BUBBLES $16: native animation is selected once.
internal sealed partial class JabuBubbleRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRandom _random;
    private Vector2 _position;
    private int _state, _counter, _counter2, _speed, _drift;
    public Node2D Node => this;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal JabuBubbleRoomEntity(Vector2 position,int subid,JabuExteriorDatabase data,OracleRandom random)
    {
        _random=random; _position=position;
        InitializeVisual(data.Bubble,position,subid,positionedOam:true);
        ZIndex=ObjectDrawPriority.BehindLinkZIndex; Visible=false;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_state==0)
        {
            _drift=Position.X<0x50?-0x80:0x80;
            int value=_random.Next().Value;
            _counter=value&7; _counter2=(value&0xe0)>>3;
            _speed=new[]{0x1e,0x28,0x32,0x3c}[(value&0x18)>>3];
            _state=1; Visible=true; return;
        }
        if(_state==1)
        {
            if(_counter!=0)--_counter;
            if(_counter==0){_counter=_counter2;_state=2;}
            Visible=!Visible; return;
        }
        if(_state==2)
        {
            if(_counter!=0)--_counter;
            if(_counter==0)_state=3;
            _position.X=(_position.X+_drift/256f+256)%256;
        }
        Position=OracleObjectMovement.Shared.ApplySpeed(ref _position,_speed,ObjectAngle.Up);
        if(((int)Position.Y&255)>=0xf0){Finished=true;Visible=false;return;}
        Visible=!Visible;
        _counter2=(_counter2-1)&255;
        // partCode16 tests A from visible XOR $80, rather than counter2.
        // objectSetVisible82 leaves low bits $02, so the slowdown is unreachable.
        QueueRedraw();
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
}
