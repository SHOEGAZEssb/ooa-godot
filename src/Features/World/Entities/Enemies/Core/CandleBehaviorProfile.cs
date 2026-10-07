using System.Collections.Generic;

namespace oracleofages;

internal sealed record CandleBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> Effects,
    IReadOnlyList<EnemyBehaviorValue> ExplosionEffects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions,
    IReadOnlyList<EnemyBehaviorValue> FlameData)
{
    internal int Wait => State[0].Value;
    internal int Walk => State[1].Value;
    internal int Burn => State[2].Value;
    internal int Flicker => State[3].Value;
    internal int WalkSpeed => State[4].Value;
    internal int BurnSpeed => State[5].Value;
    internal int FastSpeed => State[6].Value;
    internal int AngleMask => State[7].Value;
    internal int AngleOffset => State[8].Value;
    internal int ExplosionMode => State[9].Value;
    internal int ExplosionRadius => State[10].Value;
    internal int FlameZ => State[11].Value;
}
