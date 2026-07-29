namespace oracleofages;
internal sealed class FakeOctorokState(FakeOctorokRecord record, NpcCharacter actor)
{
    public FakeOctorokRecord Record { get; } = record;
    public NpcCharacter Actor { get; } = actor;
    public OracleObjectPosition Position { get; set; } =
        OracleObjectMovement.Shared.PositionFromPixels(actor.Position);
    public FakeOctorokStage Stage { get; set; }
    public int Counter { get; set; }
}
