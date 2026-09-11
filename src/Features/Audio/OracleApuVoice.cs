namespace oracleofages;

/// <summary>One physical CGB channel, independent of the eight driver programs.</summary>
internal sealed class OracleApuVoice
{
    internal bool Enabled;
    internal bool Clocked;
    internal bool DutyStarted;
    internal bool DacEnabled;
    internal bool LengthEnabled;
    internal int Length;
    internal int Timer = 8;
    internal int Position;
    internal int Sample;
    internal int Volume;
    internal int EnvelopeCounter;
    internal bool EnvelopeRunning;
    internal int Lfsr;
}
