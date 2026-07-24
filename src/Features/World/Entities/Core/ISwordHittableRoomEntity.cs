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
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns);
}

/// <summary>
/// Part collision mode $01 maps Link's sword collision types $04-$0b to
/// COLLISIONEFFECT_23. That effect kills the part so its next update grants
/// the drop without reporting enemy contact back to the sword.
/// </summary>
internal interface ILinkSwordCollectibleRoomEntity
{
    bool TryCollectWithSword(Rect2 hitbox);
}

internal enum EnemyKnockbackStrength
{
    None,
    Low,
    Normal,
    High
}
