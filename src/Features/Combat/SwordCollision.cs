using System;

namespace oracleofages;

internal static class SwordCollision
{
    // items/sword.s:@swordLevelData and @state4; swordParent.s:@state6.
    // Spin writes ITEMCOLLISION_SWORDSPIN ($08) at every sword level.
    // Eligibility (including the collision-disabled poke) belongs to the caller.
    internal static int Type(SwordActionState state, int level) => state switch
    {
        SwordActionState.Spin => 0x08,
        SwordActionState.Held or SwordActionState.Charged => 0x09,
        _ => Math.Clamp(level, 1, 3) + 0x03
    };
}
