using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneDisableInputCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
