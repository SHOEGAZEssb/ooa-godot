using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneMakeAButtonSensitiveCommand(
    CutsceneCommandSource Source,
    string Actor)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
