using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOriginalExecutionTiming()
    {
        // Clean-US instruction traces, with complete timer-interrupt service
        // intervals removed. $ba/$a2 decode into banked WRAM: keeping the
        // importer's stack in $dxxx would silently terminate these early.
        OracleLoadingWork work = OracleLoadingWork.Shared;
        // Independent instruction trace at the first Impa textbox:
        // showText is 700 clocks; textThreadStart through its first yield is
        // 106464, excluding timer/VBlank interrupts. Its queued DMA is 1884.
        IReadOnlyList<LoadingStep> textbox = OracleTextboxLoadingWork.Shared.Plan(0x0102, 0);
        FailIf(textbox.Count != 2 || textbox[0] != new LoadingStep("cpu", 107164) ||
            textbox[1] != new LoadingStep("vblank-work", 1884),
            "TX_0102 opening work differs from the clean-US instruction trace.");
        FailIf(work.Graphics(0xa0) != 610584 || work.Graphics(0xba) != 440144 ||
            work.Graphics(0xa2) != 158412,
            "loadGfxHeader CPU work differs from executed clean-US $a0/$ba/$a2 decoder paths.");
        FailIf(work.Plan("files")[10].Value != 1_272_492,
            "loadFileDisplayVariables/font copy lost the cold SRAM verification or untouched WRAM-bank-4 name bytes.");

        using var fixture = new SoundValidationFixture(new OracleSoundData());
        OracleSoundEngine sound = fixture.Sound;
        sound.PlaySound(0x01);
        for (int tick = 0; tick < 10; tick++) sound.Tick();
        sound.PlaySound(0x56);
        sound.PlaySound(0xfa);
        for (int tick = 0; tick < 32; tick++) sound.Tick();
        FailIf(sound.Driver.ReadState(0xc014) != 32 || sound.Channel(0).WaitFrames != 2,
            "Title fade setup differs from the clean-US timer boundary.");

        // Original $0050 entry through RETI. Interrupt dispatch itself adds
        // another 20 clocks. These measured costs vary with channel bytecode.
        foreach (int expected in new[] { 15400, 8856, 12120, 8344, 11492 })
            FailIf(sound.RunTimerInterrupt(correction: false) != expected,
                $"timerInterrupt CPU service cost differs from clean-US {expected} clocks.");
        FailIf(sound.Driver.ReadState(0xc014) != 37,
            "The fade must continue while foreground loading is blocked.");
        sound.PlaySound(0x11);
        FailIf(sound.RunTimerInterrupt(correction: true) != 32580 ||
            sound.Channel(0).WaitFrames != 111,
            "Music queue drain, hFFB8 correction and file-select sequencing lost their source order or CPU work.");

        var clock = new OracleExecutionClock(_ => 424);
        clock.EnableTimer();
        clock.WaitUntil(140291);
        FailIf(clock.TimerTicks != 0, "$77 TIMA must not overflow before the 137th divider edge plus four clocks.");
        clock.ConsumeCpuWork(1);
        FailIf(clock.TimerTicks != 1 || clock.Clocks != 140736 ||
            clock.TimerCorrectionCounter != 0xff,
            "timerInterrupt must suspend foreground work and wrap the initial hFFB8=$00 to $ff.");

        var corrections = new List<bool>();
        var phased = new OracleExecutionClock(correction =>
        {
            corrections.Add(correction);
            return correction ? 460 : 424;
        }, clocks: 500000, correctionCounter: 1);
        phased.EnableTimer();
        FailIf(phased.NextTimer != 640004,
            "enableTimer must preserve DIV's phase rather than start a new 1024-clock divider.");
        phased.WaitUntil(640004);
        FailIf(phased.NextTimer != 781316 || phased.TimerCorrectionCounter != 7,
            "hFFB8 zero must reload TIMA with $76 and retain a seven-interrupt correction period.");
        for (int tick = 0; tick < 7; tick++) phased.WaitUntil(phased.NextTimer);
        FailIf(corrections.Count != 8 || !corrections[0] || !corrections[7] ||
            corrections.GetRange(1, 6).Contains(true),
            "The timer correction is tied to interrupt count, independently of application updates.");
        byte retainedCorrection = phased.TimerCorrectionCounter;
        phased.DisableTimer();
        long retainedTicks = phased.TimerTicks;
        phased.ConsumeCpuWork(500000);
        FailIf(phased.TimerTicks != retainedTicks, "disableTimer must suppress sequencing during source work.");
        phased.EnableTimer();
        FailIf(phased.TimerCorrectionCounter != retainedCorrection,
            "restartSound/enableTimer must preserve hFFB8.");

        var whole = new OracleExecutionClock(correction => correction ? 460 : 424);
        var chunks = new OracleExecutionClock(correction => correction ? 460 : 424);
        whole.EnableTimer();
        chunks.EnableTimer();
        whole.ConsumeCpuWork(40_000_000);
        for (int index = 0; index < 1000; index++) chunks.ConsumeCpuWork(40_000);
        FailIf(whole.Clocks != chunks.Clocks || whole.TimerTicks != chunks.TimerTicks ||
            whole.NextTimer != chunks.NextTimer ||
            whole.TimerCorrectionCounter != chunks.TimerCorrectionCounter,
            "Splitting foreground work across host frames must retain every audio interrupt and its phase.");

        ValidateLoadingApplicationLoop();
    }

    private void ValidateLoadingApplicationLoop()
    {
        using var split = new ExecutionTimingValidationRoot();
        using var batched = new ExecutionTimingValidationRoot();
        AddChild(split);
        AddChild(batched);
        try
        {
            foreach (ExecutionTimingValidationRoot root in new[] { split, batched })
            {
                root.Initialize();
                root.Step(241);
                if (root == split)
                {
                    const double titleLoad = (37_499_532 - 36_017_044) / (double)OracleExecutionClock.CpuClocksPerSecond;
                    const double beforeLcdOff = 256.0 / OracleExecutionClock.CpuClocksPerSecond;
                    root.Sample("inventory");
                    root.AdvanceTimedApplication(beforeLcdOff);
                    FailIf(!root._originalTiming!.Busy || !root._frontendIntroScreen!.Visible ||
                        root._mainMenuScreen!.Visible || !root._frontendIntroScreen.OriginalLcdEnabled,
                        "Title loading exposed its destination before the original disableLcd call.");
                    root.AdvanceTimedApplication(titleLoad / 2 - beforeLcdOff);
                    FailIf(root._frontendIntroScreen.Visible || !root._mainMenuScreen.Visible ||
                        root._mainMenuScreen.OriginalLcdEnabled,
                        "Title graphics must be installed behind the source LCD-off white interval.");
                    root._originalTiming.CompleteUpdate();
                    FailIf(root._originalTiming.Busy || root._originalTiming.CompletedUpdates != 242 ||
                        !root._mainMenuScreen.OriginalLcdEnabled,
                        "Title presentation did not resume after its source graphics load.");
                }
                else root.Step(1, "inventory");
                root.Step(9);
                root.Step(1, "inventory");
                root.Step(31);
                FailIf(root._originalTiming!.Clocks != 43_249_800 ||
                    root._originalTiming.TimerTicks != 299 || root._sound.Driver.ReadState(0xc014) != 32,
                    "Cold-start/title timing did not reach the independently measured file-loading boundary.");
            }
            const double loadingTime = (45_749_364 - 43_249_800) / (double)OracleExecutionClock.CpuClocksPerSecond;
            // A press and release wholly inside the blocked load must never
            // become the next menu input. Timer sequencing must still run.
            split.Sample();
            const double dispatchTime = 256.0 / OracleExecutionClock.CpuClocksPerSecond;
            split.AdvanceTimedApplication(dispatchTime);
            // Native frames 304-325 stay white across this handoff. The file
            // controller exists before disableLcd, but its palettes must not
            // clear the title's white fade and expose the destination early.
            FailIf(split._mainMenu?.CurrentPage != Page.FileSelect ||
                split._mainMenuScreen!.CurrentPage != Page.Title ||
                split._mainMenuScreen.WhiteFadeOffset != 31 || !split._mainMenuScreen.OriginalLcdEnabled,
                "File loading briefly exposed the unfaded file screen before LCD blanking.");
            split.AdvanceTimedApplication(loadingTime / 4 - dispatchTime);
            FailIf(!split._originalTiming!.Busy || split._originalTiming.CompletedUpdates != 283 ||
                split._sound.Driver.ReadState(0xc014) <= 32,
                "Loading failed to block foreground updates while continuing audio.");
            FailIf(split._mainMenuScreen.CurrentPage != Page.FileSelect || split._mainMenuScreen.OriginalLcdEnabled,
                "File graphics must replace the title only behind the original LCD-off white interval.");
            split.Sample("attack");
            split.AdvanceTimedApplication(loadingTime / 4);
            split.Sample();
            split.AdvanceTimedApplication(loadingTime / 2);
            batched.Sample();
            batched.AdvanceTimedApplication(loadingTime);
            foreach (ExecutionTimingValidationRoot root in new[] { split, batched })
            {
                FailIf(root._originalTiming!.Busy || root._originalTiming.CompletedUpdates != 284 ||
                    root._sound.Driver.ReadState(0xc014) != 37 || root._sound.Channel(0).WaitFrames != 100,
                    "Completed source loading differs from clean-US fade $25 / channel-0 wait $64.");
                FailIf(root._mainMenu?.CurrentPage != Page.FileSelect,
                    "A host input edge was consumed before loading completed.");
                FailIf(root._mainMenuScreen!.CurrentPage != Page.FileSelect ||
                    !root._mainMenuScreen.OriginalLcdEnabled || root._mainMenuScreen.WhiteFadeOffset != 0,
                    "Single/batched loading ended with a stale display or retained white override.");
                root.Step(1);
                FailIf(root._mainMenu?.CurrentPage != Page.FileSelect,
                    "A press released during loading leaked into the next original input poll.");
            }
            FailIf(split._originalTiming!.Clocks != batched._originalTiming!.Clocks ||
                split._sound.Apu.Clocks != batched._sound.Apu.Clocks ||
                split._sound.Channel(0).WaitFrames != batched._sound.Channel(0).WaitFrames,
                "Host-frame partitioning changed loading completion, APU time or audio state.");
        }
        finally { split.Free(); batched.Free(); }
    }
}
