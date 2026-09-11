using System;

namespace oracleofages;

internal sealed class SoundValidationFixture(OracleSoundData data, bool output = false) : IDisposable
{
    internal OracleSoundEngine Sound { get; } = new(data, output);
    public void Dispose() => Sound.Free();
}
