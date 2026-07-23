using Godot;
using System;

namespace oracleofages;

/// <summary>A native WRAM write that carries into the next operation.</summary>
internal sealed record CutsceneSetDisabledObjectsContinueCommand(
    CutsceneCommandSource Source,
    int Value)
    : CutsceneCommand(Source);
