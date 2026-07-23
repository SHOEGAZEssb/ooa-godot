using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Fixed-update orchestration wait whose first dispatch consumes frame one and
/// whose completion yields. This matches RoomEventTimeline's established
/// duration without changing the interaction-script wait opcode above.
/// </summary>
internal sealed record CutsceneWaitFramesCommand(
    CutsceneCommandSource Source,
    int Frames)
    : CutsceneCommand(Source);
