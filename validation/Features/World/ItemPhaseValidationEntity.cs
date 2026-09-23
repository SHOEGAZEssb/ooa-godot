using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Observes the actual manager's enemy phase without adding runtime tracing.
internal sealed class ItemPhaseValidationEntity(Action observe)
    : RoomEntityAdapter<Node2D>(new Node2D(), _ => { }), IFixedRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => observe();
}
