using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum StoneStage
{
    None,
    Inert,
    Moved,
    WaitingForApproach,
    SpotJumpHold,
    SpotJumpAir,
    FirstLandingWait,
    FirstText,
    FirstTextPost,
    ApproachStone,
    AtStoneWait,
    SecondJumpHold,
    SecondJumpAir,
    SecondLandingWait,
    SignText,
    SignTextPost,
    LinkSelect,
    LinkFirstAxis,
    LinkAxisWait,
    LinkSecondAxis,
    LinkTargetWait,
    LinkFaceWait,
    PrePushScript,
    WaitingForPush,
    PushStarted,
    PostPushScript
}
