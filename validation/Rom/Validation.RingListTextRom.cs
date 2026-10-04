using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingListTextSpeedsRom() => RunRingListTextRom(true, true, true);

    private void RunRingListTextRom(bool comparePixels, bool compareFrames = false, bool otherTextSpeeds = false)
    {
        int hostCase1 = 0;
        foreach (int textSpeed in otherTextSpeeds ? Enumerable.Range(0, 5) : new[] { 4 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wRingBoxLevel, 3);
            save.SetTextSpeed(textSpeed);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.GrantAppraisedRingForDebug(0);
            _inventory.GrantAppraisedRingForDebug(1);
            _inventory.SetRingBoxSlotFromList(0, 0);
            _inventory.SetRingBoxSlotFromList(1, 1);
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareFrames) rom.EnableVramDmaTransfers();
            if (comparePixels) { rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu(); }
            _ringMenu.OpenImmediatelyForValidation(RingMenuMode.List, static () => { });
            rom.OpenImmediately(4, 1);
            rom.AdvanceText(); // State 0 clears the inventory name line.
            var sounds = _sound.AttachPlayRequestAudit();
            rom.ClearSounds();
            int update = 0;
            using Image font = (Image)OracleTileRenderer.BuildMonochromeFontTexture("res://assets/oracle/gfx/gfx_font.png").GetImage().Duplicate();
            byte[] fontPixels = font.GetData();
            int fontStride = font.GetWidth() * 4;
            FailIf(font.GetFormat() != Image.Format.Rgba8, "Ring name comparison requires RGBA font pixels.");
            void Step(int count = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, pressed, ++update);
                    rom.AdvanceText();
                    edge = 0;
                    if (compareFrames)
                        CompareRingFramePixelsRom(rom, RingMenuMode.List,
                            $"Ring List frame speed={textSpeed} update={update} batch={batched}");
                    // Inventory mode composes the title before the standard
                    // description thread reuses this WRAM graphics buffer.
                    if (rom[0xcbcd] == 1 && rom[0xcba1] == 2)
                    {
                        string name = _ringMenuScreen.DisplayedRingNameForValidation;
                        int padding = (16 - name.Length) / 2;
                        for (int column = 0; column < 16; column++)
                        for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int letter = column - padding;
                            int expected = letter >= 0 && letter < name.Length &&
                                fontPixels[((name[letter] >> 4) * 16 + y) * fontStride +
                                    ((name[letter] & 15) * 8 + x) * 4 + 3] > 0 ? 2 : 3;
                            int offset = column * 32 + (y / 8) * 16 + (y & 7) * 2;
                            int actual = ((rom.RingNameGfx(offset) >> (7 - x)) & 1) |
                                ((rom.RingNameGfx(offset + 1) >> (7 - x)) & 1) << 1;
                            if (expected != actual) FailIf(true,
                                $"Ring name update {update} '{name}' pixel {column * 8 + x},{y}: runtime shade {expected} != ROM {actual}.");
                        }
                        if (comparePixels)
                        {
                            // Native inventory text composes all 16 columns
                            // before the description reuses w7's graphics.
                            // Compare the actual renderer upload image here,
                            // with native shades/palette as the expectation.
                            using Image frame = _ringMenuScreen.ComposeImage();
                            byte[] pixels = frame.GetData();
                            for (int y = 0; y < 16; y++)
                            for (int x = 0; x < 128; x++)
                            {
                                int offset = (x / 8) * 32 + (y / 8) * 16 + (y & 7) * 2;
                                int shade = (rom.RingNameGfx(offset) >> (7 - (x & 7)) & 1) |
                                    (rom.RingNameGfx(offset + 1) >> (7 - (x & 7)) & 1) << 1;
                                int expectedColor = rom.HudColor(false, 0, shade);
                                int pixel = ((y + 88) * 160 + x + 16) * 4;
                                int observed = (pixels[pixel] * 31 + 127) / 255 |
                                    ((pixels[pixel + 1] * 31 + 127) / 255) << 5 |
                                    ((pixels[pixel + 2] * 31 + 127) / 255) << 10;
                                if (pixels[pixel + 3] != 255 || observed != expectedColor) FailIf(true,
                                    $"Rendered ring name update={update} '{name}' pixel ({x + 16},{y + 88}): runtime=${observed:x4}, native=${expectedColor:x4}/shade={shade}.");
                            }
                        }
                    }
                    bool description = rom[0xcba1] == 0 && rom[0xcba0] != 0;
                    FailIf(_dialogue.IsOpen != description,
                        $"Ring list text update {update}: panel runtime={_dialogue.IsOpen}, ROM={description}, name=${rom[0xcbbb]:x2}, description=${rom[0xcbc0]:x2}, mode=${rom[0xcba1]:x2}, active=${rom[0xcba0]:x2}, state=${rom.TextState:x2}, menu=${rom[0xcbcd]:x2}/${rom[0xcbce]:x2}, page/cursor/box=${rom[0xcbb6]:x2}/${rom[0xcbb4]:x2}/${rom[0xcbbe]:x2}, runtime={_ringMenuScreen.Page}/{_ringMenuScreen.ListCursor}/{_ringMenuScreen.SelectingList}/{_ringMenuScreen.PageTransitionActive}.");
                    if (description && rom[0xcba0] != 0xff && rom.TextState != 0x10)
                    {
                        int glyphs = Enumerable.Range(0, 0xc0).Count(offset =>
                            rom.Text(0xd000 + offset) is >= 0x40 and < 0x80 &&
                            (rom.Text(0xd100 + offset) & 0x80) != 0 &&
                            (rom.Text(0xd000 + offset) & 1) == 0);
                        FailIf(_dialogue.VisibleGlyphCount != glyphs,
                            $"Ring list description update {update}: glyphs {_dialogue.VisibleGlyphCount} != ROM {glyphs}.");
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Ring list text update {update}: text/menu sound order differs.");
                });
            }
            void SettleDescription(int limit)
            {
                int began = update;
                do { Step(); }
                while ((!_dialogue.PrintingComplete || rom[0xcba0] != 0x80) && update - began < limit);
                FailIf(!_dialogue.PrintingComplete || rom[0xcba0] != 0x80,
                    $"Ring list speed={textSpeed}: native description failed to settle within {limit} updates.");
            }
            SettleDescription(800);
            FailIf(_dialogue.CurrentMessage != "Symbol of a\nmeeting" || !_dialogue.PrintingComplete,
                "Ring $00 lost its source description or non-exitable completion gate.");
            Step(1, 0x10);
            SettleDescription(800);
            Step(1, 1); // Choosing a box slot does not replace its text yet.
            Step(1, 0x10);
            SettleDescription(800);
            Step(1, 0x10); // Unowned ring prints the source's blank description.
            Step(10);
            FailIf(_dialogue.CurrentMessage != string.Empty || !_dialogue.PrintingComplete,
                "An unowned list ring must retain the source's completed blank textbox.");
            Step(1, 2); // $ff restores the panel but retains text ownership until replacement.
            Step(10);
            Step(1, 0x10); // Empty box slot.
            Step(10);
            Step(1, 1);
            Step(1, 4);
            Step(23);
            Step(10);
            for (int ring = 0; ring < 64; ring++) _inventory.GrantAppraisedRingForDebug(ring);
            rom.CopySave(_saveData);
            if (compareFrames)
            {
                // Grants normally occur outside the list. Reopen through its
                // native initialization, which redraws the newly owned icons.
                _ringMenu.CloseImmediatelyForValidation();
                _ringMenu.OpenImmediatelyForValidation(RingMenuMode.List, static () => { });
                rom.SaveGraphicsBeforeMenu(); rom.OpenImmediately(4, 1); rom.AdvanceText();
                sounds.Clear(); rom.ClearSounds();
                Step(1, 1);
            }
            while (_ringMenuScreen.Page != 0) { Step(1, 4); Step(22); }
            for (int ring = 0; ring < 64; ring++)
            {
                // The fastest scenario covers every description. Slower
                // settings settle representative entries across all pages,
                // while retaining native navigation through every ring.
                bool settleRing = textSpeed == 4
                    ? ring is 0 or 1 or 7 or 15 or 16 or 31 or 32 or 63
                    : ring is 0 or 63;
                Step(3); // Preserve every name's first pass, including empty glyphs.
                if (settleRing) SettleDescription(800);
                if (otherTextSpeeds && settleRing)
                    FailIf(!_dialogue.PrintingComplete || rom[0xcba0] != 0x80,
                        $"Ring list speed={textSpeed} ring=${ring:x2}: representative description failed to reach native non-exitable completion.");
                if (ring < 63)
                {
                    Step(1, 0x10);
                    if (_ringMenuScreen.PageTransitionActive) Step(22);
                }
            }
            _ringMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated all 64 clean-US ring-name first-pass glyph pixels, ring-list description delays, sparse/blank text, non-exitable completion, external cancellation, scroll restart and text sound order through split/batched updates.");
    }
}
