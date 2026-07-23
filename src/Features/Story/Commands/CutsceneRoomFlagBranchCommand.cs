using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneRoomFlagBranchCommand(
    CutsceneCommandSource Source,
    int Flag,
    int TargetCommand)
    : CutsceneCommand(Source);
