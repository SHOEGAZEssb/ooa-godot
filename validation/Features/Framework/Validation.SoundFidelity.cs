using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSoundShortEffects()
    {
        // Independently executed on SameBoy CGB-E, commit 213a12ce93d6:
        // trigger with old NRx2, then write new NRx2 without retriggering.
        // These intermediate volumes are absent from the driver WRAM hashes.
        (int oldValue, int newValue, int volume)[] envelopes =
        [
            (0x08, 0x20, 0), (0x08, 0x80, 0), (0x20, 0xb0, 2), (0x80, 0x10, 8),
            (0xb0, 0x08, 4), (0x08, 0x08, 1), (0x80, 0x81, 7), (0x88, 0x81, 7),
            (0x82, 0x88, 6), (0x88, 0x89, 9), (0x10, 0x08, 14)
        ];
        foreach (int voice in new[] { 0, 1, 3 })
        foreach (var entry in envelopes)
        {
            var apu = new OracleApu();
            int register = 0xff10 + voice * 5;
            apu.Write(register + 2, entry.oldValue);
            apu.Write(register + 4, 0x80);
            apu.Write(register + 2, entry.newValue);
            FailIf(apu.Voice(voice).Volume != entry.volume,
                $"CGB envelope ${register + 2:x4}: ${entry.oldValue:x2}->${entry.newValue:x2} " +
                $"must expose volume ${entry.volume:x} before retriggering, got ${apu.Voice(voice).Volume:x}.");
        }

        var envelopeApu = new OracleApu();
        envelopeApu.Write(0xff11, 0x80);
        envelopeApu.Write(0xff12, 0x08);
        envelopeApu.Write(0xff13, 0xa2);
        envelopeApu.Write(0xff14, 0x87);
        envelopeApu.AdvanceClocks(376 * 8);
        envelopeApu.Write(0xff12, 0x20);
        FailIf(envelopeApu.Voice(0).Volume != 0 || envelopeApu.DigitalOutput(0) != 0,
            "SND_TEXT $66's NR12=$08->$20 write emitted a full-volume pulse before the note trigger.");
        envelopeApu.Write(0xff14, 0x87);
        FailIf(envelopeApu.Voice(0).Volume != 2,
            "Suppressing the spurious attack must retain the actual SND_TEXT $66 volume-$2 note.");

        // Clean US ROM, actual file-selection Down input in SameBoy CGB-E:
        // channelCmdff writes NR12=$08 while volume is $1. Hardware exposes
        // volume $e until func_42ea triggers NR14, 432 double-speed CPU clocks
        // (216 APU clocks) later. This short release click is original behavior;
        // a fade or treating both writes as atomic would conceal it.
        var cursorRelease = new OracleApu();
        cursorRelease.Write(0xff11, 0xd9);
        cursorRelease.Write(0xff12, 0x10);
        cursorRelease.Write(0xff13, 0xa0);
        cursorRelease.Write(0xff14, 0xc7);
        cursorRelease.AdvanceClocks(384); // Duty position 1 is high.
        cursorRelease.Write(0xff12, 0x08);
        cursorRelease.AdvanceClocks(216);
        FailIf(cursorRelease.Voice(0).Volume != 14 || cursorRelease.DigitalOutput(0) != 14,
            "SND_MENU_MOVE $84 must retain the clean-ROM release pulse between NR12=$08 and NR14's trigger.");
        cursorRelease.Write(0xff14, 0xc7);
        cursorRelease.AdvanceClocks(8192);
        FailIf(cursorRelease.Voice(0).Volume != 0 || cursorRelease.DigitalOutput(0) != 0 ||
            !cursorRelease.Voice(0).DacEnabled,
            "SND_MENU_MOVE $84's release trigger must silence the pulse while leaving its DAC connected.");

        var ultrasonic = new List<Vector2>();
        var testApu = new OracleApu(ultrasonic.Add);
        testApu.Write(0xff24, 0x77);
        testApu.Write(0xff25, 0x11);
        testApu.Write(0xff11, 0x80);
        testApu.Write(0xff12, 0xf0);
        testApu.Write(0xff13, 0xff);
        testApu.Write(0xff14, 0x87);
        testApu.AdvanceClocks(OracleApu.ClockRate / 10);
        double aliasRms = Math.Sqrt(ultrasonic.Skip(1000).Average(s => (double)s.X * s.X));
        FailIf(aliasRms > 0.0001,
            "The 131072 Hz pulse must not alias into audible 1228 Hz noise during sample conversion.");

        var audible = new List<Vector2>();
        testApu = new OracleApu(audible.Add);
        testApu.Write(0xff24, 0x77);
        testApu.Write(0xff25, 0x10); // CH1 left only.
        testApu.Write(0xff11, 0x80);
        testApu.Write(0xff12, 0xf0);
        testApu.Write(0xff13, 0x00);
        testApu.Write(0xff14, 0x87); // 131072 / (2048 - $700) = 512 Hz.
        testApu.AdvanceClocks(2 * OracleApu.ClockRate);
        double real = 0, imaginary = 0;
        for (int i = 0; i < 44100; i++)
        {
            double angle = 2 * Math.PI * 512 * i / 44100;
            real += audible[i + 44100].X * Math.Cos(angle);
            imaginary += audible[i + 44100].X * Math.Sin(angle);
        }
        double amplitude = 2 * Math.Sqrt(real * real + imaginary * imaginary) / 44100;
        double coefficient = Math.Pow(0.998943, 4194304.0 / 44100);
        double cosine = Math.Cos(2 * Math.PI * 512 / 44100);
        // The 50% pulse's first Fourier coefficient is 1/pi at this mixer
        // gain, followed by the documented discrete CGB high-pass response.
        double expected = Math.Sqrt((2 - 2 * cosine) /
            (1 + coefficient * coefficient - 2 * coefficient * cosine)) / Math.PI;
        FailIf(Math.Abs(amplitude - expected) > 0.002 || audible.Any(s => s.Y != 0),
            $"Band limiting altered audible pitch/gain or stereo routing: amplitude {amplitude}, expected {expected}.");

        var data = new OracleSoundData();
        foreach (int id in new[] { 0x66, 0x84 })
        {
            using var fixture = new SoundValidationFixture(data, true);
            var sound = fixture.Sound;
            var field = typeof(OracleSoundEngine).GetField("_samples",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var samples = (Queue<Vector2>)field.GetValue(sound)!;
            int interval = id == 0x66 ? 4 : 12;
            for (int update = 0; update < interval * 3; update++)
            {
                int position = update % interval;
                if (position == 0) sound.PlaySound(id);
                sound.Tick();
                // audio/common/sfx/text.s and menuMove.s, also executed in
                // the clean ROM via SameBoy: logical $02 uses hardware CH1.
                int volume = id == 0x66 ? (position < 2 ? 2 : position == 2 ? 11 : 0) :
                    position < 3 ? 8 : position < 8 ? 1 : 0;
                int nr12 = volume == 0 ? 8 : volume << 4;
                FailIf(sound.Apu.Voice(0).Volume != volume || sound.Apu.Register(0xff12) != nr12 ||
                    sound.Channel(2).Active != (position < (id == 0x66 ? 3 : 8)) ||
                    sound.Apu.Frequency(0) != (id == 0x66 ? 0x7a2 : 0x7a0),
                    $"Sound ${id:x2} note, release, or repeated trigger diverged at update {update}.");
                FailIf(samples.Count is < 734 or > 736 || samples.Any(s => !float.IsFinite(s.X) || !float.IsFinite(s.Y)),
                    $"Sound ${id:x2} emitted an invalid PCM update at {update}.");
                samples.Clear();
            }
        }

        int[]? menuTrace = null;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            var audit = _sound.AttachPlayRequestAudit();
            _mainMenuScreen = new MainMenuScreen();
            AddChild(_mainMenuScreen);
            _mainMenu = new MainMenuController(_mainMenuScreen, (_, _) => { },
                _ => null, (_, _) => SaveResult.Succeeded, _ => { }, _sound.PlaySound, startAtFileSelect: true);
            StepGameplayUpdates(60, Vector2.Zero, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["move_down"], ["move_down"], batched);
            FailIf(_mainMenu.Cursor != 1 || audit.Requests[^1] != 0x84 || !_sound.Channel(2).Active,
                "File-selection Down must start SND_MENU_MOVE $84 over MUS_FILE_SELECT $11 in the same application update.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(_sound.Channel(2).Active, "File-selection cursor sound did not release after its eight source updates.");
            StepGameplayUpdates(1, Vector2.Zero, ["move_up"], ["move_up"], batched);
            FailIf(_mainMenu.Cursor != 0 || !_sound.Channel(2).Active || _sound.ActiveMusic != 0x11,
                "File-selection Up must retrigger the cursor sound while retaining its music.");
            int[] trace = audit.Requests.ToArray();
            if (menuTrace is not null) FailIf(!menuTrace.SequenceEqual(trace), "File-selection audio depends on host batching.");
            menuTrace = trace;
        }
        GD.Print("Validated CGB envelope-write transients, band-limited PCM, repeated text/cursor effects, and actual file-selection audio updates.");
    }

    private void ValidateSoundOutputTiming()
    {
        var data = new OracleSoundData();
        foreach (int hostRate in new[] { 60, 30, 120, 0 })
        {
            using var fixture = new SoundValidationFixture(data, true);
            var sound = fixture.Sound;
            sound.ApplicationUpdateOwned = true;
            using var stream = new AudioStreamGenerator
            {
                MixRate = OracleSoundEngine.SampleRate,
                BufferLength = OracleSoundEngine.OutputBufferLengthSeconds
            };
            using var playback = (AudioStreamGeneratorPlayback)stream.InstantiatePlayback();
            var playbackField = typeof(OracleSoundEngine).GetField("_playback",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            playbackField.SetValue(sound, playback);
            playback.Start();
            int capacity = playback.GetFramesAvailable();
            sound.PlaySound(OracleSoundEngine.MusOverworld);
            var scheduler = new ApplicationFixedUpdateScheduler();
            double mixerRate = AudioServer.GetMixRate();
            double nextMix = 0, elapsed = 0;
            int warmedSkips = -1;
            double[] unevenDeltas = [1.0 / 120, 1.0 / 40, 1.0 / 60, 1.0 / 30, 1.0 / 120];
            var field = typeof(OracleSoundEngine).GetField("_samples",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var queued = (Queue<Vector2>)field.GetValue(sound)!;
            void AdvanceHost(double delta)
            {
                scheduler.Advance(delta, sound.Tick);
                sound._Process(0);
                // Include native and managed storage. Capacity alone does not
                // establish latency; the old managed ring retained 250 ms.
                int pending = capacity - playback.GetFramesAvailable() + queued.Count;
                FailIf(pending > 2940, $"Audio retained {pending} frames, exceeding 66.7 ms of output latency.");
                elapsed += delta;
                while (nextMix < elapsed - 1e-9)
                {
                    playback.MixAudio(1, 512);
                    nextMix += 512 / mixerRate;
                }
            }
            for (int frame = 0; elapsed < 4; frame++)
            {
                AdvanceHost(hostRate == 0 ? unevenDeltas[frame % unevenDeltas.Length] : 1.0 / hostRate);
                if (elapsed >= 1 && warmedSkips < 0) warmedSkips = playback.GetSkips();
            }
            int skips = playback.GetSkips() - warmedSkips;
            FailIf(skips != 0 || queued.Count != 0,
                $"Audio at {hostRate} FPS has {skips} mixer underruns or a stale PCM backlog of {queued.Count} samples.");

            // A real host stall lets the mixer drain first, then batches the
            // missed game updates. Do not keep playing all that stale PCM.
            double stall = 0.25;
            while (nextMix < elapsed + stall)
            {
                playback.MixAudio(1, 512);
                nextMix += 512 / mixerRate;
            }
            AdvanceHost(stall);
            int recoveredSkips = playback.GetSkips();
            for (int frame = 0; frame < 120; frame++) AdvanceHost(1.0 / 60);
            FailIf(playback.GetSkips() != recoveredSkips || queued.Count != 0,
                "Audio did not recover without further underruns or stale samples after a 250 ms host stall.");
            GD.Print($"Audio output at {(hostRate == 0 ? "uneven" : hostRate)} FPS: zero warmed underruns; bounded latency and stall recovery passed.");
            playback.Stop();
            playbackField.SetValue(sound, null);
        }

        // Exercise the actual native output join with deliberately opposite
        // DC samples: there are no legitimate waveform edges to hide a click.
        using var joinFixture = new SoundValidationFixture(data, true);
        var joinSound = joinFixture.Sound;
        joinSound.ApplicationUpdateOwned = true;
        using var joinStream = new AudioStreamGenerator
        {
            MixRate = OracleSoundEngine.SampleRate,
            BufferLength = OracleSoundEngine.OutputBufferLengthSeconds
        };
        using var joinPlayback = (AudioStreamGeneratorPlayback)joinStream.InstantiatePlayback();
        var joinPlaybackField = typeof(OracleSoundEngine).GetField("_playback",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        joinPlaybackField.SetValue(joinSound, joinPlayback);
        var samplesField = typeof(OracleSoundEngine).GetField("_samples",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var samples = (Queue<Vector2>)samplesField.GetValue(joinSound)!;
        joinPlayback.Start();
        for (int i = 0; i < 1470; i++) samples.Enqueue(new Vector2(0.25f, 0.25f));
        joinSound._Process(0);
        joinPlayback.MixAudio(1, (int)(AudioServer.GetMixRate() * 0.04));
        for (int i = 0; i < 11025; i++) samples.Enqueue(new Vector2(-0.25f, -0.25f));
        joinSound._Process(0);
        Vector2[] joined = joinPlayback.MixAudio(1, (int)(AudioServer.GetMixRate() * 0.05));
        float largestJump = joined.Zip(joined.Skip(1), (a, b) => Math.Abs(a.X - b.X)).Max();
        FailIf(largestJump > 0.012f || !joined.Any(s => s.X < -0.24f) || joined[0].X < 0.24f,
            $"Discarded audio must join continuously from the last submitted sample: largest step {largestJump}.");
        joinPlayback.Stop();
        joinPlaybackField.SetValue(joinSound, null);
        GD.Print("Validated the native mixer across steady/batched/uneven host frames, bounded stall recovery, and a continuous PCM join.");
    }

    private void ValidateSoundDriverControls()
    {
        var data = new OracleSoundData();
        var save = OracleSaveData.CreateStandardGame();
        save.SetRoomFlag(0, 0x03, 0x01, false);
        save.SetGlobalFlag(0x29);
        FailIf(data.RoomMusic(0, 0x03) != 0x24 || data.RoomMusic(0, 0x03, save) != 0x1f,
            "checkPlayRoomMusic must replace MUS_SYMMETRY_PRESENT $24 with MUS_SADNESS $1f while room 0:$03 bit 0 is clear, independently of global flag $29.");
        save.SetRoomFlag(0, 0x03, 0x01);
        FailIf(data.RoomMusic(0, 0x03, save) != 0x24,
            "Restoring room 0:$03 bit 0 must restore MUS_SYMMETRY_PRESENT $24.");
        using var fixture = new SoundValidationFixture(data);
        var sound = fixture.Sound;
        sound.PlaySound(0x01);
        FailIf(sound.Channel(0).Active, "bank0.s:playSound must enqueue MUS_TITLESCREEN $01 until timerInterrupt.");
        sound.Tick();
        FailIf(sound.Channel(0).WaitFrames != 0x17, "MUS_TITLESCREEN $01 first note must start on queue drain.");

        // Fade values are stored after NR50 was written. At update 7 the
        // backing volume is $66, but NR50 is still $77 until update 8.
        sound.PlaySound(0xfa);
        for (int i = 0; i < 6; i++) sound.Tick();
        FailIf(sound.Driver.ReadState(0xc024) != 0x77 || sound.Apu.Register(0xff24) != 0x77,
            "SNDCTRL_FAST_FADEOUT $fa lowered volume before update 7.");
        sound.Tick();
        FailIf(sound.Driver.ReadState(0xc024) != 0x66 || sound.Apu.Register(0xff24) != 0x77,
            "updateSound must publish the previous fade volume to NR50 before decrementing wSoundVolume.");
        sound.Tick();
        FailIf(sound.Apu.Register(0xff24) != 0x66, "$fa update 8 must publish NR50=$66.");
        for (int i = 8; i < 62; i++) sound.Tick();
        FailIf(!sound.Channel(0).Active || sound.Apu.Register(0xff24) != 0,
            "$fa must leave music running at NR50=$00 through update 62.");
        sound.Tick();
        FailIf(Enumerable.Range(0, 8).Any(i => sound.Channel(i).Active) ||
            sound.Driver.ReadState(0xc014) != 0 || sound.Driver.ReadState(0xc015) != 0,
            "$fa update 63 must stop all channels and clear fade direction AND counter.");

        foreach ((int command, int mask) in new[] { (0xf7, 3), (0xf8, 7), (0xf9, 15), (0xfa, 7), (0xfb, 15), (0xfc, 31) })
        {
            sound.PlaySound(0x01);
            sound.PlaySound(command);
            bool rising = command <= 0xf9;
            int finish = 8 * (mask + 1) - 1;
            for (int update = 1; update <= finish; update++)
            {
                sound.Tick();
                int steps = (update + 1) / (mask + 1);
                int level = rising ? Math.Min(7, steps) : Math.Max(0, 7 - steps);
                FailIf(sound.Driver.ReadState(0xc024) != level * 0x11 ||
                    (sound.Driver.ReadState(0xc015) == 0) != (update == finish),
                    $"Sound fade ${command:x2} diverged at update {update}; volume/direction boundary is source-derived from updateSound.");
            }
        }

        sound.RestartSound();
        for (int i = 0; i < 16; i++) sound.PlaySound(0x57);
        sound.Tick();
        FailIf(sound.Channel(2).Active, "wMusicQueue wraps after 16 writes; equal head/tail is empty.");
        sound.PlaySound(0x57);
        sound.RestartSound();
        sound.Tick();
        FailIf(sound.Channel(2).Active, "restartSound/enableTimer must discard queued SND_GAINHEART $57.");

        sound.PlaySound(0x01);
        sound.Tick();
        int wait = sound.Channel(0).WaitFrames;
        sound.PlaySound(0xf5);
        sound.Tick();
        FailIf(!sound.Disabled || sound.Channel(0).WaitFrames != wait ||
            sound.Apu.Register(0xff12) != 8 || sound.Apu.Register(0xff17) != 8 ||
            sound.Apu.Register(0xff1c) != 0 || sound.Apu.Register(0xff21) != 8,
            "$f5 must run updateChannelStuff for all eight channels before freezing the driver; CH5 restores the active music wave at its saved mute level.");
        sound.PlaySound(0x54);
        sound.Tick();
        FailIf(sound.Channel(2).WaitFrames != 0 || !sound.Channel(2).Active,
            "Disabled sound must still accept descriptors without executing their programs.");
        sound.PlaySound(0xf6);
        sound.Tick();
        FailIf(sound.Disabled || sound.Channel(2).WaitFrames != 0x15,
            "$f6 must resume queued SND_OPENMENU $54 at its first note.");
        GD.Print("Validated sound queue, all six exact fades, NR50 publication, restart cancellation, disable and resume.");
    }

    private void ValidateSoundDriverHandoffs()
    {
        var data = new OracleSoundData();
        using var fixture = new SoundValidationFixture(data);
        var sound = fixture.Sound;
        sound.PlaySound(0x01);
        sound.Tick();
        sound.PlaySound(0x57); // Three one-update notes, then cmdff.
        for (int i = 0; i < 4; i++) sound.Tick();
        FailIf(sound.Channel(2).Active || !sound.Channel(0).Active ||
            sound.Channel(0).WaitFrames != 0x13 || sound.Apu.Register(0xff12) != 8,
            "SND_GAINHEART $57 cmdff must silence CH1 without resurrecting the interrupted music envelope.");
        for (int i = 0; i < 19; i++) sound.Tick();
        FailIf(sound.Apu.Register(0xff12) != 8 || sound.Channel(0).WaitFrames != 0,
            "Interrupted title note must remain silent through its original last wait update.");
        sound.Tick();
        FailIf(sound.Apu.Register(0xff12) != 0x20 || sound.Channel(0).WaitFrames != 0x13,
            "MUS_TITLESCREEN $01 must restart CH1 only on its next note at update 25.");

        sound.RestartSound();
        sound.SetMusicVolume(1);
        sound.PlaySound(0x11);
        sound.Tick();
        // fileSelectChannel6: vol $5; note $2a $0e. func_39_478c
        // scales music noise as well as squares: 5 >> 2 = 1, NR42=$11.
        FailIf(sound.Apu.Register(0xff21) != 0x11 || sound.Apu.Register(0xff22) != 0x14 ||
            sound.Apu.Register(0xff1c) != 0x60 || sound.Channel(6).WaitFrames != 0x0d,
            "MUS_FILESELECT $11 music volume 1 must scale CH4 to $11 and CH3 to $60 before the first notes.");
        sound.SetMusicVolume(0);
        sound.Tick();
        FailIf(sound.Driver.ReadState(0xc023) != 2 || sound.Apu.Register(0xff12) != 8 ||
            sound.Apu.Register(0xff17) != 8 || sound.Apu.Register(0xff1c) != 0,
            "updateMusicVolume(0) must silence active squares with the old volume gate and latch wc023 after one update.");

        sound.RestartSound();
        sound.SetMusicVolume(3);
        sound.PlaySound(0x11);
        sound.Tick();
        sound.PlaySound(0x5f);
        for (int i = 0; i < 5; i++) sound.Tick();
        // CH5 cmdff restores the current music waveform, but setWaveform
        // writes NR34=$80: only the low frequency byte survives until the
        // following music frequency update. Music must not use a saved voice.
        FailIf(sound.Channel(5).Active || sound.Apu.Register(0xff1e) != 0x80 ||
            sound.Apu.Frequency(2) >= 0x100 || sound.Apu.Register(0xff1c) != 0x20,
            "SND_DAMAGE_LINK $5f completion must reload CH3 wave RAM with the original NR34=$80 handoff.");
        for (int i = 0; i < 16; i++)
        {
            int packed = (int)Math.Round(data.WaveSample(0x0d, i * 2) * 7.5f + 7.5f) << 4;
            packed |= (int)Math.Round(data.WaveSample(0x0d, i * 2 + 1) * 7.5f + 7.5f);
            FailIf(sound.Apu.Register(0xff30 + i) != packed,
                $"CH5 cmdff failed to restore the music waveform $0d byte ${i:x2}.");
        }
        GD.Print("Validated shared physical square/wave handoffs, music noise attenuation, and the music-volume mute latch.");
    }

    private void ValidateSoundApuTiming()
    {
        // These are the original SND_MENU_MOVE register values: cmdf0 $d9
        // installs duty 3, length 64-$19=39, and NRx4 length enable.
        var apu = new OracleApu();
        apu.Write(0xff11, 0xd9);
        apu.Write(0xff12, 0x80);
        apu.Write(0xff13, 0xa0);
        apu.Write(0xff14, 0xc7);
        apu.AdvanceClocks(8192 * 77 - 1);
        FailIf(!apu.Voice(0).Enabled || apu.Voice(0).Length != 1,
            "CGB CH1 length 39 expired before its 39th 256 Hz length clock.");
        apu.AdvanceClocks(1);
        FailIf(apu.Voice(0).Enabled || apu.Voice(0).Length != 0,
            "CGB CH1 raw length did not expire on its exact hardware boundary.");

        apu = new OracleApu();
        apu.Write(0xff21, 0x31);
        apu.Write(0xff22, 0x14);
        apu.Write(0xff23, 0x80);
        apu.AdvanceClocks(65535);
        FailIf(apu.Voice(3).Volume != 3, "CH4 envelope advanced before the shared 64 Hz frame sequencer edge.");
        apu.AdvanceClocks(1);
        FailIf(apu.Voice(3).Volume != 2, "CH4 envelope did not advance at 65536 APU clocks.");
        apu.AdvanceClocks(2 * 65536);
        FailIf(apu.Voice(3).Volume != 0 || !apu.Voice(3).Enabled,
            "A zero envelope must retain CH4's DAC and active status.");
        apu.Write(0xff21, 0);
        FailIf(apu.Voice(3).Enabled || apu.Voice(3).DacEnabled, "NR42=$00 must immediately disable the noise DAC.");

        apu = new OracleApu();
        apu.Write(0xff30, 0xe9);
        apu.Write(0xff1a, 0x80);
        apu.Write(0xff1c, 0x40);
        apu.Write(0xff1d, 0);
        apu.Write(0xff1e, 0x80);
        FailIf(apu.DigitalOutput(2) != 0, "CH3 trigger must retain its previous sample buffer.");
        apu.AdvanceClocks(4096);
        FailIf(apu.DigitalOutput(2) != 4 || apu.Voice(2).Position != 1,
            "CH3 first sample must be nibble 1 ($9), shifted digitally to $4 at NR32=$40.");
        apu.Write(0xff1e, 0x80);
        FailIf(apu.DigitalOutput(2) != 4 || apu.Voice(2).Position != 0,
            "CH3 retrigger must reset its index while retaining the sample latch.");

        var samples = new List<Vector2>();
        apu = new OracleApu(samples.Add);
        apu.Write(0xff24, 0x77);
        apu.Write(0xff25, 0x01); // CH1 right only.
        apu.Write(0xff12, 0x80);
        apu.Write(0xff14, 0x80);
        apu.AdvanceClocks(OracleApu.ClockRate / 60);
        FailIf(samples.Count != 734 || samples.Any(s => s.X != 0) || samples.All(s => s.Y == 0),
            "CGB stereo routing or rational 44100 Hz sample clock diverged.");
        GD.Print("Validated CGB raw note lengths, 64 Hz envelopes, DAC gates, wave latch/digital attenuation and stereo sampling.");
    }

    private void ValidateSoundDriverCatalog()
    {
        var data = new OracleSoundData();
        string[] expected = FileAccess.GetFileAsString(
            "res://validation/Fixtures/Audio/sound-driver-us.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#')).ToArray();
        FailIf(expected.Length != 0xdf, "Independent clean-US sound-driver fixture must cover $00-$de.");
        byte[] snapshot = new byte[0x71 + 0x26];
        for (int id = 0; id < 0xdf; id++)
        {
            using var fixture = new SoundValidationFixture(data);
            var sound = fixture.Sound;
            sound.PlaySound(id);
            try
            {
                // Captured independently with PyBoy 2.7.0 executing the clean
                // US ROM (MD5 C4639CC61C049E5A085526BB6CAC03BB). Each snapshot
                // follows b39_updateSound: WRAM $c014-$c084, HRAM $ffd8-$fffd.
                // Zeroed sound RAM; init($39), play(id), 4096 updates. Hashes
                // include source side effects and 16-bit pitch accumulation,
                // not just the data consumed by the runtime's next command.
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                for (int update = 0; update < 4096; update++)
                {
                    if (update == 0) sound.Tick();
                    else sound.Driver.Update();
                    int offset = 0;
                    for (int address = 0xc014; address < 0xc085; address++) snapshot[offset++] = (byte)sound.Driver.ReadState(address);
                    for (int address = 0xffd8; address < 0xfffe; address++) snapshot[offset++] = (byte)sound.Driver.ReadState(address);
                    hash.AppendData(snapshot);
                }
                string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                string[] row = expected[id].Trim().Split('\t');
                FailIf(row[0] != id.ToString("x2") || row[1] != actual,
                    $"Sound ${id:x2} diverged from independently executed clean-ROM state: expected {row[1]}, got {actual}.");
                sound.PlaySound(0xf1);
                sound.PlaySound(0xf0);
                sound.Tick();
                FailIf(Enumerable.Range(0, 8).Any(i => sound.Channel(i).Active),
                    $"Sound ${id:x2} retained an active channel after both stop controls.");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Clean-US sound ${id:x2} catalog playback failed.", error);
            }
        }
        GD.Print("Matched independent clean-ROM WRAM/HRAM traces for all 223 sounds over 4096 driver updates each, plus stop controls.");
    }

    private void ValidateSoundApplicationBatching()
    {
        var data = new OracleSoundData();
        using var individualFixture = new SoundValidationFixture(data, true);
        using var batchFixture = new SoundValidationFixture(data, true);
        var individual = individualFixture.Sound;
        var batched = batchFixture.Sound;
        var individualScheduler = new ApplicationFixedUpdateScheduler();
        var batchScheduler = new ApplicationFixedUpdateScheduler();
        int individualUpdate = 0, batchUpdate = 0;
        void Advance(OracleSoundEngine sound, ref int update)
        {
            // Same ordering as GameRoot.AdvanceApplicationUpdate: gameplay
            // requests first, audio boundary last; include disable/cancel/reuse.
            switch (update++)
            {
                case 0: sound.PlaySound(0x11); break;
                case 1: sound.PlaySound(0x54); break;
                case 2: sound.SetMusicVolume(1); break;
                case 3: sound.PlaySound(0xf5); break;
                case 4: sound.PlaySound(0x5f); break;
                case 5: sound.PlaySound(0xf6); break;
                case 7: sound.PlaySound(0xf1); break;
                case 8: sound.PlaySound(0xfa); break;
                case 9: sound.PlaySound(0x57); break;
            }
            sound.AdvanceApplicationUpdate();
        }
        for (int i = 0; i < 12; i++) individualScheduler.Advance(1.0 / 60, () => Advance(individual, ref individualUpdate));
        batchScheduler.Advance(12.0 / 60, () => Advance(batched, ref batchUpdate));
        for (int address = 0xc014; address < 0xc085; address++)
            FailIf(individual.Driver.ReadState(address) != batched.Driver.ReadState(address),
                $"Sound state ${address:x4} depends on host-frame batching.");
        // Inspect the actual queued output, keeping audit access in validation.
        var field = typeof(OracleSoundEngine).GetField("_samples", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var left = (Queue<Vector2>)field.GetValue(individual)!;
        var right = (Queue<Vector2>)field.GetValue(batched)!;
        FailIf(left.Count < 8800 || !left.SequenceEqual(right) || left.All(s => s == Vector2.Zero),
            "Batched application updates dropped intermediate sound or produced different PCM samples.");

        int[]? gameplayTrace = null;
        foreach (bool batchGameplay in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetMakuTreeState(3);
            _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
            LoadValidationRoom(0, 0x38);
            _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
            _inventory.SetScriptedEquippedItems(InventoryState.ItemNone, InventoryState.ItemSword);
            _player.WarpTo(new Vector2(136, 40));
            FailIf(_collision.Collides(_player.Position),
                "Sword/audio fixture must stand on clear room 0:$38 geometry at ($88,$28).");
            _sound.RestartSound();
            ValidationSoundRequestAudit audit = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batchGameplay);
            FailIf(!_player.IsAttacking || audit.Requests.Count == 0 ||
                !Enumerable.Range(0, 8).Any(i => _sound.Channel(i).Active),
                "The actual gameplay loop must consume sword input, enqueue its source sound and execute the first note in the same update: " +
                $"batch={batchGameplay}, position={_player.Position}, attacking={_player.IsAttacking}, sounds={string.Join(',', audit.Requests)}, swordLevel={_inventory.SwordLevel}, dialogue={_dialogue.IsOpen}, event={_roomEvents.Active}, swordDisabled={_entities.PlayerSwordDisabled}, physics={_player.IsPhysicsProcessing()}.");
            StepGameplayUpdates(40, Vector2.Zero, batched: batchGameplay);
            FailIf(_player.IsAttacking, "Sword action must complete before its sound re-entry check.");
            int requests = audit.Requests.Count;
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batchGameplay);
            FailIf(!_player.IsAttacking || audit.Requests.Count <= requests,
                "A completed sword action must be able to request another sound through the gameplay loop.");
            int[] trace = audit.Requests.Concat(Enumerable.Range(0, 8)
                .SelectMany(i => new[] { _sound.Channel(i).Priority,
                    _sound.Channel(i).Active ? _sound.Channel(i).WaitFrames : 0 })).ToArray();
            if (gameplayTrace is not null)
                FailIf(!gameplayTrace.SequenceEqual(trace),
                    "Actual sword gameplay requests and sound counters depend on host-frame batching.");
            gameplayTrace = trace;
        }
        GD.Print("Validated identical driver state and PCM for twelve individual versus batched application updates.");
    }
}
