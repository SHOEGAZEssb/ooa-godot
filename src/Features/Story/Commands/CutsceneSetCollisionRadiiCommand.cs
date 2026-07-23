using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetCollisionRadiiCommand(
    CutsceneCommandSource Source,
    string Actor,
    int RadiusY,
    int RadiusX)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
