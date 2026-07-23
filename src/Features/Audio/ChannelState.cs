using Godot;
using System;

namespace oracleofages;
internal sealed class ChannelState
{
    public int Index { get; }
    public bool Active { get; private set; }
    public int Priority { get; private set; }
    public int Bank { get; private set; }
    public int Offset { get; set; }
    public int WaitFrames { get; set; }
    public int Volume { get; set; } = 8;
    public int OutputVolume { get; set; } = 8;
    public int DutyOrWaveform { get; set; }
    public int Envelope { get; set; }
    public int EnvelopeParameter { get; set; }
    public int EnvelopeDirection { get; set; }
    public int EnvelopePeriod { get; set; }
    public int EnvelopeCounter { get; set; }
    public int EnvelopeAttackTarget { get; set; } = -1;
    public int EnvelopeAttackFrames { get; set; }
    public int EnvelopeStage { get; set; }
    public double EnvelopeClock { get; set; }
    public int RawEnvelope { get; set; }
    public bool RawFrequencyMode { get; set; }
    public bool SkipContinuousDriverUpdates { get; set; }
    public int PitchShift { get; set; }
    public int PitchSlide { get; set; }
    public int Vibrato { get; set; }
    public int VibratoDelay { get; set; }
    public int VibratoPhase { get; set; }
    public int BaseFrequencyRegister { get; set; }
    public int CurrentFrequencyRegister { get; set; }
    public int NoiseNote { get; set; }
    public int NoiseEnvelope { get; set; }
    public int NoiseRegister { get; set; }
    public int NoiseLfsr { get; set; } = 0x7fff;
    public bool NoiseTriggerPending { get; set; }
    public bool Gate { get; set; }
    public double Phase { get; set; }

    public ChannelState(int index) => Index = index;
    public void Start(int priority, int bank, int offset)
    {
        Active = true;
        Priority = priority;
        Bank = bank;
        Offset = offset;
        WaitFrames = 0;
        Volume = 8;
        OutputVolume = 8;
        DutyOrWaveform = 0;
        Envelope = 0;
        EnvelopeParameter = 0;
        EnvelopeDirection = 0;
        EnvelopePeriod = 0;
        EnvelopeCounter = 0;
        EnvelopeAttackTarget = -1;
        EnvelopeAttackFrames = 0;
        EnvelopeStage = 0;
        EnvelopeClock = 0;
        RawEnvelope = 0;
        RawFrequencyMode = false;
        SkipContinuousDriverUpdates = false;
        PitchShift = 0;
        PitchSlide = 0;
        Vibrato = 0;
        VibratoDelay = 0;
        VibratoPhase = 0;
        NoiseTriggerPending = false;
        Gate = false;
    }

    public void Stop()
    {
        Active = false;
        Priority = 0;
        Gate = false;
        WaitFrames = 0;
    }
}
