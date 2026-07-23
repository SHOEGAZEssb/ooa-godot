using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetSpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Speed)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
