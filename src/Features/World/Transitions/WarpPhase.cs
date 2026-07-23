using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum WarpPhase
{
    None,
    FadeOut,
    LeaveScreen,
    FadeIn,
    TimeWarpInitialize,
    TimeWarpDissolve,
    TimeWarpSetup,
    TimeWarpSourceEffect,
    TimeWarpSourceTrail,
    TimeWarpBlackFadeIn,
    TimeWarpWhiteFadeOut,
    TimeWarpArrivalFadeIn,
    TimeWarpArrivalWait,
    TimeWarpArrivalEffect,
    TimeWarpArrivalFlicker
}
