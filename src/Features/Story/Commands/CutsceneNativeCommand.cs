using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneNativeCommand(
    CutsceneCommandSource Source,
    string Handler)
    : CutsceneCommand(Source);
