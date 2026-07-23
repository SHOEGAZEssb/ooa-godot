using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Contributes to the room's live wNumEnemies equivalent. Native puzzle
/// sentinels opt in alongside combat enemies.
/// </summary>
internal interface IRoomEnemyCounterEntity
{
    bool CountsAsEnemy { get; }
}
