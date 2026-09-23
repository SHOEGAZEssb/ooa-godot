using System.Collections.Generic;

namespace oracleofages;

internal sealed class BallChainBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> state,
    IReadOnlyList<EnemyBehaviorValue> part,
    IReadOnlyList<EnemyBehaviorValue> bodyEffects,
    IReadOnlyList<EnemyBehaviorValue> bodyMask,
    IReadOnlyList<EnemyBehaviorValue> ballEffects,
    IReadOnlyList<EnemyBehaviorValue> ballMask)
{
    internal int SpeedRaw => state[0].Value;
    internal int AttackDistance => state[1].Value;
    internal int WindupFrames => state[2].Value;
    internal int RequiredEnemySlots => state[3].Value;
    internal int OriginOffset => state[4].Value;
    internal int OrbitRadius => state[5].Value;
    internal int ReleaseRadius => state[6].Value;
    internal int ThrowRadius => state[7].Value;
    internal int ExtensionSpeed => state[8].Value;
    internal int ExtensionDeceleration => state[9].Value;
    internal int SlowRotation => state[10].Value;
    internal int FastRotation => state[11].Value;
    internal int AngleMask => state[12].Value;
    internal int ParentBlockInvincibility => unchecked((sbyte)state[13].Value);
    internal IReadOnlyList<EnemyBehaviorValue> PartData => part;
    internal IReadOnlyList<EnemyBehaviorValue> BodyEffects => bodyEffects;
    internal IReadOnlyList<EnemyBehaviorValue> BodyMask => bodyMask;
    internal IReadOnlyList<EnemyBehaviorValue> BallEffects => ballEffects;
    internal IReadOnlyList<EnemyBehaviorValue> BallMask => ballMask;
}
