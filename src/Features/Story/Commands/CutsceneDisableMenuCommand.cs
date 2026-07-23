using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneDisableMenuCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
