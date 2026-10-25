using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC $dc:$14 attempts its child once, retaining its own slot until after allocation.
internal sealed partial class MakuPortalDecorationRoomEntity(DungeonObjectRecord record, OracleRoomData room,
    Action createDecoration) : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch()
    {
        if (Finished) return;
        if (room.GetMetatile(record.Position) == 0xdc) createDecoration();
        Finished = true;
    }
}
