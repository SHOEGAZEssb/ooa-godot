using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Persistent sound request queue, original driver, and CGB audio output.</summary>
public partial class OracleSoundEngine : Node
{
    public const int UpdatesPerSecond = 60;
    public const int SampleRate = 44100;
    // Capacity must fit a 30 Hz host's two updates plus a mixer safety margin.
    // This is storage capacity, not how far ahead we fill the playback.
    internal const float OutputBufferLengthSeconds = 0.05f;
    private const int OutputLeadFrames = 1024;
    private const int MaximumQueuedOutputFrames = SampleRate / 15;
    private const int OutputBridgeFrames = 64;
    public const int MusTitlescreen = 0x01;
    public const int MusMinigame = 0x02;
    public const int MusOverworld = 0x03;
    public const int MusEssence = 0x06;
    public const int MusNayru = 0x08;
    public const int MusGameOver = 0x09;
    public const int MusEssenceRoom = 0x0d;
    public const int MusFairyFountain = 0x0f;
    public const int MusGetEssence = 0x10;
    public const int MusFileSelect = 0x11;
    public const int MusSpiritsGrave = 0x13;
    public const int MusRoomOfRites = 0x1d;
    public const int MusMakuTree = 0x1e;
    public const int MusSadness = 0x1f;
    public const int MusDisaster = 0x21;
    public const int MusMapleTheme = 0x2b;
    public const int MusMapleGame = 0x2c;
    public const int MusMiniboss = 0x2d;
    public const int MusBoss = 0x2e;
    public const int MusLadxSideview = 0x2f;
    public const int MusCrazyDance = 0x31;
    public const int MusRalph = 0x35;
    public const int MusIntro1 = 0x3f;
    public const int MusIntro2 = 0x40;
    public const int MusBlackTowerEntrance = 0x46;
    public const int SndGetItem = 0x4c;
    public const int SndSolvePuzzle = 0x4d;
    public const int SndDamageEnemy = 0x4e;
    public const int SndChargeSword = 0x4f;
    public const int SndClink = 0x50;
    public const int SndThrow = 0x51;
    public const int SndBombLand = 0x52;
    public const int SndJump = 0x53;
    public const int SndOpenMenu = 0x54;
    public const int SndCloseMenu = 0x55;
    public const int SndSelectItem = 0x56;
    public const int SndGainHeart = 0x57;
    public const int SndClink2 = 0x58;
    public const int SndFallInHole = 0x59;
    public const int SndError = 0x5a;
    public const int SndSwordBeam = 0x5d;
    public const int SndEnergyThing = 0x5c;
    public const int SndGetSeed = 0x5e;
    public const int SndDamageLink = 0x5f;
    public const int SndRupee = 0x61;
    public const int SndBossDamage = 0x63;
    public const int SndLinkDead = 0x64;
    public const int SndLinkFall = 0x65;
    public const int SndText = 0x66;
    public const int SndBossDead = 0x67;
    public const int SndSlash = 0x6a;
    public const int SndSwordSpin = 0x6b;
    public const int SndOpenChest = 0x6c;
    public const int SndCutGrass = 0x6d;
    public const int SndEnterCave = 0x6e;
    public const int SndExplosion = 0x6f;
    public const int SndDoorClose = 0x70;
    public const int SndDing = 0xc8;
    public const int SndSeedShooter = 0xcb;
    public const int SndMoveBlock = 0x71;
    public const int SndLightTorch = 0x72;
    public const int SndKillEnemy = 0x73;
    public const int SndSwordSlash = 0x74;
    public const int SndUnknown5 = 0x75;
    public const int SndShield = 0x76;
    public const int SndDropEssence = 0x77;
    public const int SndBoomerang = 0x78;
    public const int SndBigExplosion = 0x79;
    public const int SndMysterySeed = 0x7b;
    public const int SndAquamentusHover = 0x7c;
    public const int SndStrongPound = 0x81;
    public const int SndMagicPowder = 0x83;
    public const int SndMenuMove = 0x84;
    public const int SndScentSeed = 0x85;
    public const int SndSplash = 0x87;
    public const int SndText2 = 0x89;
    public const int SndFilledHeartContainer = 0x8b;
    public const int SndTeleport = 0x8d;
    public const int SndFairyCutscene = 0x91;
    public const int SndCompass = 0xa2;
    public const int SndWarpStart = 0x95;
    public const int SndPoof = 0x98;
    public const int SndBaseball = 0x99;
    public const int SndPickup = 0x9c;
    public const int SndChicken = 0xa0;
    public const int SndLand = 0xa3;
    public const int SndLinkSwim = 0x88;
    public const int SndBeam = 0xa4;
    public const int SndBreakRock = 0xa5;
    public const int SndStrike = 0xa6;
    public const int SndVeranFairyAttack = 0xa8;
    public const int SndDig = 0xa9;
    public const int SndSwordObtained = 0xab;
    public const int SndShock = 0xac;
    public const int SndTuneOfEchoes = 0xad;
    public const int SndTuneOfCurrents = 0xae;
    public const int SndTuneOfAges = 0xaf;
    public const int SndOpening = 0xb0;
    public const int SndMakuDisappear = 0xb2;
    public const int SndFadeOut = 0xb4;
    public const int SndRumble2 = 0xb8;
    public const int SndWhistle = 0xcc;
    public const int SndMakuTreePast = 0xce;
    public const int SndPirateBell = 0xd0;
    public const int SndTimewarpInitiated = 0xd1;
    public const int SndLightning = 0xd2;
    public const int SndTimewarpCompleted = 0xd4;
    public const int SndCtrlStopMusic = 0xf0;
    public const int SndCtrlStopSfx = 0xf1;
    public const int SndCtrlDisable = 0xf5;
    public const int SndCtrlEnable = 0xf6;
    public const int SndCtrlFastFadeOut = 0xfa;
    public const int SndCtrlMediumFadeOut = 0xfb;
    public const int SndCtrlSlowFadeOut = 0xfc;


    private readonly OracleSoundData _data;
    private readonly OracleSoundDriver _driver;
    private readonly OracleApu _apu;
    private readonly ChannelState[] _channels = new ChannelState[8];
    private readonly byte[] _requests = new byte[16];
    private readonly Queue<Vector2> _samples = new();
    private readonly Vector2[] _outputBuffer = new Vector2[MaximumQueuedOutputFrames];
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
    private Vector2 _lastOutputSample, _bridgeStart;
    private bool _outputDiscontinuity;
    internal bool ApplicationUpdateOwned { get; set; }

    public int ActiveMusic { get; private set; }
    public bool Disabled => _driver.ReadState(0xc01b) != 0;
    public int MusicVolume => _requestedVolume;
    internal OracleSoundData Data => _data;
    internal OracleSoundDriver Driver => _driver;
    internal OracleApu Apu => _apu;
    internal ChannelState Channel(int channel) => _channels[channel];
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
        if (music != ActiveMusic) PlaySound(music == 0 ? SndCtrlStopMusic : music);
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
        if (soundId == 0) return;
        if ((uint)soundId >= OracleSoundData.SoundCount &&
            soundId is not (SndCtrlStopMusic or SndCtrlStopSfx or SndCtrlDisable or SndCtrlEnable or
                0xf7 or 0xf8 or 0xf9 or 0xfa or 0xfb or 0xfc))
            throw new ArgumentOutOfRangeException(nameof(soundId), $"Unsupported original sound ${soundId:x2}.");
        _requestObserver?.OnSoundRequested(soundId);
        // The original 16-byte ring has no overflow guard. Tail meeting head
        // means empty, including when exactly 16 requests overwrite the queue.
        _requests[_requestTail] = (byte)soundId;
        _requestTail = (_requestTail + 1) & 15;
        if (soundId == SndCtrlStopMusic) ActiveMusic = 0;
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

        // Godot consumes blocks on its audio thread, independently of the
        // application update. Keep a small initial reserve instead of feeding
        // an empty playback exactly one 60 Hz update at a time.
        if (starting || (recovering && queued == 0))
        {
            int lead = Math.Min(OutputLeadFrames, available);
            Array.Clear(_outputBuffer, 0, lead);
            _playback.PushBuffer(_outputBuffer.AsSpan(0, lead));
            queued += lead;
            available -= lead;
            _lastOutputSample = Vector2.Zero;
        }

        // Flush only after the complete host-frame batch. Count BOTH queues
        // when bounding latency; retaining 250 ms behind a full native ring
        // delays every subsequent effect even after the host has recovered.
        int keep = Math.Max(0, MaximumQueuedOutputFrames - queued);
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
