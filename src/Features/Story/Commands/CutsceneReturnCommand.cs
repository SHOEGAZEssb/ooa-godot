using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneReturnCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
