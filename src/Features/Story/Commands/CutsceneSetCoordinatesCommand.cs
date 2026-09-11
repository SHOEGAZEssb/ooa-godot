namespace oracleofages;

internal sealed record CutsceneSetCoordinatesCommand(
    CutsceneCommandSource Source, string Actor, int Y, int X)
    : CutsceneCommand(Source)
{
    public CutsceneActorId ActorId { get; } = new(Actor);
}
