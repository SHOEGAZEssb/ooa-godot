using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneWriteObjectByteCommand(
    CutsceneCommandSource Source,
    string Actor,
    int Address,
    int Value)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
