using System.Collections.Generic;

namespace oracleofages;

internal sealed record VireProjectileProfile(
    IReadOnlyList<EnemyBehaviorValue> PartData,
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> Speeds,
    IReadOnlyList<EnemyBehaviorValue> SplitAngles,
    IReadOnlyList<EnemyBehaviorValue> Effects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions,
    IReadOnlyList<EnemyBehaviorPair> BoundaryOffsets)
{
    internal int HomingLifetime => State[0].Value;
    internal int HomingTurnInterval => State[1].Value;
    internal int DoubleAngleOffset => State[2].Value;
    internal int ArrivalRadius => State[3].Value;
    internal int SplitChildren => State[4].Value;
    internal int PrimarySplitAngle => State[5].Value;
    internal int Speed(int family, int health) => Speeds[family * 3 +
        (health >= State[6].Value ? 0 : health >= State[7].Value ? 1 : 2)].Value;
}
