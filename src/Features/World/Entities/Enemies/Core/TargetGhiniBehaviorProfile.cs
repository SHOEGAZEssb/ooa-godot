using System.Collections.Generic;

namespace oracleofages;

internal sealed record TargetGhiniBehaviorProfile(IReadOnlyList<EnemyBehaviorValue> Values,
    IReadOnlyList<EnemyBehaviorValue> Effects, IReadOnlyList<EnemyBehaviorValue> ActiveCollisions,
    IReadOnlyList<EnemyBehaviorValue> EmberStatus, IReadOnlyList<EnemyBehaviorValue> PegasusStatus)
{
    internal int InitialSpeed => Values[0].Value;
    internal int TargetSpeed => Values[1].Value;
    internal int SpeedStep => Values[2].Value;
    internal int AccelerationUpdates => Values[3].Value;
    internal int DecelerationUpdates => Values[4].Value;
    internal int SpeedStepMask => Values[5].Value;
    internal int TargetMask => Values[6].Value;
    internal int TargetYClamp => Values[7].Value;
    internal int TargetYOffset => Values[8].Value;
    internal int TargetXOffset => Values[9].Value;
    internal int InitialZ => Values[10].Value;
    internal int TargetDiameter => Values[11].Value;
}
