using System.Collections.Generic;

namespace oracleofages;

internal sealed record WaterTektiteBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorValue> Speeds)
{
    internal int SwimUpdates => State[0].Value;
    internal int RestUpdates => State[1].Value;
    internal int AngleMask => State[2].Value;
    internal int AngleOffset => State[3].Value;
    internal int FirstWaterTile => State[4].Value;
    internal int WaterTileCount => State[5].Value;
    internal int KnockbackSpeed => State[6].Value;
}
