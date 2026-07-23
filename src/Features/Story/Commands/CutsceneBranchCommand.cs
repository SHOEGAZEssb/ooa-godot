using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneBranchCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);
