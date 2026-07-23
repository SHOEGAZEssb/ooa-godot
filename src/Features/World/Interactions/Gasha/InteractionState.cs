using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum InteractionState
{
    WaitingForPlant,
    NutReady,
    NutAirborne,
    AwaitingNutText,
    RewardHeld,
    Disappearing,
    Finished
}
