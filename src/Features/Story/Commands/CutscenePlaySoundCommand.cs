using Godot;
using System;

namespace oracleofages;

internal sealed record CutscenePlaySoundCommand(
    CutsceneCommandSource Source,
    int Sound)
    : CutsceneCommand(Source);
