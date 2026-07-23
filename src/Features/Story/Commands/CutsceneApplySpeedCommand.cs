using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneApplySpeedCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Counter)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
