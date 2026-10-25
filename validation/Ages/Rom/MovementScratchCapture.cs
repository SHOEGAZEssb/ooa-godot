namespace Oracles;

// Observes the four WRAM movement bytes at the caller's existing phase probe.
// Expected bytes still come from an independently advanced native reference.
internal sealed class MovementScratchCapture(OracleRuntimeState memory)
{
    internal byte[] Bytes { get; } = new byte[4];
    internal int Samples { get; set; }

    internal void Capture()
    {
        Samples++;
        for (int index = 0; index < Bytes.Length; index++)
            Bytes[index] = memory.ReadWramByte(0xcec0 + index);
    }
}
