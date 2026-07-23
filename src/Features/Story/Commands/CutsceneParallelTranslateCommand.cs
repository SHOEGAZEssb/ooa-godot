using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Two independently timed actor displacements dispatched in stable actor
/// order. The command completes after the longer lane has completed.
/// </summary>
internal sealed record CutsceneParallelTranslateCommand(
    CutsceneCommandSource Source,
    string Actor,
    Vector2 Delta,
    int Frames,
    string Actor2,
    Vector2 Delta2,
    int Frames2)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
    public CutsceneActorId Actor2Id { get; } = new(Actor2);
}
