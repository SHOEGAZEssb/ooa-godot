namespace oracleofages;

/// <summary>A live view of a logical channel in the sound driver's WRAM/HRAM.</summary>
internal sealed class ChannelState(int index, OracleSoundData data, OracleSoundDriver driver)
{
    internal int Index => index;
    internal bool Active => Priority != 0;
    internal int Priority => driver.ReadState(0xc06d + index);
    internal int Bank => driver.ReadState(0xffda + index);
    internal int Offset => data.PointerOffset(Bank, driver.ReadStateWord(0xffe2 + index * 2));
    internal int WaitFrames => driver.ReadState(0xc075 + index);
    internal int Volume => driver.ReadState(0xc07d + index);
    internal int DutyOrWaveform => index < 4 ? RawDuty >> 6 : RawDuty;
    internal int RawDuty => driver.ReadState(0xc057 + index);
    internal int Envelope => driver.ReadState(0xc065 + index);
    internal int EnvelopeParameter => driver.ReadState(0xc069 + index);
    internal int EnvelopeStage => driver.ReadState(0xc05d + index);
    internal int EnvelopeAttackFrames => driver.ReadState(0xc061 + index);
    internal int PitchShift => (sbyte)driver.ReadState(0xc033 + index);
    internal int PitchSlide => (sbyte)driver.ReadState(0xc03f + index);
    internal bool RawFrequencyMode => driver.ReadState(0xc039 + index) != 0;
    internal bool SkipContinuousDriverUpdates => (driver.ReadState(0xc039 + index) & 0x40) != 0;
    internal int BaseFrequencyRegister => driver.ReadStateWord(0xfff2 + index * 2);
    internal bool WaveRest => driver.ReadState(0xc02d + index) != 0;
}
