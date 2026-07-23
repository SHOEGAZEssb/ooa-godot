using Godot;
using System;

namespace oracleofages;

/// <summary>An asm helper animation change that carries into the next command.</summary>
internal sealed record CutsceneSetAnimationContinueCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Animation,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
