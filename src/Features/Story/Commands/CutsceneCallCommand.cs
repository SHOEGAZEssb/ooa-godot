using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneCallCommand(
    CutsceneCommandSource Source,
    int TargetCommand)
    : CutsceneCommand(Source);
