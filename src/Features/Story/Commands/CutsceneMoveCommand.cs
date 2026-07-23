using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Cardinal NPC movement opcodes set angle, select the corresponding
/// animation, and install counter2 in one script update.
/// </summary>
internal sealed record CutsceneMoveCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Angle,
    int Counter,
    string EncodedAnimation)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
