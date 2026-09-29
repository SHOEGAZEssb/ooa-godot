using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs source loading work between input polls. LCD and sound timer time keep
/// advancing while the foreground is busy; host frames can stop inside a load.
/// </summary>
internal sealed class OracleApplicationTiming : IDisposable
{
    private readonly OracleSoundEngine _sound;
    private readonly Action<bool> _setLcd;
    private readonly OracleExecutionClock _clock;
    private readonly OracleVideoPresentation _video;
    private IEnumerator<TimingOperation>? _work;
    private TimingOperation _operation;
    private long _remaining;
    private bool _hasOperation;
    private long _lcdOrigin;
    private long _nextVBlank = long.MaxValue;
    private bool _lcdEnabled;
    private bool _vblankMasked;
    private bool _filesVerified;

    internal long Clocks => _clock.Clocks;
    internal long TimerTicks => _clock.TimerTicks;
    internal bool Busy => _work is not null;
    internal long CompletedUpdates { get; private set; }

    internal OracleApplicationTiming(OracleSoundEngine sound, Action<bool> setLcd, Action<bool>? displayBlank = null)
    {
        _sound = sound;
        _setLcd = setLcd;
        _video = new OracleVideoPresentation(displayBlank ?? (_ => { }));
        // Clean-US cold start: enableTimer's TAC write, then the first
        // _mainLoop input boundary. DIV is free-running across restarts.
        _clock = new OracleExecutionClock(correction => sound.RunTimerInterrupt(correction, clockHardware: true),
            clocks: 1_164_232, advanceHardware: sound.AdvanceHardwareClocks);
        _clock.EnableTimer();
        _clock.ConsumeCpuWork(1_171_344 - _clock.Clocks);
    }

    internal void StagePresentation(IEnumerable<Action> actions, bool loading) => _video.Stage(actions, loading);

    internal void BeginUpdate(string? loading, IReadOnlyList<Action> requests,
        Func<int, OracleSaveData?> load, bool palettesDirty, string? enteredName = null)
    {
        if (Busy) throw new InvalidOperationException("Input was polled during original loading work.");
        _work = ExecuteUpdate(loading, requests, load, palettesDirty, enteredName).GetEnumerator();
    }

    internal void AdvanceTo(long deadline)
    {
        deadline &= ~3L;
        while (_work is not null)
        {
            _video.AdvanceTo(Clocks);
            if (!_hasOperation)
            {
                if (!_work.MoveNext())
                {
                    _work.Dispose(); _work = null; CompletedUpdates++;
                    _video.CompleteLoading();
                    return;
                }
                _operation = _work.Current;
                _remaining = _operation.Clocks;
                _hasOperation = true;
            }
            if (_operation.Wait)
            {
                _clock.WaitUntil(Math.Min(_remaining, deadline));
                _video.AdvanceTo(Clocks);
                if (Clocks < _remaining) return;
            }
            else
            {
                if (Clocks >= deadline && _remaining != 0) return;
                long cpuDeadline = _lcdEnabled && !_vblankMasked ? Math.Min(deadline, _nextVBlank) : deadline;
                _clock.ConsumeUntil(ref _remaining, cpuDeadline);
                _video.AdvanceTo(Clocks);
                if (_lcdEnabled && !_vblankMasked && Clocks >= _nextVBlank)
                {
                    // wVBlankChecker is not $ff while foreground work runs:
                    // vblankInterrupt updates registers, then skips palettes
                    // and OAM. Native $0040 entry through RETI costs 448.
                    _clock.ConsumeUninterruptible(20 + 448);
                    _nextVBlank += OracleExecutionClock.LcdFrameClocks;
                }
                if (_remaining > 0 && Clocks < deadline) continue;
                if (_remaining > 0) return;
            }
            _hasOperation = false;
        }
    }

    internal void CompleteUpdate() => AdvanceTo(long.MaxValue - 4);

    private IEnumerable<TimingOperation> ExecuteUpdate(string? loading, IReadOnlyList<Action> requests,
        Func<int, OracleSaveData?> load, bool palettesDirty, string? enteredName)
    {
        int paletteWork;
        int vblankWork = 0;
        if (loading is not null)
        {
            bool textbox = loading.StartsWith("textbox-", StringComparison.Ordinal);
            if (textbox)
                foreach (Action request in requests) request();
            // Main-loop/thread dispatch paths to the initializer. These are
            // original instruction costs, not movie-frame or timer counts.
            yield return Cpu(loading switch { "capcom" => 1512, "title" => 1408, "files" => 2552,
                // _mainLoop -> pollInput -> resume the existing file thread
                // at $00:$1a1f. The initial thread-start clear is not repeated.
                "new-file-options" or "name-entry" or "name-commit" or "files-return" or "file-select" or "file-start" => 612,
                "pregame-start" or "pregame-start-empty" or "pregame-start-negative" => 596,
                "pregame-graphics" => 0, // Resume the suspended graphics stack, without a game dispatch.
                "pregame-text" => 0, // Plan includes the main handler and newly started text thread.
                "arrival-init" or "arrival-load" => 0,
                _ when textbox => 0,
                _ => throw new InvalidOperationException($"Unsupported loading owner {loading}.") });
            var plan = loading switch
            {
                "files" => OracleLoadingWork.Shared.Files(load, _filesVerified),
                "files-return" => OracleLoadingWork.Shared.ReturnToFiles(load),
                "name-commit" => OracleLoadingWork.Shared.CommitName(enteredName!),
                "name-entry" => OracleLoadingWork.Shared.EnterName(load),
                _ when textbox => TextboxPlan(loading),
                _ => OracleLoadingWork.Shared.Plan(loading)
            };
            if (loading == "files") _filesVerified = true;
            foreach (LoadingStep step in plan)
            {
                switch (step.Kind)
                {
                    case "cpu": yield return Cpu(step.Value); break;
                    case "vblank-work": vblankWork += step.Value; break;
                    case "timer":
                        if ((step.Value & 4) != 0) _clock.EnableTimer(); else _clock.DisableTimer();
                        break;
                    case "sound-stop":
                        long before = _sound.Driver.CpuCycles;
                        _sound.RestartSound();
                        _clock.AccountSoundWork(_sound.Driver.CpuCycles - before);
                        break;
                    case "sound": _sound.PlaySound(step.Value); break;
                    case "lcd-off":
                        foreach (TimingOperation operation in DisableLcd()) yield return operation;
                        break;
                    case "lcd":
                        SetLcd((step.Value & 0x80) != 0);
                        break;
                    case "gfx": case "dma-gfx": break; // source boundary metadata
                    default: throw new InvalidOperationException($"Unsupported loading operation {step.Kind}.");
                }
            }
            // PALH_05 dirties BG $7d / OBJ $7f; title/capcom fade all palettes.
            // fileSelectMode5 retains its palettes while replacing graphics.
            paletteWork = loading switch
            {
                "files" or "files-return" or "name-entry" => 13 * 184 + 56,
                "new-file-options" or "name-commit" or "file-select" or "pregame-graphics" => 0,
                "pregame-text" => 184 + 56, // PALH_0e: BG palette 1, skipping palette 0.
                _ when textbox => 184 + 56,
                _ => 16 * 184
            };
        }
        else
        {
            foreach (Action request in requests) request();
            paletteWork = palettesDirty ? 16 * 184 : 0;
        }
        if (!_lcdEnabled) throw new InvalidOperationException("Original update ended without enabling the LCD.");
        while (_nextVBlank < Clocks) _nextVBlank += OracleExecutionClock.LcdFrameClocks;
        yield return new TimingOperation(_nextVBlank, Wait: true);
        _nextVBlank += OracleExecutionClock.LcdFrameClocks;
        // VBlank masks timer service until RETI. The resumed HALT/main-loop
        // path consumes another 36 clocks with interrupts enabled.
        _clock.ConsumeUninterruptible(24 + 1740 + paletteWork + vblankWork);
        yield return Cpu(36);
    }

    private IEnumerable<TimingOperation> DisableLcd()
    {
        if (!_lcdEnabled) { yield return Cpu(36); yield break; }
        _vblankMasked = true;
        yield return Cpu(76);
        while (true)
        {
            yield return Cpu(12); // LDH LY
            long lcdPosition = (Clocks - 4 - _lcdOrigin) % OracleExecutionClock.LcdFrameClocks;
            int line = (int)(lcdPosition / 912);
            // CGB double-speed LY leads ordinary line boundaries by four
            // clocks and resets eight clocks into the final scanline.
            // (Gambatte LCD::getLyReg; confirmed by native LDH LY reads.)
            // An audio ISR
            // can consume the entire LY >= $91 window, forcing this source
            // polling loop to wait another frame before disabling the LCD.
            if (lcdPosition >= 153 * 912 + 8) line = 0;
            else if (line < 153 && lcdPosition % 912 >= 908) line++;
            yield return Cpu(8); // CP 145
            yield return Cpu(line < 145 ? 12 : 8);
            if (line >= 145) break;
        }
        yield return Cpu(84); // source work through the LCDC write
        SetLcd(false);
        yield return Cpu(56); // clear IF, restore IE/BC, RET
    }

    private void SetLcd(bool enabled)
    {
        _video.SetLcd(enabled, Clocks - 4);
        if (enabled && !_lcdEnabled)
        {
            _vblankMasked = false;
            // Import bus callbacks follow the write's final four clocks.
            _lcdOrigin = Clocks - 4;
            _nextVBlank = _lcdOrigin + 144 * 912;
        }
        _lcdEnabled = enabled;
        _setLcd(enabled);
    }

    private static TimingOperation Cpu(long clocks) => new(clocks, false);
    private static IReadOnlyList<LoadingStep> TextboxPlan(string loading)
    {
        string[] fields = loading.Split('-');
        return OracleTextboxLoadingWork.Shared.Plan(
            Convert.ToInt32(fields[1], 16), int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture));
    }
    public void Dispose() => _work?.Dispose();
    private readonly record struct TimingOperation(long Clocks, bool Wait);
}
