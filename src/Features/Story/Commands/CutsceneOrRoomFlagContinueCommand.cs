using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native object-code room-flag mutation that remains in the same object
/// update. This is deliberately distinct from scriptCmd_orRoomFlags, whose
/// no-carry return yields the interaction script.
/// </summary>
internal sealed record CutsceneOrRoomFlagContinueCommand(
    CutsceneCommandSource Source,
    int Flag)
    : CutsceneCommand(Source);
