using System.Collections.Generic;

namespace oracleofages;

internal sealed record FloormasterBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> HoverZ,
    IReadOnlyList<EnemyBehaviorValue> ChaseZ,
    IReadOnlyList<EnemyBehaviorValue> Effects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions,
    IReadOnlyList<EnemyBehaviorValue> LinkReturn)
{
    internal int InitialCounter => State[0].Value;
    internal int SpawnCounter => State[1].Value;
    internal int RetryCounter => State[2].Value;
    internal int MaximumLive => State[3].Value;
    internal int HoverCounter => State[4].Value;
    internal int ChaseCounter => State[5].Value;
    internal int NormalSpeed => State[6].Value;
    internal int FastSpeed => State[7].Value;
    internal int LinkMotionRadius => State[8].Value;
    internal int LinkMotionDiameter => State[9].Value;
    internal int InitialDistance => State[10].Value;
    internal int DistanceStep => State[11].Value;
    internal int MaximumY => State[12].Value;
    internal int MaximumXDifference => State[13].Value;
    internal int GrabZ => State[14].Value;
    internal int ReturnPosition => LinkReturn[4].Value;
    internal int ReturnTransition => LinkReturn[3].Value;
    internal int ReturnParameter => LinkReturn[5].Value;
}
