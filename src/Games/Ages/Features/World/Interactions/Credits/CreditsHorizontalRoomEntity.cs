using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_CREDITS_TEXT_HORIZONTAL $ae. Parent and children each occupy an
// ordinary interaction slot; later-slot children initialize in the same pass.
internal sealed class CreditsHorizontalRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly CreditsHorizontalDatabase _data;
    private readonly EndingScreen _screen;
    private int _pointer,_word,_direction,_animation,_y,_x,_travel;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int MaskState {get;private set;}
    internal int Counter {get;private set;}
    internal int NextAnimation=>_animation;
    internal int Wait=>_word;
    internal int Travel=>_travel;
    internal bool Child {get;}
    internal int SubId {get;}
    internal Vector2 Position=>new(_x,_y);
    public Node2D Node {get;}=new(){Visible=false};
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    internal CreditsHorizontalRoomEntity(RoomEventContext context,CreditsHorizontalDatabase data,EndingScreen screen,int subid,
        bool child=false,int counter=0,int animation=0,int y=0,int x=0)
    {_context=context;_data=data;_screen=screen;SubId=subid;Child=child;Counter=counter;_animation=animation;_y=y;_x=x;}
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;
            if(Child){_travel=20;_x=SubId==0?0xf0:0;Publish();}
            else
            {
                _pointer=_data.Entry(SubId).Offset;LoadRecord();
                var entry=_data.Entry(SubId);_direction=entry.Direction;_animation=entry.Animation;
            }
            return;
        }
        if(_context.Transitions.NativePaletteFadeActive)return;
        if(Child)
        {
            if(Substate==0)
            {
                _travel=(_travel-1)&255;
                if(_travel==0){Substate=1;_x=SubId==0?0xa0:0x50;}
                else
                {
                    // SPEED_200 twice, publishing the shared movement delta.
                    var position=new Vector2(_x,_y);
                    for(int index=0;index<2;index++)NativeObjectMovement.ApplySpeed(_context.Entities.RuntimeState,ref position,0x50,SubId==0?0x18:8);
                    _x=(int)position.X&255;_y=(int)position.Y&255;
                }
            }
            else if(Counter!=255&&(Counter=(Counter-1)&255)==0){Finished=true;_screen.RemoveObjectOam(this);return;}
            Publish();return;
        }
        switch(Substate)
        {
            case 0:
                if(_word!=0)_word--;
                if(_word!=0)return;
                SpawnAndLoad();CheckFlags();return;
            case 1:
                switch(MaskState)
                {
                    case 0:if(Decrement()){MaskState=1;}return;
                    case 1:
                        if((_context.Entities.FrameCounter&3)!=0)return;
                        if(Counter==16){MaskState=2;_pointer-=3;LoadRecord();Counter=30;}
                        else{_screen.MaskCreditsFontColumn(Counter,false);Counter=(Counter+1)&255;}
                        return;
                    case 2:if(Decrement()){MaskState=3;Counter=16;}return;
                    case 3:
                        if((_context.Entities.FrameCounter&3)!=0)return;
                        if(Decrement()){Substate=0;MaskState=0;CheckFlags();}
                        else _screen.MaskCreditsFontColumn(Counter,true);
                        return;
                    default:throw new NotSupportedException($"INTERAC $ae font-mask state ${MaskState:x2}.");
                }
            case 2:
                if(!Decrement())return;
                _context.Entities.RuntimeState.SetWramByte(0xcfdf,255);Finished=true;return;
            default:throw new NotSupportedException($"INTERAC $ae substate ${Substate:x2}.");
        }
    }
    private bool Decrement(){Counter=(Counter-1)&255;return Counter==0;}
    private void CheckFlags()
    {if((_word&0x80)!=0){Substate=(_word&0x40)==0?1:2;Counter=180;}}
    private void SpawnAndLoad()
    {
        if(_context.Entities.InteractionSlotAvailable)
            _context.Entities.AddEntity(new CreditsHorizontalRoomEntity(_context,_data,_screen,_direction,true,Counter,_animation,_y,_x));
        _animation=(_animation+1)&255;LoadRecord();
    }
    private void LoadRecord()
    {
        // Zero waits recursively spawn on the same update, before the parent
        // inspects the new low byte's $80/$40 control flags.
        for(int depth=0;depth<256;depth++)
        {
            _word=_data.Read(_pointer++)|(_data.Read(_pointer++)<<8);Counter=_data.Read(_pointer++);_y=_data.Read(_pointer++);
            if(_word!=0)return;
            if(_context.Entities.InteractionSlotAvailable)
                _context.Entities.AddEntity(new CreditsHorizontalRoomEntity(_context,_data,_screen,_direction,true,Counter,_animation,_y,_x));
            _animation=(_animation+1)&255;
        }
        throw new NotSupportedException("creditsTextHorizontal.s: unterminated zero-wait recursion.");
    }
    private void Publish()=>_screen.SetObjectOam(this,_data.Frame(_animation),_y+16,_x-0x10,_context.Entities.InteractionSlot(Node));
}
