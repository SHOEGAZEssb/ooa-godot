using System.Collections.Generic;

namespace oracleofages;

internal sealed record BariBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> ShockCounters,
    IReadOnlyList<EnemyBehaviorValue> ZValues,
    IReadOnlyList<EnemyBehaviorValue> NormalEffects,
    IReadOnlyList<EnemyBehaviorValue> ElectricEffects,
    IReadOnlyList<EnemyBehaviorValue> ActiveCollisions)
{
    internal int LargeSpeed => State[0].Value;
    internal int SmallSpeed => State[1].Value;
    internal int InitialCounter => State[2].Value;
    internal int ShockUpdates => State[3].Value;
    internal int SplitUpdates => State[4].Value;
    internal int LargeCounterMask => State[5].Value;
    internal int LargeCounterOffset => State[6].Value;
    internal int SmallCounterMask => State[7].Value;
    internal int SmallCounterOffset => State[8].Value;
    internal int BobMask => State[9].Value;
    internal int BobShift => State[10].Value;
    internal int NormalMode => State[11].Value;
    internal int ElectricMode => State[12].Value;
    internal int SplitMinimumCollision => State[13].Value;
    internal int ChildOffset => State[14].Value;
    internal int ElectricCollision => State[15].Value;
}
