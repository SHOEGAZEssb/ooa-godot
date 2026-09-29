using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFileMenuAudioTiming()
    {
        // Expectations below are independent clean-US native instruction and
        // timer traces, not the generated plans consumed by the application.
        OracleLoadingWork work = OracleLoadingWork.Shared;
        FailIf(work.Plan("name-entry")[0] != new LoadingStep("cpu", 461188) ||
            work.CommitName("A\0\0\0\0")[0] != new LoadingStep("cpu", 278644) ||
            work.CommitName("A    ")[0] != new LoadingStep("cpu", 278612) ||
            work.Plan("file-select")[5] != new LoadingStep("vblank-work", 1884),
            "Name save/font conversion or queued file-select VBlank work differs from clean-US execution.");

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
                root.Step(1);
                root.Step(1, "inventory");
                FailIf(root._originalTiming!.Clocks != 46_699_428 ||
                    root._mainMenu!.CurrentPage != Page.NameEntry ||
                    root._mainMenuScreen!.CurrentPage != Page.NewFileOptions,
                    "Selecting New Game must defer name-entry initialization to its next dispatch.");
            }

            const double loadTime = (47_662_152 - 46_699_428) / (double)OracleExecutionClock.CpuClocksPerSecond;
            split.Sample("move_down", "item");
            split.AdvanceTimedApplication(loadTime / 4);
            FailIf(!split._originalTiming!.Busy || split._originalTiming.CompletedUpdates != 287 ||
                split._sound.Channel(0).WaitFrames >= 93 || split._mainMenuScreen!.EnteredName.Length != 0,
                "Name initialization accepted input or stopped sound timer service.");
            split.Sample("map");
            split.AdvanceTimedApplication(loadTime / 4);
            split.Sample();
            split.AdvanceTimedApplication(loadTime / 2);
            batch.Sample();
            batch.AdvanceTimedApplication(loadTime);
            foreach (var root in new[] { split, batch })
            {
                FailIf(root._originalTiming!.Busy || root._originalTiming.CompletedUpdates != 288 ||
                    root._originalTiming.Clocks != 47_662_152 || root._originalTiming.TimerTicks != 330 ||
                    root._sound.Channel(0).WaitFrames != 86 || root._mainMenuScreen!.EnteredName.Length != 0 ||
                    root._mainMenuScreen.NameCursor != 0,
                    "Name-entry work lost LCD-enabled short VBlanks, sound interrupts, or the input gate.");
            }
            FailIf(split._sound.Apu.Clocks != batch._sound.Apu.Clocks,
                "Host-frame subdivision changed name-entry audio time.");

            // Empty confirmation cancels creation, but still runs the mode-2
            // dispatch and next-update file initializer. Re-entry must remain usable.
            batch.Step(1, "inventory");
            batch.Step(1, "attack");
            batch.Step(1);
            FailIf(batch.SavedSlot(0) is not null || batch._mainMenu!.CurrentPage != Page.FileSelect ||
                batch._mainMenuScreen!.CurrentPage != Page.NameEntry,
                "Empty-name cancellation must defer file-select initialization without committing a file.");
            batch.Step(1);
            batch.Step(1, "inventory");
            batch.Step(1);
            batch.Step(1, "inventory");
            batch.Step(1);
            batch.Step(1, "attack");
            FailIf(batch._mainMenuScreen!.EnteredName != "A" || batch.SavedSlot(0) is not null,
                "Re-entry after empty-name cancellation lost name input or committed prematurely.");

            // Actual menu input/persistence handoffs: select 'A', move to OK,
            // accept, commit on the next dispatch, then reload the file screen.
            split.Step(1, "attack");
            split.Step(1, "inventory");
            split.Step(1, "attack");
            FailIf(split.SavedSlot(0) is not null || split._mainMenuScreen!.EnteredName != "A",
                "Name confirmation committed a file before mode2's save dispatch.");
            split.Step(1);
            FailIf(split.SavedSlot(0)?.LinkName != "A" || split.SavedSlot(0)?.TextSpeed != 0x02 ||
                split._originalTiming!.Clocks != 48_502_392 || split._originalTiming.TimerTicks != 336 ||
                split._sound.Channel(0).WaitFrames != 80 || split._mainMenu!.CurrentPage != Page.FileSelect ||
                split._mainMenuScreen!.CurrentPage != Page.NameEntry,
                "Name commit lost the initial text speed, deferred file screen, or source save work.");
            split.Step(1);
            FailIf(split._originalTiming!.Clocks != 50_076_900 || split._originalTiming.TimerTicks != 347 ||
                split._sound.Channel(0).WaitFrames != 69 || split._mainMenuScreen!.CurrentPage != Page.FileSelect,
                "Reloading populated file select omitted its source SRAM/font/graphics work.");
            split.Step(1, "inventory");
            FailIf(split._originalTiming.Clocks != 50_357_232 || split._originalTiming.TimerTicks != 349 ||
                split._mainMenu!.CurrentPage != Page.TextSpeed,
                "Selecting a saved file omitted its queued VBlank display work.");
            split.Step(1, "move_right");
            FailIf(split._sound.Channel(2).WaitFrames != 2 || split._originalTiming.Clocks != 50_495_796,
                "Moving from initial text speed $02 to $03 omitted SND_MENU_MOVE.");
            split.Step(1, "inventory");
            FailIf(split.SavedSlot(0)?.TextSpeed != 0x03 || split._originalTiming.Clocks != 50_920_084 ||
                split._originalTiming.TimerTicks != 353,
                "Starting a file omitted the text-speed save or its elapsed sound interrupts.");
            split.Step(31);
            FailIf(!split._mainMenu!.IsActive || !split._mainMenu.PaletteWorkPending ||
                split._sound.Channel(0).WaitFrames != 32,
                "File launch finished before the 32-step white fade.");
            split.Step(1);
            FailIf(!split._mainMenu.IsActive || split._mainMenu.PaletteWorkPending ||
                split._originalTiming.Clocks != 55_411_476 || split._sound.Channel(0).WaitFrames != 31,
                "The completed fade must retain the file thread until its next update without uploading palettes.");
            split.Step(1);
            FailIf(split._mainMenu is not null || split._sound.Channel(2).Active ||
                split._sound.Channel(6).Active || split._sound.Channel(0).WaitFrames != 8,
                "mainThreadStart must reset SFX as well as music before the pregame cue.");
        }
        finally { split.Free(); batch.Free(); }
    }
}
