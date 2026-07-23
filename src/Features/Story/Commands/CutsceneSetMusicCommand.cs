using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetMusicCommand(
    CutsceneCommandSource Source,
    int Music)
    : CutsceneCommand(Source);
