using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum ExposedPhase
{
    LaunchInit,
    Airborne,
    Grabbable,
    GhostFleeInit,
    GhostFlee,
    GhostWaitInit,
    GhostWait,
    GhostRoam,
    GhostSeek
}
