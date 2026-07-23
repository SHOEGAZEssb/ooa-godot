using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneTextOptionBranchCommand(
    CutsceneCommandSource Source,
    int Value,
    int TargetCommand)
    : CutsceneCommand(Source);
