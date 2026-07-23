using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Exposes the low-bit shutter completion represented by wcc93. Native bosses
/// wait for every enemy shutter to finish closing before starting their intro.
/// </summary>
internal interface IBossShutterState
{
    bool BossIntroReady { get; }
}
