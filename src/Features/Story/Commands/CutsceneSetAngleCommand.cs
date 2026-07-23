using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetAngleCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Angle)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
