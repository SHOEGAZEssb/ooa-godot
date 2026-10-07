using System.Collections.Generic;

namespace oracleofages;

internal sealed record GiantBladeTrapBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> Speeds,
    IReadOnlyList<EnemyBehaviorValue> ProbeOffsets,
    IReadOnlyList<EnemyBehaviorValue> CollisionEffects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions)
{
    internal int AccelerationUpdates => State[0].Value;
    internal int TurnWaitUpdates => State[1].Value;
    internal int InitialAngle => State[2].Value;
    internal int SwordInvincibility => State[3].Value;
    internal int InitialSpeed => State[4].Value;
}
