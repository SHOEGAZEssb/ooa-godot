using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// An interaction-phase observer captures inputs after physical object movement,
// before the actual gameplay collision pass. It owns no gameplay collision rules.
internal sealed class CollisionRomObserver(Action observe)
    : RoomEntityAdapter<Node2D>(new Node2D(), static _ => { }), IFixedRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => observe();
}
