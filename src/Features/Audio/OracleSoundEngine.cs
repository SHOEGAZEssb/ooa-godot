using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Persistent sound request queue, original driver, and CGB audio output.</summary>
public partial class OracleSoundEngine : Node
{
    public const int UpdatesPerSecond = 60;
    public const int SampleRate = 44100;
    // Capacity must also accommodate larger device mixer bursts. This is
    // storage capacity, not how far ahead we fill the playback.
    internal const float OutputBufferLengthSeconds = 0.2f;
    private const int OutputLeadFrames = 1024;
    private const int MaximumOutputLeadFrames = SampleRate / 10;
    private const int MaximumQueuedOutputFrames = SampleRate / 15;
    private const int OutputBridgeFrames = 64;

    private readonly OracleSoundData _data;
    private readonly OracleSoundDriver _driver;
    private readonly OracleApu _apu;
    private readonly ChannelState[] _channels = new ChannelState[8];
    private readonly byte[] _requests = new byte[16];
    private readonly Queue<Vector2> _samples = new();
    private readonly Vector2[] _outputBuffer = new Vector2[
        MaximumQueuedOutputFrames + MaximumOutputLeadFrames - OutputLeadFrames];
    private readonly bool _enableOutput;
    private readonly bool _allowHeadlessOutput;
    private readonly ApplicationFixedUpdateScheduler _updates = new();
    private IOracleSoundRequestObserver? _requestObserver;
    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private int _requestHead, _requestTail;
    private int _requestedVolume = 3;
    private bool _volumePending;
    private long _clockOrigin, _updateCount;
    private int _outputCapacity, _outputSkips, _bridgeRemaining;
    private int _outputLeadFrames = OutputLeadFrames;
    private Vector2 _lastOutputSample, _bridgeStart;
    private bool _outputDiscontinuity;
    internal bool ApplicationUpdateOwned { get; set; }

    public int ActiveMusic { get; private set; }
    public bool Disabled => _driver.ReadState(WramAddress.wSoundDisabled) != 0;
    public int MusicVolume => _requestedVolume;
    internal OracleSoundData Data => _data;
    internal OracleSoundDriver Driver => _driver;
    internal OracleApu Apu => _apu;
    internal ChannelState Channel(int channel) => _channels[channel];
    internal void SetNativeChannelVolume(int channel, byte value) => _driver.SetChannelVolumeByte(channel, value);
    internal bool OutputResourcesActiveForValidation => _player is not null || _playback is not null;

    public OracleSoundEngine() : this(new OracleSoundData(), true) { }

    internal OracleSoundEngine(OracleSoundData data, bool enableOutput, bool allowHeadlessOutput = false)
    {
        _data = data;
        _enableOutput = enableOutput;
        _allowHeadlessOutput = allowHeadlessOutput;
        _apu = new OracleApu(enableOutput ? QueueSample : null);
        _driver = new OracleSoundDriver(data, _apu);
        _clockOrigin = _apu.Clocks;
        _samples.Clear();
        for (int i = 0; i < 8; i++) _channels[i] = new ChannelState(i, data, _driver);
    }

    public override void _Ready()
    {
        if (!_enableOutput || (!_allowHeadlessOutput && DisplayServer.GetName() == "headless")) return;
        using var stream = new AudioStreamGenerator { MixRate = SampleRate, BufferLength = OutputBufferLengthSeconds };
        _player = new AudioStreamPlayer { Name = "OracleApu", Stream = stream };
        AddChild(_player);
        _player.Play();
        _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _ExitTree()
    {
        _player?.Stop();
        if (_playback is not null)
        {
            _playback.Stop();
            _playback.ClearBuffer();
            _playback.Dispose();
            _playback = null;
        }
        if (_player is not null) _player.Stream = null;
        _player = null;
        _samples.Clear();
        _outputCapacity = _outputSkips = _bridgeRemaining = 0;
        _outputLeadFrames = OutputLeadFrames;
        _lastOutputSample = _bridgeStart = Vector2.Zero;
        _outputDiscontinuity = false;
        _requestObserver = null;
    }

    public override void _Process(double delta)
    {
        if (!ApplicationUpdateOwned) _updates.Advance(delta, Tick);
        FillAudioBuffer();
    }

    internal void AdvanceApplicationUpdate() => Tick();
    public void PlayRoomMusic(int group, int room) => PlayMusicIfChanged(_data.RoomMusic(group, room));
    public void PlayRoomMusic(int group, int room, OracleSaveData save) =>
        PlayMusicIfChanged(_data.RoomMusic(group, room, save));

    public void PlayMusicIfChanged(int music)
    {
        if (music != ActiveMusic) PlaySound(music == 0 ? SoundId.SndCtrlStopMusic : music);
    }

    public void SetMusicVolume(int volume)
    {
        // bank0.s:setMusicVolume writes hMusicVolume; timerInterrupt applies
        // its last requested value BEFORE draining wMusicQueue.
        _requestedVolume = Math.Clamp(volume, 0, 3);
        _volumePending = true;
    }

    public void RestartSound()
    {
        _driver.Stop();
        // restartSound/enableTimer discard pending requests, retaining the
        // driver volume, fades, disabled state and pending hMusicVolume write.
        _requestHead = _requestTail = 0;
        ActiveMusic = 0;
    }

    public void PlaySound(int soundId)
    {
        if (soundId == SoundId.MusNone) return;
        if ((uint)soundId >= OracleSoundData.SoundCount &&
            soundId is not (SoundId.SndCtrlStopMusic or SoundId.SndCtrlStopSfx or SoundId.SndCtrlDisable or SoundId.SndCtrlEnable or
                SoundId.SndctrlFastFadein or SoundId.SndctrlMediumFadein or SoundId.SndctrlSlowFadein or SoundId.SndCtrlFastFadeOut or SoundId.SndCtrlMediumFadeOut or SoundId.SndCtrlSlowFadeOut))
            throw new ArgumentOutOfRangeException(nameof(soundId), $"Unsupported original sound ${soundId:x2}.");
        _requestObserver?.OnSoundRequested(soundId);
        // The original 16-byte ring has no overflow guard. Tail meeting head
        // means empty, including when exactly 16 requests overwrite the queue.
        _requests[_requestTail] = (byte)soundId;
        _requestTail = (_requestTail + 1) & 15;
        if (soundId == SoundId.SndCtrlStopMusic) ActiveMusic = 0;
        else if (soundId < OracleSoundData.MusicCount) ActiveMusic = soundId;
    }

    internal void SetRequestObserver(IOracleSoundRequestObserver? observer) => _requestObserver = observer;

    internal void Tick()
    {
        if (_volumePending)
        {
            _driver.SetVolume(_requestedVolume);
            _volumePending = false;
        }
        while (_requestHead != _requestTail)
        {
            _driver.Play(_requests[_requestHead]);
            _requestHead = (_requestHead + 1) & 15;
        }
        _driver.Update();
        // Preserve every update's audio when a host frame batches gameplay.
        // Hardware clocks are independent of driver enable/mute state.
        long target = _clockOrigin + ++_updateCount * OracleApu.ClockRate / UpdatesPerSecond;
        if (_apu.Clocks < target) _apu.AdvanceClocks(checked((int)(target - _apu.Clocks)));
    }

    private void QueueSample(Vector2 sample)
    {
        // Bound latency after a host stall. This is presentation backpressure;
        // all driver and APU updates still execute, including discarded audio.
        if (_samples.Count >= SampleRate / 4)
        {
            _samples.Dequeue();
            _outputDiscontinuity = true;
        }
        _samples.Enqueue(sample);
    }

    private void FillAudioBuffer()
    {
        if (_playback is null || _samples.Count == 0) return;
        int available = _playback.GetFramesAvailable();
        bool starting = _outputCapacity == 0;
        if (starting) _outputCapacity = available;
        int queued = _outputCapacity - available;
        int skips = _playback.GetSkips();
        bool recovering = skips != _outputSkips;
        _outputSkips = skips;
        // Learn a larger reserve only when this output device underruns.
        // Bluetooth/mobile mixer bursts and host jitter can exceed the
        // desktop reserve. Never change the driver or APU update clock.
        if (recovering && !starting)
            _outputLeadFrames = Math.Min(MaximumOutputLeadFrames, _outputLeadFrames * 2);

        // Godot consumes blocks on its audio thread, independently of the
        // application update. Keep a small initial reserve instead of feeding
        // an empty playback exactly one 60 Hz update at a time.
        if (starting || recovering)
        {
            int lead = Math.Min(Math.Max(0, _outputLeadFrames - queued), available);
            Array.Clear(_outputBuffer, 0, lead);
            _playback.PushBuffer(_outputBuffer.AsSpan(0, lead));
            queued += lead;
            available -= lead;
            if (lead > 0) _lastOutputSample = Vector2.Zero;
        }

        // Flush only after the complete host-frame batch. Count BOTH queues
        // when bounding latency; retaining 250 ms behind a full native ring
        // delays every subsequent effect even after the host has recovered.
        int keep = Math.Max(0,
            MaximumQueuedOutputFrames + _outputLeadFrames - OutputLeadFrames - queued);
        while (_samples.Count > keep)
        {
            _samples.Dequeue();
            _outputDiscontinuity = true;
        }
        if (_outputDiscontinuity || recovering)
        {
            // Bridge only a presentation discontinuity. Ordinary APU samples
            // and source register transitions pass through unchanged.
            _bridgeStart = _lastOutputSample;
            _bridgeRemaining = OutputBridgeFrames;
            _outputDiscontinuity = false;
        }
        int frames = Math.Min(available, _samples.Count);
        for (int i = 0; i < frames; i++)
        {
            Vector2 sample = _samples.Dequeue();
            if (_bridgeRemaining > 0)
            {
                sample = _bridgeStart.Lerp(sample, 1f - _bridgeRemaining / (float)OutputBridgeFrames);
                _bridgeRemaining--;
            }
            _outputBuffer[i] = _lastOutputSample = sample;
        }
        _playback.PushBuffer(_outputBuffer.AsSpan(0, frames));
    }

    internal static float ToneFrequencyForValidation(int channel, int frequencyRegister) =>
        (channel is 4 or 5 ? 65536.0f : 131072.0f) / (2048 - (frequencyRegister & 0x7ff));

    internal static float NoiseClockForValidation(int noiseRegister)
    {
        int divider = noiseRegister & 7, shift = noiseRegister >> 4;
        return shift >= 14 ? 0 : 262144.0f / (divider == 0 ? 0.5f : divider) / (1 << shift);
    }

    internal static double CgbHighPassFactorForValidation => OracleApu.HighPassFactor;
}
