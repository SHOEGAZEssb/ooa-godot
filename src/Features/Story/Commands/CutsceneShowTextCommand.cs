using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneShowTextCommand(
    CutsceneCommandSource Source,
    int TextId,
    string Message)
    : CutsceneCommand(Source);
