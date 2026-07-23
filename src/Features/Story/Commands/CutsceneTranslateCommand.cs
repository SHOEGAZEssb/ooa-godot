using Godot;
using System;

namespace oracleofages;

/// <summary>Exact fixed-update displacement used by imported controller lanes.</summary>
internal sealed record CutsceneTranslateCommand(
    CutsceneCommandSource Source,
    string Actor,
    Vector2 Delta,
    int Frames,
    int Animation,
    bool SetAnimationOnStart)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
