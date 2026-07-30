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
/// Some collision effects write knockback to the attacking sword item. The
/// item's next update transfers this response to Link.
/// </summary>
internal interface ISwordAttackerKnockbackRoomEntity
{
    bool TryGetSwordAttackerKnockback(
        EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response);
}

/// <summary>
/// Collision tables that distinguish the active Link sword parent state can
/// consume this context before their ordinary sword-hit capability runs.
/// </summary>
internal interface ILinkSwordStateAwareRoomEntity
{
    void SetLinkSwordState(SwordActionState state, int swordLevel);
}

internal readonly record struct SwordAttackerKnockback(
    Vector2 SourcePosition,
    int Frames);

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
