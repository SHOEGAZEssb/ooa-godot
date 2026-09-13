namespace oracleofages;

/// <summary>A view of the authoritative wLever1/2PullDistance byte.</summary>
internal sealed class LeverState(OracleRuntimeState runtime, int address)
{
    internal int PullDistance
    {
        get => runtime.ReadWramByte(address);
        set => runtime.SetWramByte(address, (byte)value);
    }
}
