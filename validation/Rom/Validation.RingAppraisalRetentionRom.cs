using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingAppraisalRetentionRom() => RunRingAppraisalRetentionRom(false);
    private void ValidateRingAppraisalRepeatedRemovalRom() => RunRingAppraisalRetentionRom(true);

    private void RunRingAppraisalRetentionRom(bool repeated)
    {
        int hostCase1 = 0;
        foreach (int count in repeated ? new[] { 17, 33, 64 } : new[] { 2, 16, 17, 32, 33, 64 })
        foreach (bool last in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(8); save.SetTextSpeed(4);
            save.WriteWramByte(WramAddress.wRingBoxLevel, 3);
            save.WriteWramByte(0xc6ad, repeated ? (byte)0x99 : (byte)0x50);
            InitializeTransientSession(save); LoadValidationRoom(0, 0x45);
            for (int index = 0; index < count; index++) _inventory.GiveUnappraisedRing((index + 1) & 0x3f);
            int removals = repeated ? 3 : 1;
            int selected = last ? count - removals : 0;
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.EnableVramDmaTransfers(); rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu();
            bool completed = false;
            _ringMenu.OpenImmediatelyForValidation(RingMenuMode.Appraisal, () => completed = true);
            rom.OpenImmediately(4); rom.AdvanceText();
            int update = 0;
            string context = $"Appraisal retention count=${count:x2} selected=${selected:x2} removals={removals} batch={batched}";
            void Step(int updates = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(updates, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette(); rom.Update(edge, pressed, ++update);
                    rom.PublishHudGraphics(); rom.AdvanceText(); edge = 0;
                    for (int index = 0; index < 64; index++)
                        FailIf(_inventory.UnappraisedRingAt(index) != rom[0xc5c0 + index],
                            context + $" update={update} state=${rom[0xcbce]:x2}: slot ${index:x2} runtime=${_inventory.UnappraisedRingAt(index):x2}, native=${rom[0xc5c0 + index]:x2}.");
                    FailIf(_saveData.ReadWramByte(WramAddress.wNumUnappraisedRingsBcd) != rom[0xc6cd],
                        context + $" update={update}: retained unappraised BCD count differs.");
                    FailIf(_ringMenuScreen.PageCount != rom[0xcbb5] || _ringMenuScreen.RequestedPage != rom[0xcbb6],
                        context + $" update={update}: retained page/count differs, runtime={_ringMenuScreen.RequestedPage}/{_ringMenuScreen.PageCount}, native={rom[0xcbb6]}/{rom[0xcbb5]}.");
                    if (_ringMenuScreen.Visible)
                        CompareRingFramePixelsRom(rom, RingMenuMode.Appraisal, context + $" update={update}");
                });
            }
            Step(60);
            for (int page = 0; page < selected / 16; page++) { Step(1, 4); Step(22); }
            if ((selected & 15) >= 8) { Step(1, 0x80); Step(); }
            for (int column = 0; column < (selected & 7); column++) { Step(1, 0x10); Step(); }
            for (int removal = 0; removal < removals; removal++)
            {
                if (removal != 0) { Step(1, 0x10); Step(); }
                int selectedRing = (selected + removal + 1) & 0x3f;
                Step(60); Step(1, 1); Step(100); Step(1, 1); Step(40);
                for (int limit = 0; limit < 500 && !_inventory.HasAppraisedRing(selectedRing); limit++)
                { Step(1, 1); Step(); }
                FailIf(!_inventory.HasAppraisedRing(selectedRing) || rom[0xc5c0 + selected + removal] != 0xff,
                    context + $": appraisal failed to remove entry ${selected + removal:x2}.");
                // State 3 leaves a hole. State 4 publishes BCD after its
                // result delay, without changing cursor or page capacity.
                for (int limit = 0; limit < 500 && rom[0xcbce] != 0; limit++) { Step(1, 1); Step(); }
                FailIf(rom[0xcbce] != 0 || _inventory.UnappraisedRingCount != count - removal - 1,
                    context + ": result delay failed to return to Browse with the removed rings.");
                if (repeated)
                {
                    Step(60); Step(1, 1); Step(4);
                    FailIf(rom[0xcbce] != 0 || _inventory.UnappraisedRingCount != count - removal - 1,
                        context + ": confirming a retained empty cell began another appraisal.");
                }
            }
            int expectedPages = (count + 15) / 16;
            FailIf(_ringMenuScreen.PageCount != expectedPages, context + ": removal changed the source's retained page count.");
            Step(60); Step(1, 2); Step(22);
            FailIf(!completed || _gameplayPause.IsLeased, context + ": cancellation failed to release ownership.");
            completed = false;
            _ringMenu.OpenImmediatelyForValidation(RingMenuMode.Appraisal, () => completed = true);
            rom.SaveGraphicsBeforeMenu(); rom.OpenImmediately(4); rom.AdvanceText();
            Step(60);
            FailIf(_ringMenuScreen.PageCount != (count - removals + 15) / 16,
                context + ": reopening did not realign entries and recalculate pages.");
            Step(1, 2); Step(22);
            FailIf(!completed || _gameplayPause.IsLeased, context + ": repeated cancellation failed.");
        }
        GD.Print($"Validated {(repeated ? 7 : 13)} clean-US appraisal capacity/first-last removal cases: repeated={repeated}, retained raw holes, empty-cell confirmation, BCD publication, source page counts, result-delay redraw, both scroll buffers, reopening realignment and complete frame pixels in representative split/batched gameplay.");
    }
}
