using System.Collections.Generic;

namespace oracleofages;

internal sealed record BubbleBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> CollisionEffects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions)
{
    internal int Speed => State[0].Value;
    internal int AngleMask => State[1].Value;
    internal int CenterMask => State[2].Value;
    internal int TurnChanceMask => State[3].Value;
    internal int JinxUpdates => State[4].Value;
    internal int ContactInvincibility => State[5].Value;
    internal int ContactKnockback => State[6].Value;
}
