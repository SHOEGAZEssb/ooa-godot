using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Recognized native objectFlickerVisibility/dec-counter script loop. The
/// counter byte and frame mask remain explicit imported operands.
/// </summary>
internal sealed record CutsceneFlickerCommand(
    CutsceneCommandSource Source,
    string Actor,
    int CounterAddress,
    int FrameMask)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
