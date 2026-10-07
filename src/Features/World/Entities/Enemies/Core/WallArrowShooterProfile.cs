using System.Collections.Generic;

namespace oracleofages;

internal sealed record WallArrowShooterProfile(
    IReadOnlyList<EnemyBehaviorValue> PartData,
    IReadOnlyList<EnemyBehaviorValue> ArrowPartData,
    IReadOnlyList<EnemyBehaviorValue> State,
    IReadOnlyList<EnemyBehaviorPair> SpawnOffsets,
    IReadOnlyList<EnemyBehaviorPair> BoundaryOffsets)
{
    internal int Interval => State[0].Value;
    internal int AlignmentRadius => State[1].Value;
    internal int AlignmentWidth => State[2].Value;
    internal int LaunchDelay => State[3].Value;
    internal Godot.Vector2 ArrowRadii => new(ArrowPartData[2].Value & 15, ArrowPartData[2].Value >> 4);
}
