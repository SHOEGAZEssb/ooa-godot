using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetGlobalFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);
