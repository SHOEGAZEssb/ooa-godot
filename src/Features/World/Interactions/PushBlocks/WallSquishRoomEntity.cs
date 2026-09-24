using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class WallSquishRoomEntity(OracleRoomData room, Func<int> pushAngle,
    Func<int> raisedFloorOffset, string source) : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    internal int State { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        State = 1;
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (State == 0) { State = 1; return; }
        if (!LinkWallProbe.Shared.SurroundedByWalls(frame.Player.Position,
                (room.TilesetFlags & 0x20) != 0,
                raisedFloorOffset() != 0 ? room.IsSolidForRaisedFloorLink : room.IsSolid)) return;
        frame.Player.RequestWallSquish(pushAngle(), source);
    }
}
