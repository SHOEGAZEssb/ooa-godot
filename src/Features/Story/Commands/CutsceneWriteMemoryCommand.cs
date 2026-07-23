using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneWriteMemoryCommand(
    CutsceneCommandSource Source,
    string Binding,
    int Value)
    : CutsceneCommand(Source);
