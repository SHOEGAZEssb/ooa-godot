using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Species-specific rows from objectCollisionTable.s for active item
/// collisions that cannot be represented by the ordinary sword response.
/// </summary>
internal interface IItemCollisionHittableRoomEntity
{
    bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns);
}

internal enum RoomEntityItemCollision
{
    ThrownObject = 0x16,
    Bomb = 0x18,
    SwordBeam = 0x19
}
