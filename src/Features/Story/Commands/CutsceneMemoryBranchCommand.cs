using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneMemoryBranchCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value,
    int TargetCommand)
    : CutsceneCommand(Source);
