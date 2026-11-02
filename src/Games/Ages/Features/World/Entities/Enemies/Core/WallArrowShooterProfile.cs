
namespace Oracles;

internal sealed record WallArrowShooterProfile(
    IReadOnlyList<EnemyBehaviorValue> PartData,
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorPair> SpawnOffsets)
{
    internal int Interval => State[0].Value;
    internal int AlignmentRadius => State[1].Value;
    internal int AlignmentWidth => State[2].Value;
}
