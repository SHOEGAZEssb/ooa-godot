using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneEndCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
