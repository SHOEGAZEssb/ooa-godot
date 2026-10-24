using Godot;
using System.Collections.Generic;

namespace oracleofages;

// common/interactions/linkShip.s:$d4. High subid bits phase the seagulls;
// their poses are static, while the ship uses the shared frame counter.
internal sealed class CreditsShipRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,
    EndingScreen screen,int subid,Vector2 position) : IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal int State {get;private set;}
    internal int Subid {get;private set;}=subid;
    internal int Counter {get;private set;}
    internal int Z {get;private set;}
    internal Vector2 Position=>position;
    public Node2D Node {get;}=new(){Visible=false};
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    internal void Detach()=>screen.RemoveObjectOam(this);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;Counter=(Subid>>4)*4;Subid&=15;}
        else if(Subid!=2)
        {
            Counter=(Counter-1)&255;
            int clock=Subid==0?context.Entities.FrameCounter:Counter;
            if((clock&(Subid==0?31:7))==0)Z=(sbyte)data.Table("ship-z")[(clock>>(Subid==0?5:3))&7];
        }
        screen.SetObjectOam(this,data.NativeFrames(0xd4,Subid)[0],((int)position.Y+Z)&255,(int)position.X,
            context.Entities.NativeInteractionSlot(this),project:context.Transitions.WorldToScreen);
    }
}
