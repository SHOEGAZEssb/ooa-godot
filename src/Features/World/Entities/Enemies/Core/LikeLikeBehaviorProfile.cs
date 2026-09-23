using System.Collections.Generic;

namespace oracleofages;

internal sealed class LikeLikeBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> state,
    IReadOnlyList<EnemyBehaviorValue> release,
    IReadOnlyList<EnemyBehaviorValue> collisions,
    IReadOnlyList<EnemyBehaviorValue> activeCollisions,
    IReadOnlyList<EnemyBehaviorValue> linkDamage,
    IReadOnlyList<EnemyBehaviorValue> enemyDamage)
{
    internal int SpeedRaw => state[0].Value;
    internal int AngleMask => state[1].Value;
    internal int DurationMask => state[2].Value;
    internal int DurationBase => state[3].Value;
    internal int HoldFrames => state[4].Value;
    internal int ShieldEscapePresses => state[5].Value;
    internal int CooldownFrames => state[6].Value;
    internal int ShieldLostText => state[7].Value;
    // Native signed counter byte: $94 advances towards zero, not down from148.
    internal int ReleaseInvincibility => unchecked((sbyte)release[0].Value);
    internal IReadOnlyList<EnemyBehaviorValue> CollisionEffects => collisions;
    internal IReadOnlyList<EnemyBehaviorValue> ActiveCollisions => activeCollisions;
    internal IReadOnlyList<EnemyBehaviorValue> LinkDamage => linkDamage;
    internal IReadOnlyList<EnemyBehaviorValue> EnemyDamage => enemyDamage;
}
