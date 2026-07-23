using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum BossState
{
    WaitingForDoors,
    Falling,
    Active,
    PreparingStomp,
    Stomping,
    StompLanded,
    Firing,
    HeadExposed,
    Regenerating,
    Dying,
    Dead
}
