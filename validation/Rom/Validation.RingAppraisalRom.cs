using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingAppraisalTextSpeedsRom() => RunRingAppraisalRom(true, true);

    private void RunRingAppraisalRom(bool compareFrames, bool otherTextSpeeds = false)
    {
        int hostCase1 = 0;
        foreach (int textSpeed in otherTextSpeeds ? Enumerable.Range(0, 5) : new[] { 4 })
        foreach (int kind in new[] { 0, 1, 2, 3, 4 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            if (textSpeed != 4 && kind != 0) continue;
            // First free appraisal, paid new/duplicate, insufficient money,
            // and the hundredth appraisal. Native text thread drives the gate.
            var save = OracleSaveData.CreateStandardGame();
            save.SetTextSpeed(textSpeed);
            if (kind != 0) save.SetGlobalFlag(8);
            save.WriteWramByte(WramAddress.wRingBoxLevel, 1);
            save.WriteWramByte(WramAddress.wNumRupees, (byte)(kind == 3 ? 0x19 : 0x50));
            save.WriteWramByte(WramAddress.wNumRingsAppraised, (byte)(kind == 4 ? 99 : 0));
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.GiveUnappraisedRing(7);
            if (kind == 2) _inventory.GrantAppraisedRingForDebug(7);
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareFrames)
            {
                rom.EnableVramDmaTransfers();
                rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu();
            }
            bool completed = false;
            _ringMenu.OpenImmediatelyForValidation(RingMenuMode.Appraisal, () => completed = true);
            rom.OpenImmediately(4);
            // State 0 clears the inventory name; the first shared gameplay
            // dispatch below opens the appraisal prompt on both sides.
            rom.AdvanceText();
            var sounds = _sound.AttachPlayRequestAudit();
            rom.ClearSounds();
            int update = 0;
            void Step(int count = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette();
                    rom.Update(edge, pressed, ++update);
                    if (compareFrames) rom.PublishHudGraphics();
                    rom.AdvanceText();
                    edge = 0;
                    if (compareFrames && _ringMenuScreen.Visible)
                        CompareRingFramePixelsRom(rom, RingMenuMode.Appraisal,
                            $"Ring Appraisal frame kind={kind} speed={textSpeed} update={update} batch={batched} text=${rom.TextState:x2}/${rom[0xcba0]:x2} glyphs={_dialogue.VisibleGlyphCount} message='{_dialogue.CurrentMessage.Replace('\n', '|')}'");
                    int money = (rom[0xc6ae] & 15) * 100 + (rom[0xc6ad] >> 4) * 10 + (rom[0xc6ad] & 15);
                    FailIf(_inventory.Rupees != money || _inventory.RingsAppraised != rom[0xc6ce] ||
                        _inventory.HasAppraisedRing(7) != ((rom[0xc616] & 0x80) != 0),
                        $"Appraisal kind={kind} update {update} native state=${rom[0xcbce]:x2}, text=${rom.TextState:x2}: money/count/owned runtime={_inventory.Rupees}/{_inventory.RingsAppraised}/{_inventory.HasAppraisedRing(7)}, ROM={money}/{rom[0xc6ce]}/{((rom[0xc616] & 0x80) != 0)}.");
                    int displayedMoney = (rom[0xcbe6] & 15) * 100 +
                        (rom[0xcbe5] >> 4) * 10 + (rom[0xcbe5] & 15);
                    FailIf(_statusBar.DisplayedRupees != displayedMoney ||
                        _statusBar.DisplayedHealth != rom[0xcbe4],
                        $"Appraisal kind={kind} update {update}: HUD money/health animation differs from native runRingMenu.");
                    FailIf(_ringMenu.IsActive != (rom[0xcbcb] != 0),
                        $"Appraisal kind={kind} update {update}: close runtime={_menuLifecycle.CurrentPhase}, ROM=${rom[0xcbcb]:x2}/${rom[0xcbcc]:x2}, palette=${rom[0xc4ab]:x2}, text=${rom.TextState:x2}.");
                    FailIf(_saveData.HasGlobalFlag(9) != ((rom[0xc6d1] & 2) != 0),
                        "Hundredth appraisal did not publish GLOBALFLAG $09 at the native boundary.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Appraisal kind={kind} update {update}: sounds {string.Join(',', sounds.Requests)} != ROM {string.Join(',', rom.Sounds)}.");
                });
            }
            Step(60);
            if (kind == 0)
            {
                Step(1, 2); // First appraisal cannot be canceled without a box.
                Step(60);
            }
            Step(1, 1); // Select the unidentified ring.
            Step(100); // Let the confirmation cursor become ready.
            if (otherTextSpeeds)
            {
                for (int limit = 0; limit < 1000 && !_dialogue.ChoiceCursorVisible; limit++) Step();
                FailIf(!_dialogue.ChoiceCursorVisible, $"Appraisal kind={kind} speed={textSpeed} choice did not reach its bounded input gate.");
            }
            Step(1, 1); // Confirm Yes; payment/reveal occurs after text closes.
            Step(40);
            for (int limit = 0; limit < 1000 && _menuLifecycle.CurrentPhase == Phase.Open; limit++)
            {
                // Repeated edges permit reveal/continuation/close; they cannot
                // bypass preparation, option or ordinary text closing updates.
                Step(1, 1);
                Step(1);
            }
            FailIf(_menuLifecycle.CurrentPhase != Phase.ClosingFadeOut,
                "Appraisal failed to reach its native exit within the bounded sequence.");
            for (int tick = 0; tick < 22 && !completed; tick++) Step();
            FailIf(!completed || _gameplayPause.IsLeased,
                "Appraisal close did not return the callback/pause lease.");
            int expectedMoney = kind switch { 0 => 50, 2 => 60, 3 => 19, _ => 30 };
            int expectedCount = kind == 3 ? 0 : kind == 4 ? 100 : 1;
            FailIf(_inventory.Rupees != expectedMoney || _inventory.RingsAppraised != expectedCount ||
                _inventory.UnappraisedRingCount != (kind == 3 ? 1 : 0),
                $"Appraisal kind={kind} did not establish its independently traced payment/refund/count/removal result.");
            if (kind != 0)
            {
                // Re-entry, No confirmation, and B cancellation retain the
                // unidentified entry and cannot debit another appraisal fee.
                if (kind != 3) _inventory.GiveUnappraisedRing(8);
                rom.CopySave(_saveData);
                completed = false;
                _ringMenu.OpenImmediatelyForValidation(RingMenuMode.Appraisal, () => completed = true);
                rom.OpenImmediately(4);
                rom.AdvanceText();
                sounds.Clear();
                rom.ClearSounds();
                Step(60);
                Step(1, 1);
                Step(100);
                if (otherTextSpeeds)
                {
                    for (int limit = 0; limit < 1000 && !_dialogue.ChoiceCursorVisible; limit++) Step();
                    FailIf(!_dialogue.ChoiceCursorVisible, $"Repeated appraisal kind={kind} speed={textSpeed} choice did not reach its bounded input gate.");
                }
                Step(1, 2);
                Step(1, 1);
                Step(60);
                FailIf(_inventory.Rupees != expectedMoney || _inventory.RingsAppraised != expectedCount ||
                    _inventory.UnappraisedRingCount != 1, "No appraisal confirmation changed inventory.");
                Step(1, 2);
                Step(22);
                FailIf(!completed || _gameplayPause.IsLeased,
                    "Repeated appraisal cancellation lost its completion callback.");
            }
        }
        GD.Print("Validated clean-US free/paid/new/duplicate/insufficient/hundredth appraisal text gates, transactions, refund/exit waits and callback handoff through split/batched application updates.");
    }
}
