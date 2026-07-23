using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneMemoryGateCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);
