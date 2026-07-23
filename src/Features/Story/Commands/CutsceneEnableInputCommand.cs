using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneEnableInputCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
