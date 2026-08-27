using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Seed collision target whose source object has meaningful Z placement.
/// </summary>
internal interface ISeedHeightAwareHittableRoomEntity
{
    SeedHitResult ApplySeedHitAtHeight(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int sourceZ,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns);
}
