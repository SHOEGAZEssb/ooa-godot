using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneGateCommand(
    CutsceneCommandSource Source,
    string Gate)
    : CutsceneCommand(Source);
