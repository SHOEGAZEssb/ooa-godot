using System.Collections.Generic;

namespace oracleofages;

internal sealed class FireballShooterBehaviorProfile(
    IReadOnlyList<EnemyBehaviorValue> state,
    IReadOnlyList<EnemyBehaviorValue> timings)
{
    internal int LinkDistance => state[0].Value;
    internal int CooldownMask => state[1].Value;
    internal int CooldownBase => state[2].Value;
    internal int TileYOffset => state[3].Value;
    internal int TileXOffset => state[4].Value;
    internal int RoomClearSubId => state[5].Value;
    internal IReadOnlyList<EnemyBehaviorValue> TimingOffsets => timings;
}
