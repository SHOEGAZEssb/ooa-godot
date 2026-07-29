namespace oracleofages;
internal sealed class FleeingAudience(
    NpcCharacter actor,
    FleeRecord record,
    OracleObjectPosition position)
{
    public NpcCharacter Actor { get; } = actor;
    public FleeRecord Record { get; } = record;
    public OracleObjectPosition Position { get; set; } = position;
    public int Delay { get; set; } = record.Delay;

    public int ZFixed;
    public int SpeedZ = record.WaitJumpSpeedZ;
    public bool Escaping { get; set; }
}
