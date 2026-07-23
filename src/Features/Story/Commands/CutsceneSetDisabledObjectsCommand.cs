using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetDisabledObjectsCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);
