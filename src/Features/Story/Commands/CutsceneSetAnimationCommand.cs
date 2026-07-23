using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneSetAnimationCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Animation,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
