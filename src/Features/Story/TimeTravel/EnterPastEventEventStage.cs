using Godot;
using System;

namespace oracleofages;
internal enum EnterPastEventEventStage
{
    Inactive,
    Begin,
    InstallIntroWait,
    IntroWait,
    PreJumpWait,
    BeginJump,
    Jump,
    InstallPostJumpWait,
    PostJumpWait,
    Dialogue,
    PostTextWait,
    StartFirstDown,
    FirstDown,
    Right,
    SecondDown,
    StartSlowDown,
    SlowDown,
    StartFinalDown,
    FinalDown
}
