using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum DoorState
{
    Initialize,
    WaitingForLinkClear,
    WatchingTrigger,
    ReadyToClose,
    ClosingInterleaved,
    WaitingForEnemies,
    SolveDelay,
    ReadyToOpen,
    OpeningInterleaved
}
