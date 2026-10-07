using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Observes an object phase without adding runtime tracing. Register an enemy
// slot when the observation must precede parts/interactions.
internal sealed class ItemPhaseValidationEntity(Action observe)
    : RoomEntityAdapter<Node2D>(new Node2D(), _ => { }), IFixedRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => observe();
}
