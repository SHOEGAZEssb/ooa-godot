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
            // eraseFile/loadFile/font work runs with the LCD enabled. Native
            // movie frames 332-336 retain new-file options, then 337-338 blank.
            FailIf(split._mainMenuScreen.CurrentPage != Page.NewFileOptions || !split._mainMenuScreen.OriginalLcdEnabled,
                "Name entry flashed over the old screen during its LCD-enabled save/font work.");
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
                FailIf(root._mainMenuScreen.CurrentPage != Page.NameEntry || !root._mainMenuScreen.OriginalLcdEnabled,
                    "Name-entry graphics did not become visible after loading.");
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
            FailIf(split._mainMenuScreen!.CurrentPage != Page.FileSelect || split._mainMenuScreen.TextSpeedCursorVisible,
                "Completing the file load exposed its queued tilemap before the next completed scanout.");
            // Native movie frame 358 ends before the panel's first scanout.
            // The next input must nevertheless act on the loaded text speed.
            split.Sample("move_right");
            split.AdvanceTimedApplication((50_486_300 - 50_357_232) / (double)OracleExecutionClock.CpuClocksPerSecond);
            FailIf(split._mainMenuScreen.CurrentPage != Page.FileSelect || split._mainMenuScreen.TextSpeedCursorVisible,
                "Text-speed input prematurely revealed the queued message-speed panel.");
            split.AdvanceTimedApplication((50_495_796 - 50_486_300) / (double)OracleExecutionClock.CpuClocksPerSecond);
            FailIf(split._sound.Channel(2).WaitFrames != 2 || split._originalTiming.Clocks != 50_495_796,
                "Moving from initial text speed $02 to $03 omitted SND_MENU_MOVE.");
            FailIf(split._mainMenuScreen.CurrentPage != Page.TextSpeed || split._mainMenuScreen.TextSpeed != 2 ||
                split._mainMenuScreen.TextSpeedCursorVisible,
                "The panel must precede the next update's text-speed cursor/OAM publication.");
            split.Sample("inventory");
            split.AdvanceTimedApplication((50_767_196 - 50_495_796) / (double)OracleExecutionClock.CpuClocksPerSecond);
            FailIf(!split._originalTiming.Busy || !split._mainMenuScreen.TextSpeedCursorVisible ||
                split._mainMenuScreen.TextSpeed != 3 || split._mainMenuScreen.WhiteFadeOffset != 0,
                "The completed cursor frame did not retain text speed $03 while the file save was still running.");
            split.AdvanceTimedApplication((50_920_084 - 50_767_196) / (double)OracleExecutionClock.CpuClocksPerSecond);
            FailIf(split.SavedSlot(0)?.TextSpeed != 0x03 || split._originalTiming.Clocks != 50_920_084 ||
                split._originalTiming.TimerTicks != 353,
                "Starting a file omitted the text-speed save or its elapsed sound interrupts.");
            split.Step(31);
            // Executed snapshot 327 has paletteFadeHandler01 stopped at $20;
            // the last visible RGB5 offset remains $1f, with no new upload.
            FailIf(!split._mainMenu!.IsActive || split._mainMenu.PaletteWorkPending ||
                split._sound.Channel(0).WaitFrames != 32,
                "File launch skipped its handoff or uploaded a palette after the source fade stopped.");
            split.Step(1);
            FailIf(!split._mainMenu.IsActive || split._mainMenu.PaletteWorkPending ||
                split._originalTiming.Clocks != 55_411_476 || split._sound.Channel(0).WaitFrames != 31,
                "The completed fade must retain the file thread until its next update without uploading palettes.");
            split.Step(1);
            FailIf(split._mainMenu is not null || split._sound.Channel(2).Active ||
                split._sound.Channel(6).Active || split._sound.Channel(0).WaitFrames != 8,
                "mainThreadStart must reset SFX as well as music before the pregame cue.");
            NewGameIntroScreen intro = split.GetNode<NewGameIntroScreen>("NewGameIntro");
            FailIf(split._originalTiming.TimerTicks != 390 || !intro.DisplayBlank,
                "Pregame initialization skipped loading interrupts or revealed its discarded LCD frame.");
            FailIf(split._saveData.ReadWramByte(0xc622) != 1 || split.SavedSlot(0)!.ReadWramByte(0xc622) != 0,
                "The first mainThread update must tick the live playtime without committing it to the file slot.");
            split.Step(1);
            FailIf(intro.DisplayBlank,
                "Pregame initialization retained white after the next complete LCD frame.");
            FailIf(split._saveData.ReadWramByte(0xc622) != 1,
                "The graphics continuation incorrectly started a second mainThread iteration.");
            split.Step(1);
            FailIf(split._saveData.ReadWramByte(0xc622) != 2,
                "The main thread did not resume playtime after the graphics continuation.");
            split.Step(358);
            FailIf(!intro.Dialogue.IsOpen || split._originalTiming.TimerTicks != 751 ||
                split._sound.Channel(0).WaitFrames != 2 || split._sound.Channel(1).WaitFrames != 7 ||
                split._sound.Channel(4).WaitFrames != 7 || split._saveData.ReadWramByte(0xc622) != 0x68 ||
                split._saveData.ReadWramByte(0xc623) != 1,
                "Opening TX_1213 must cross the extra source VBlank/audio tick without adding a playtime update.");
            split._sound.AttachPlayRequestAudit();
            int textSounds = split._sound.PlayRequestsFor(SoundId.SndText);
            split.Step(1, "item");
            FailIf(intro.Dialogue.VisibleGlyphCount != 0 || split._sound.PlayRequestsFor(SoundId.SndText) != textSounds,
                "The first text-row preparation update accepted B or played a glyph sound.");
            split.Step(1, "attack");
            FailIf(intro.Dialogue.VisibleGlyphCount != 10 || split._sound.PlayRequestsFor(SoundId.SndText) != textSounds + 1,
                "The following printing update failed to reveal the first row on A.");
            split.Step(1, "item");
            FailIf(intro.Dialogue.VisibleGlyphCount != 10,
                "Preparing the lower text row also displayed it on the same input.");
            split.Step(1, "attack");
            FailIf(intro.Dialogue.VisibleGlyphCount != 22,
                "The prepared lower text row did not display on the following input.");
            split.Step(1, "item");
            FailIf(!intro.Dialogue.IsOpen, "The close request skipped the text-thread teardown update.");
            split.Step(1, "attack");
            FailIf(intro.Dialogue.IsOpen || split._sound.PlayRequestsFor(SoundId.SndFairyCutscene) != 0,
                "Closing TX_1213 advanced the earlier Link handler before the next main-thread update.");
            split.Step(1, "item");
            FailIf(split._sound.PlayRequestsFor(SoundId.SndFairyCutscene) != 1,
                "Link failed to start vanishing after the text thread cleared TX_1213.");

            // Commit the other host's name, then cancel and re-enter text speed.
            // Whole-update batching must preserve the same panel/OAM separation
            // and cancel must not commit the temporary speed change.
            batch.Step(1, "inventory");
            batch.Step(1, "attack");
            batch.Step(2);
            batch.Step(1, "inventory");
            batch.Step(1, "move_right");
            FailIf(batch._mainMenuScreen!.CurrentPage != Page.TextSpeed || batch._mainMenuScreen.TextSpeedCursorVisible,
                "Whole-update stepping collapsed the panel and cursor publications.");
            batch.Step(1, "map");
            FailIf(batch._mainMenu!.CurrentPage != Page.FileSelect || batch.SavedSlot(0)?.TextSpeed != 2,
                "Cancel committed text speed or left logical input on the pending display page.");
            batch.Step(1, "move_down");
            FailIf(batch._mainMenuScreen.CurrentPage != Page.FileSelect || !batch._mainMenuScreen.TextSpeedCursorVisible ||
                batch._mainMenuScreen.Cursor != 1,
                "The source Back dispatch must retain its final cursor without overwriting the next file-selection input.");
            batch.Step(1, "move_up");
            batch.Step(1, "inventory");
            batch.Step(2);
            FailIf(batch._mainMenu!.CurrentPage != Page.TextSpeed || !batch._mainMenuScreen.TextSpeedCursorVisible ||
                batch._mainMenuScreen.TextSpeed != 2,
                "Re-entering text speed retained a cancelled value or lost its cursor.");
        }
        finally { split.Free(); batch.Free(); }
    }
}
