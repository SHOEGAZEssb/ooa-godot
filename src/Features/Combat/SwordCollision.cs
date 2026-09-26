using System;

namespace oracleofages;

internal static class SwordCollision
{
    private const int SwordLevelCollisionOffset = ItemCollisionType.L1Sword - 1;
    // items/sword.s:@swordLevelData and @state4; swordParent.s:@state6.
    // Spin writes ITEMCOLLISION_SWORDSPIN ($08) at every sword level.
    // Eligibility (including the collision-disabled poke) belongs to the caller.
    internal static int Type(SwordActionState state, int level) => state switch
    {
        SwordActionState.Spin => ItemCollisionType.SwordSpin,
        SwordActionState.Held or SwordActionState.Charged => ItemCollisionType.SwordHeld,
        _ => Math.Clamp(level, 1, 3) + SwordLevelCollisionOffset
    };
}
