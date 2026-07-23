using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum ImpaIntroEventStage
{
    None,
    LinkInitialize,
    LinkInitialWait,
    LinkHorizontal,
    LinkCenterWait,
    LinkApproach,
    WaitingForScript,
    Following
}
