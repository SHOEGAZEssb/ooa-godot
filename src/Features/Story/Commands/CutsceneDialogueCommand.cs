using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Opens dialogue, blocks until it closes, then yields its completion update.
/// Used by imported multi-object orchestration whose controller owned that
/// extra command boundary.
/// </summary>
internal sealed record CutsceneDialogueCommand(
    CutsceneCommandSource Source,
    int TextId,
    string Message)
    : CutsceneCommand(Source);
