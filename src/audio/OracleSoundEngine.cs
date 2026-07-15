using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Ports the Oracle sound sequencer's eight logical channels and renders their
/// square, wave, and noise voices through a Godot AudioStreamGenerator.
/// </summary>
public partial class OracleSoundEngine : Node
{
    public const int UpdatesPerSecond = 60;
    public const int SampleRate = 44100;
    private const double ApuClock = 4194304.0;

    public const int MusTitlescreen = 0x01;
    public const int MusOverworld = 0x03;
    public const int MusNayru = 0x08;
    public const int MusEssenceRoom = 0x0d;
    public const int MusFairyFountain = 0x0f;
    public const int MusFileSelect = 0x11;
    public const int MusRoomOfRites = 0x1d;
    public const int MusSadness = 0x1f;
    public const int MusDisaster = 0x21;
    public const int MusLadxSideview = 0x2f;
    public const int SndSelectItem = 0x56;
    public const int SndSwordSlash = 0x74;
    public const int SndMenuMove = 0x84;
    public const int SndCtrlStopMusic = 0xf0;
    public const int SndCtrlStopSfx = 0xf1;
    public const int SndCtrlDisable = 0xf5;
    public const int SndCtrlEnable = 0xf6;
    public const int SndCtrlFastFadeOut = 0xfa;
    public const int SndCtrlMediumFadeOut = 0xfb;

    private static readonly float[] SquareDuty = { 0.125f, 0.25f, 0.5f, 0.75f };
    private static readonly double CgbHighPassFactor =
        Math.Pow(0.998943, ApuClock / SampleRate);

    private readonly OracleSoundData _data;
    private readonly ChannelState[] _channels = new ChannelState[8];
    private readonly bool _enableOutput;
    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private double _updateTicks;
    private int _fadeDirection;
    private int _fadeSpeed;
    private int _fadeCounter;
    private int _masterVolume = 7;
    private int _musicVolume = 3;
    private double _highPassCapacitor;

    public int ActiveMusic { get; private set; }
    public bool Disabled { get; private set; }
    public int MusicVolume => _musicVolume;
    internal OracleSoundData Data => _data;
    internal ChannelState Channel(int channel) => _channels[channel];

    public OracleSoundEngine() : this(new OracleSoundData(), true) { }

    internal OracleSoundEngine(OracleSoundData data, bool enableOutput)
    {
        _data = data;
        _enableOutput = enableOutput;
        for (int channel = 0; channel < _channels.Length; channel++)
            _channels[channel] = new ChannelState(channel);
    }

    public override void _Ready()
    {
        if (!_enableOutput)
            return;
        var stream = new AudioStreamGenerator
        {
            MixRate = SampleRate,
            BufferLength = 0.1f
        };
        _player = new AudioStreamPlayer { Name = "OracleApu", Stream = stream };
        AddChild(_player);
        _player.Play();
        _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _Process(double delta)
    {
        _updateTicks += delta * UpdatesPerSecond;
        while (_updateTicks >= 1.0)
        {
            Tick();
            _updateTicks -= 1.0;
        }
        FillAudioBuffer();
    }

    public void PlayRoomMusic(int group, int room)
    {
        int music = _data.RoomMusic(group, room);
        PlayMusicIfChanged(music);
    }

    public void PlayMusicIfChanged(int music)
    {
        if (music != ActiveMusic)
            PlaySound(music == 0 ? SndCtrlStopMusic : music);
    }

    public void SetMusicVolume(int volume) => _musicVolume = Math.Clamp(volume, 0, 3);

    public void RestartSound()
    {
        // restartSound calls the driver's stopSound entry point directly,
        // terminating all eight channels without changing music volume.
        foreach (ChannelState channel in _channels)
            channel.Stop();
        ActiveMusic = 0;
    }

    public void PlaySound(int soundId)
    {
        if (soundId == 0)
            return;
        bool stoppingMusic = soundId == SndCtrlStopMusic;
        switch (soundId)
        {
            case SndCtrlStopMusic:
                soundId = 0xde;
                break;
            case SndCtrlStopSfx:
                StopChannels(2, 3, 5, 7);
                return;
            case SndCtrlDisable:
                Disabled = true;
                return;
            case SndCtrlEnable:
                Disabled = false;
                _masterVolume = 7;
                return;
            case 0xf7: BeginFade(+1, 3); return;
            case 0xf8: BeginFade(+1, 7); return;
            case 0xf9: BeginFade(+1, 15); return;
            case 0xfa: BeginFade(-1, 7); return;
            case 0xfb: BeginFade(-1, 15); return;
            case 0xfc: BeginFade(-1, 31); return;
        }
        if ((uint)soundId >= OracleSoundData.SoundCount)
            throw new ArgumentOutOfRangeException(nameof(soundId));

        _fadeDirection = 0;
        if (stoppingMusic)
            ActiveMusic = 0;
        if (soundId < OracleSoundData.MusicCount)
            ActiveMusic = soundId;
        foreach (OracleSoundData.ChannelStart start in _data.ChannelsFor(soundId))
        {
            ChannelState channel = _channels[start.Channel];
            if (start.Priority < channel.Priority)
                continue;
            channel.Start(start.Priority, start.Bank, start.Offset);
        }
        _masterVolume = 7;
    }

    internal void Tick()
    {
        if (Disabled)
            return;
        UpdateFade();
        for (int index = 0; index < _channels.Length; index++)
        {
            ChannelState channel = _channels[index];
            if (!channel.Active)
                continue;
            if (channel.WaitFrames == 0)
                ExecuteUntilWait(channel);
            else
            {
                channel.WaitFrames--;
                UpdateDriverEnvelope(channel);
                UpdateContinuousPitch(channel);
            }
        }
    }

    private void ExecuteUntilWait(ChannelState channel)
    {
        for (int guard = 0; guard < 128 && channel.Active; guard++)
        {
            int command = Next(channel);
            if (command >= 0xf0)
            {
                switch (command)
                {
                    case 0xf0:
                        channel.RawFrequencyMode = channel.Index < 6;
                        channel.RawEnvelope = Next(channel);
                        channel.SkipContinuousDriverUpdates = channel.Index < 6 &&
                            (channel.RawEnvelope & 0x3f) != 0;
                        if (channel.Index < 4)
                            channel.DutyOrWaveform = channel.RawEnvelope >> 6;
                        else if (channel.Index == 7)
                        {
                            channel.Volume = channel.RawEnvelope >> 4;
                            channel.NoiseTriggerPending = true;
                        }
                        continue;
                    case 0xf1:
                    case 0xf2:
                    case 0xf3:
                        continue;
                    case 0xf6:
                        channel.DutyOrWaveform = Next(channel);
                        if (channel.Index is 4 or 5)
                        {
                            // setWaveform disables CH3, writes wave RAM, then
                            // retriggers NR34. The first fresh sample is index 1.
                            channel.Phase = 1.0 / 32.0;
                            channel.Gate = true;
                        }
                        continue;
                    case 0xf8:
                        channel.PitchSlide = (sbyte)Next(channel);
                        continue;
                    case 0xf9:
                        channel.Vibrato = Next(channel);
                        continue;
                    case 0xfd:
                        channel.PitchShift = (sbyte)Next(channel);
                        continue;
                    case 0xfe:
                        channel.Offset = _data.JumpOffset(channel.Bank, NextWord(channel));
                        continue;
                    default:
                        channel.Stop();
                        continue;
                }
            }
            if (command >= 0xe0)
            {
                channel.Envelope = command & 7;
                channel.EnvelopeParameter = Next(channel) & 7;
                continue;
            }
            if (command >= 0xd0)
            {
                if (channel.Index != 4)
                    channel.Volume = command & 0x0f;
                continue;
            }
            if (command == 0x60 && !channel.RawFrequencyMode && channel.Index != 7)
            {
                if (channel.Index < 4)
                {
                    // Square rests with no release envelope retrigger NRx2 as
                    // volume:$1, producing the original short decay tail.
                    if (channel.EnvelopeParameter == 0)
                        BeginSquareRelease(channel);
                }
                else if (channel.Index < 6)
                {
                    // CH3 remains clocked but NR32 is set to mute.
                    channel.Gate = false;
                }
                // Noise command $60 is not in noiseFrequencyTable, so CH4's
                // current hardware envelope is left running.
                SetWait(channel, Next(channel));
                return;
            }
            if (command == 0x61 && channel.Index < 4 && !channel.RawFrequencyMode)
            {
                SetWait(channel, Next(channel));
                return;
            }

            if (channel.Index >= 6)
            {
                SetNoiseNote(channel, command);
                SetWait(channel, Next(channel));
                return;
            }

            int frequency = channel.RawFrequencyMode
                ? ((command << 8) | Next(channel))
                : channel.Index >= 4
                    ? _data.FrequencyRegisterByIndex(command)
                    : _data.FrequencyRegister(command);
            channel.BaseFrequencyRegister = (frequency + channel.PitchShift) & 0x7ff;
            channel.CurrentFrequencyRegister = channel.BaseFrequencyRegister;
            channel.Gate = true;
            BeginSquareEnvelope(channel);
            channel.VibratoPhase = 0;
            channel.VibratoDelay = ((channel.Vibrato >> 4) & 0x0f) * 2;
            SetWait(channel, Next(channel));
            return;
        }
        if (channel.Active)
            throw new InvalidOperationException(
                $"Sound channel {channel.Index} exceeded 128 immediate commands at ${channel.Offset:x5}.");
    }

    private void SetNoiseNote(ChannelState channel, int note)
    {
        channel.NoiseNote = note;
        if (channel.Index == 7)
        {
            channel.NoiseRegister = note;
            if (!channel.NoiseTriggerPending)
                return;
            channel.NoiseTriggerPending = false;
            channel.Gate = (channel.RawEnvelope & 0xf8) != 0;
            channel.NoiseLfsr = 0x7fff;
            channel.Phase = 0;
            BeginNoiseEnvelope(channel);
            return;
        }

        if (!_data.TryGetNoise(note, out OracleSoundData.NoiseRecord record))
            return;
        channel.NoiseEnvelope = record.Envelope;
        channel.NoiseRegister = record.Frequency;
        channel.Gate = true;
        channel.NoiseLfsr = 0x7fff;
        channel.Phase = 0;
        BeginNoiseEnvelope(channel);
    }

    private void BeginSquareEnvelope(ChannelState channel)
    {
        if (channel.Index >= 4)
            return;
        int volume = EffectiveChannelVolume(channel);
        if (channel.Envelope != 0)
        {
            channel.OutputVolume = volume == 0 ? 0 : 1;
            channel.EnvelopeDirection = +1;
            channel.EnvelopePeriod = channel.Envelope;
            channel.EnvelopeAttackTarget = volume;
            channel.EnvelopeAttackFrames = _data.EnvelopeAttackFrames(
                volume, channel.Envelope);
            channel.EnvelopeStage = 1;
        }
        else
        {
            channel.OutputVolume = volume;
            channel.EnvelopeDirection = -1;
            channel.EnvelopePeriod = channel.EnvelopeParameter;
            channel.EnvelopeAttackTarget = -1;
            channel.EnvelopeAttackFrames = 0;
            channel.EnvelopeStage = channel.EnvelopeParameter == 0 ? 2 : 3;
        }
        channel.EnvelopeCounter = channel.EnvelopePeriod;
        channel.EnvelopeClock = 0;
    }

    private void BeginSquareRelease(ChannelState channel)
    {
        channel.Gate = true;
        channel.OutputVolume = EffectiveChannelVolume(channel);
        channel.EnvelopeDirection = -1;
        channel.EnvelopePeriod = 1;
        channel.EnvelopeCounter = 1;
        channel.EnvelopeAttackTarget = -1;
        channel.EnvelopeAttackFrames = 0;
        channel.EnvelopeStage = 3;
        channel.EnvelopeClock = 0;
    }

    private void BeginNoiseEnvelope(ChannelState channel)
    {
        if (channel.Index == 7)
        {
            channel.OutputVolume = channel.RawEnvelope >> 4;
            channel.EnvelopeDirection = (channel.RawEnvelope & 8) != 0 ? +1 : -1;
            channel.EnvelopePeriod = channel.RawEnvelope & 7;
        }
        else
        {
            channel.OutputVolume = EffectiveChannelVolume(channel);
            channel.EnvelopeDirection = -1;
            channel.EnvelopePeriod = channel.NoiseEnvelope & 7;
        }
        channel.EnvelopeAttackTarget = -1;
        channel.EnvelopeAttackFrames = 0;
        channel.EnvelopeStage = channel.EnvelopePeriod == 0 ? 2 : 3;
        channel.EnvelopeCounter = channel.EnvelopePeriod;
        channel.EnvelopeClock = 0;
    }

    private void UpdateDriverEnvelope(ChannelState channel)
    {
        if (channel.Index >= 4 || channel.EnvelopeStage != 1 ||
            channel.SkipContinuousDriverUpdates)
            return;
        if (channel.EnvelopeAttackFrames > 0)
        {
            channel.EnvelopeAttackFrames--;
            return;
        }
        channel.OutputVolume = channel.EnvelopeAttackTarget;
        channel.EnvelopeAttackTarget = -1;
        channel.EnvelopeDirection = -1;
        channel.EnvelopePeriod = channel.EnvelopeParameter;
        channel.EnvelopeCounter = channel.EnvelopePeriod;
        channel.EnvelopeStage = channel.EnvelopeParameter == 0 ? 2 : 3;
        channel.EnvelopeClock = 0;
    }

    private static void AdvanceHardwareEnvelope(ChannelState channel)
    {
        if (!channel.Active || !channel.Gate || channel.Index is 4 or 5 ||
            channel.EnvelopePeriod == 0)
            return;
        channel.EnvelopeClock += 64.0 / SampleRate;
        while (channel.EnvelopeClock >= 1.0)
        {
            channel.EnvelopeClock -= 1.0;
            if (channel.EnvelopeCounter > 1)
            {
                channel.EnvelopeCounter--;
                continue;
            }
            channel.EnvelopeCounter = channel.EnvelopePeriod;
            int next = channel.OutputVolume + channel.EnvelopeDirection;
            if (next is < 0 or > 15)
            {
                channel.EnvelopePeriod = 0;
                return;
            }
            channel.OutputVolume = next;
        }
    }

    private int EffectiveChannelVolume(ChannelState channel)
    {
        if (channel.Index >= 2)
            return channel.Volume;
        return _musicVolume switch
        {
            0 => 0,
            1 => channel.Volume >> 2,
            2 => channel.Volume >> 1,
            _ => channel.Volume
        };
    }

    private static void SetWait(ChannelState channel, int frames) =>
        channel.WaitFrames = (frames - 1) & 0xff;

    private void UpdateContinuousPitch(ChannelState channel)
    {
        if (channel.Index >= 6 || !channel.Gate || channel.SkipContinuousDriverUpdates)
            return;
        channel.BaseFrequencyRegister =
            (channel.BaseFrequencyRegister + channel.PitchSlide) & 0x7ff;
        int vibrato = 0;
        if (channel.VibratoDelay > 0)
            channel.VibratoDelay--;
        else if ((channel.Vibrato & 0x0f) != 0)
        {
            vibrato = _data.VibratoOffset(channel.VibratoPhase) * (channel.Vibrato & 0x0f);
            channel.VibratoPhase = (channel.VibratoPhase + 1) & 7;
        }
        channel.CurrentFrequencyRegister = (channel.BaseFrequencyRegister + vibrato) & 0x7ff;
    }

    private int Next(ChannelState channel) => _data.ReadByte(channel.Offset++);
    private int NextWord(ChannelState channel)
    {
        int value = _data.ReadWord(channel.Offset);
        channel.Offset += 2;
        return value;
    }

    private void StopChannels(params int[] channels)
    {
        foreach (int channel in channels)
            _channels[channel].Stop();
    }

    private void BeginFade(int direction, int speed)
    {
        _fadeDirection = direction;
        _fadeSpeed = speed;
        _fadeCounter = 0;
        _masterVolume = direction > 0 ? 0 : 7;
    }

    private void UpdateFade()
    {
        if (_fadeDirection == 0)
            return;
        _fadeCounter++;
        if ((_fadeCounter & _fadeSpeed) != _fadeSpeed)
            return;
        if (_fadeDirection < 0)
        {
            // NR50 level 0 still outputs at 1/8 volume. The driver leaves that
            // level active for one fade interval, then stops all channels.
            if (_masterVolume > 0)
            {
                _masterVolume--;
                return;
            }
            foreach (ChannelState channel in _channels)
                channel.Stop();
            ActiveMusic = 0;
            _fadeDirection = 0;
            return;
        }

        if (_masterVolume < 7)
        {
            _masterVolume++;
            return;
        }
        _fadeDirection = 0;
    }

    private void FillAudioBuffer()
    {
        if (_playback is null)
            return;
        int frames = _playback.GetFramesAvailable();
        for (int frame = 0; frame < frames; frame++)
        {
            foreach (ChannelState channel in _channels)
                AdvanceHardwareEnvelope(channel);
            float mix = 0;
            mix += RenderPhysicalPair(0, 2);
            mix += RenderPhysicalPair(1, 3);
            mix += RenderPhysicalPair(4, 5);
            mix += RenderPhysicalPair(6, 7);
            // NR51 routes every channel to both outputs. NR50 scales levels
            // $0-$7 as 1/8 through 8/8, never as true silence.
            mix *= 0.25f * ((_masterVolume + 1) / 8.0f);
            double filtered = mix - _highPassCapacitor;
            _highPassCapacitor = mix - filtered * CgbHighPassFactor;
            float sample = Math.Clamp((float)filtered, -1.0f, 1.0f);
            _playback.PushFrame(new Vector2(sample, sample));
        }
    }

    private float RenderPhysicalPair(int music, int sfx)
    {
        ChannelState channel = _channels[sfx].Active ? _channels[sfx] : _channels[music];
        if (!channel.Active)
            return 0;
        float volume = channel.Index is 4 or 5 ? 1.0f : channel.OutputVolume / 15.0f;
        if (channel.Index >= 6)
        {
            float noise = RenderNoise(channel);
            return channel.Gate && volume > 0 ? noise * volume : 0;
        }
        float frequency = ToneFrequencyForValidation(
            channel.Index, channel.CurrentFrequencyRegister);
        channel.Phase = (channel.Phase + frequency / SampleRate) % 1.0;
        if (channel.Index >= 4)
        {
            float waveVolume = channel.Index == 4 ? _musicVolume switch
            {
                0 => 0,
                1 => 0.25f,
                2 => 0.5f,
                _ => 1.0f
            } : 1.0f;
            return channel.Gate
                ? _data.WaveSample(channel.DutyOrWaveform, (int)(channel.Phase * 32)) * waveVolume
                : 0;
        }
        if (!channel.Gate || volume <= 0)
            return 0;
        float duty = SquareDuty[channel.DutyOrWaveform & 3];
        return (channel.Phase < duty ? 1.0f : -1.0f) * volume;
    }

    private static float RenderNoise(ChannelState channel)
    {
        int register = channel.NoiseRegister;
        float frequency = NoiseClockForValidation(register);
        channel.Phase += frequency / SampleRate;
        while (channel.Phase >= 1.0)
        {
            channel.Phase -= 1.0;
            int xor = (channel.NoiseLfsr & 1) ^ ((channel.NoiseLfsr >> 1) & 1);
            channel.NoiseLfsr = (channel.NoiseLfsr >> 1) | (xor << 14);
            if ((register & 8) != 0)
                channel.NoiseLfsr = (channel.NoiseLfsr & ~(1 << 6)) | (xor << 6);
        }
        return (channel.NoiseLfsr & 1) == 0 ? 1.0f : -1.0f;
    }

    internal static float ToneFrequencyForValidation(int channel, int frequencyRegister)
    {
        float clock = channel is 4 or 5 ? 65536.0f : 131072.0f;
        return clock / Math.Max(1, 2048 - (frequencyRegister & 0x7ff));
    }

    internal static float NoiseClockForValidation(int noiseRegister)
    {
        int divider = noiseRegister & 7;
        float divisor = divider == 0 ? 0.5f : divider;
        int shift = (noiseRegister >> 4) & 0x0f;
        return shift >= 14 ? 0 : 262144.0f / divisor / (1 << (shift + 1));
    }

    internal static double CgbHighPassFactorForValidation => CgbHighPassFactor;

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
}
