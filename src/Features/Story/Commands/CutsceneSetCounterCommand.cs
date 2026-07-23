using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetCounterCommand(
    CutsceneCommandSource Source,
    int Frames)
    : CutsceneCommand(Source);
