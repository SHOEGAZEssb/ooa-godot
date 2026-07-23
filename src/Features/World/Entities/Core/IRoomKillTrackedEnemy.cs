using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Retains the source object's one-based enemy index from Enemy.enabled. The
/// original uses indices $01-$07 to suppress recently defeated placements.
/// </summary>
internal interface IRoomKillTrackedEnemy
{
    int KillableEnemyIndex { get; }
    bool MarksEnemyKilled { get; }
    bool CountsAsDefeat => true;
}
