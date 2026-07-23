using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneOrRoomFlagCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);
