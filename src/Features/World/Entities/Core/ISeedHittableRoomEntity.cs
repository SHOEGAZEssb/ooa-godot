using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface ISeedHittableRoomEntity
{
    SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns);
}
