using Godot;
using System;

namespace oracleofages;
internal enum ChildStage
{
    Inactive,
    WaitForSignal,
    InitialWait,
    Jumping,
    InstallPostJumpWait,
    PostJumpWait,
    Dialogue,
    PostDialogueWait,
    RedFreezeWait,
    RedPostFirstWait,
    RedTurnLeftWait,
    RedPostSecondWait,
    RedTurnUpWait,
    RedFinalWait,
    Shaking,
    Fleeing,
    FleeEndPending,
    Finished
}
