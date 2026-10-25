using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface ISeedHittableRoomEntity
{
    SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns);
}
