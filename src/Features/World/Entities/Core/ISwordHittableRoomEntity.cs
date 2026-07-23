using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ISwordHittableRoomEntity
{
    bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns);
}
