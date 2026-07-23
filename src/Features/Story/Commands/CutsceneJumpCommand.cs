using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Typed expansion of the shared jumpAndWaitUntilLanded subscript. The first
/// update retains the callscript boundary; gravity begins on the next update.
/// </summary>
internal sealed record CutsceneJumpCommand(
    CutsceneCommandSource Source,
    string Actor,
    int InitialSpeedZ,
    int Gravity,
    int Sound)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
