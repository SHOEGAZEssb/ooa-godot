using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFileMenuInitializationTiming()
    {
        // Independent clean-US instruction trace: fileSelectMode5 state 0
        // loads GFXH_NEW_FILE_OPTIONS ($a7), GFXH_SAVE_MENU_LAYOUT ($a6),
        // then UNCMP_GFXH_08. Costs exclude timer ISR service and LCD waiting.
        var plan = OracleLoadingWork.Shared.Plan("new-file-options");
        FailIf(plan.Count != 11 || plan[0] != new LoadingStep("cpu", 5036) ||
            plan[3] != new LoadingStep("gfx", 0xa7) || plan[4].Value != 317012 ||
            plan[5] != new LoadingStep("gfx", 0xa6) || plan[6].Value != 162704 ||
            plan[7] != new LoadingStep("dma-gfx", 0x08) || plan[8].Value != 6924,
            "fileSelectMode5 state-0 work differs from executed clean-US loader order/costs.");

        using var split = new ExecutionTimingValidationRoot();
        using var batch = new ExecutionTimingValidationRoot();
        AddChild(split);
        AddChild(batch);
        try
        {
            foreach (var root in new[] { split, batch })
            {
                root.InitializeAtFileSelect();
                root.Step(1, "inventory");
                var presented = CaptureTasPresentationState(root);
                FailIf(presented["presentation.menu.visible"] != 1 || presented["presentation.menu.screen"] != 1 ||
                    presented["presentation.menu.overlay"] != 0 || presented["presentation.cursor.acorn.0.y"] != 52,
                    "TAS presentation followed the pending menu instead of the displayed source file-select page.");
                FailIf(root._originalTiming!.Clocks != 45_887_364 || root._originalTiming.TimerTicks != 317 ||
                    root._sound.Channel(0).WaitFrames != 99 ||
                    root._mainMenu!.CurrentPage != Page.NewFileOptions || root._mainMenuScreen!.CurrentPage != Page.FileSelect,
                    "Selecting an empty file must set mode $05 while retaining the old display until the next dispatch.");
            }

            const double loadTime = (46_558_980 - 45_887_364) / (double)OracleExecutionClock.CpuClocksPerSecond;
            // A fresh A/Down/B poll on state 0 must be ignored, as must an
            // input pulse entirely inside the CPU load. The other host uses
            // neutral input and advances the same interval in one batch.
            split.Sample("attack", "item", "move_down");
            const double dispatchTime = 256.0 / OracleExecutionClock.CpuClocksPerSecond;
            split.AdvanceTimedApplication(dispatchTime);
            FailIf(split._mainMenuScreen!.CurrentPage != Page.FileSelect || !split._mainMenuScreen.OriginalLcdEnabled,
                "New-file options flashed before disableLcd blanked the old file-select screen.");
            split.AdvanceTimedApplication(loadTime / 4 - dispatchTime);
            FailIf(!split._mainMenuScreen.DisplayBlank,
                "The completed LCD-off frame must be white during the new-file load.");
            FailIf(CaptureTasPresentationState(split).Count != 1 || CaptureTasPresentationState(split)["presentation.menu.visible"] != 0,
                "TAS presentation exposed menu contents while the displayed frame was blank.");
            FailIf(!split._originalTiming!.Busy || split._originalTiming.CompletedUpdates != 285 ||
                split._sound.Channel(0).WaitFrames >= 99 || split._mainMenu!.CurrentPage != Page.NewFileOptions ||
                split._mainMenu.Cursor != 0,
                "File-menu initialization accepted input or stopped the independent audio timer.");
            split.Sample("map");
            split.AdvanceTimedApplication(loadTime / 4);
            split.Sample();
            split.AdvanceTimedApplication(loadTime / 2);
            batch.Sample();
            batch.AdvanceTimedApplication(loadTime);

            foreach (var root in new[] { split, batch })
            {
                FailIf(root._originalTiming!.Busy || root._originalTiming.Clocks != 46_558_980 ||
                    root._originalTiming.CompletedUpdates != 286 || root._originalTiming.TimerTicks != 322,
                    "fileSelectMode5 must finish at the source LCD boundary after five elapsed sound interrupts.");
                FailIf(root._mainMenuScreen!.CurrentPage != Page.NewFileOptions || !root._mainMenuScreen.OriginalLcdEnabled,
                    "New-file options did not become visible at load completion.");
                foreach (var (channel, wait) in new[] { (0, 94), (1, 10), (2, 3), (4, 4), (6, 3) })
                    FailIf(root._sound.Channel(channel).WaitFrames != wait,
                        $"File-menu load ended with channel {channel} wait other than source ${wait:x2}.");
                root.Step(1);
                FailIf(root._mainMenuScreen.DisplayBlank,
                    "New-file options remained white after the first fully rendered enabled frame.");
                var shown = CaptureTasPresentationState(root);
                FailIf(shown["presentation.menu.screen"] != 2 || shown["presentation.menu.blackLevelRgb5"] != 0 ||
                    shown["presentation.cursor.acorn.0.x"] != 32 || shown["presentation.cursor.acorn.0.y"] != 56,
                    "TAS presentation missed the source options screen/cursor after completed-frame publication.");
                FailIf(root._mainMenu!.CurrentPage != Page.NewFileOptions || root._mainMenu.Cursor != 0,
                    "Input discarded during initialization leaked into state 1.");
            }
            FailIf(split._sound.Apu.Clocks != batch._sound.Apu.Clocks ||
                split._originalTiming!.Clocks != batch._originalTiming!.Clocks,
                "Splitting a file-menu load changed APU or foreground elapsed time.");

            // Cancellation is accepted only after initialization. Re-entering
            // an empty file must run state 0 again, including its input gate.
            split.Step(1, "map");
            FailIf(split._mainMenu!.CurrentPage != Page.FileSelect,
                "The initialized new-file menu no longer accepts Select as Back.");
            split.Step(1);
            split.Step(1, "inventory");
            long before = split._originalTiming!.Clocks;
            split.Sample("attack");
            split.AdvanceTimedApplication(1.0 / 60.0);
            FailIf(!split._originalTiming.Busy || split._mainMenu.CurrentPage != Page.NewFileOptions,
                "Re-entry skipped the new-file initializer or accepted A during it.");
            split._originalTiming.CompleteUpdate();
            FailIf(split._originalTiming.Clocks - before <= OracleExecutionClock.LcdFrameClocks,
                "Re-entering new-file options omitted the blocking source graphics work.");
        }
        finally { split.Free(); batch.Free(); }

        // Independent native replays with one/two neutral movie frames added
        // before the title's second Start. Timer service can cover the entire
        // LY >= $91 interval: LY reads $00 for most of scanline 153, so the
        // loader must wait for a later VBlank instead of exiting on that line.
        foreach (var (delay, ticks, wait) in new[] { (1, 329, 88), (2, 327, 91) })
        {
            using var phased = new ExecutionTimingValidationRoot();
            AddChild(phased);
            try
            {
                phased.InitializeAtFileSelect(delay);
                phased.Step(1, "inventory");
                phased.Step(1);
                FailIf(phased._originalTiming!.CompletedUpdates != 286 + delay ||
                    phased._originalTiming.TimerTicks != ticks || phased._sound.Channel(0).WaitFrames != wait ||
                    phased._mainMenu!.CurrentPage != Page.NewFileOptions,
                    $"New-file load at title delay {delay} missed the source LCD-wait/audio-interrupt boundary.");
            }
            finally { phased.Free(); }
        }
    }
}
