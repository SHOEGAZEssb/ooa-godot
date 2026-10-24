using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFaroreMemory()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batch);
            if (!linked)
            {
                FailIf(_entities.EntityAdapters<FaroreMemoryRoomEntity>().Any(), "Unfinished unlinked game must delete Farore's book.");
                _saveData.SetGlobalFlag(GlobalFlag.FinishedGame); LoadValidationRoom(5, 0xcf);
                StepGameplayUpdates(3, Vector2.Zero, batched: batch);
            }
            var book = _entities.EntityAdapters<FaroreMemoryRoomEntity>().Single();
            FailIf(book.Npc.Position != new Vector2(56, 56), "Farore's book must retain source position $38,$38.");
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                ApproachLibraryActor(book.Npc, batch); Press(); Step(2);
                FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains("Read this book?", StringComparison.Ordinal) ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0x91, "Book must open TX_551b with disabled mask $91 through the real A-button route.");
                _dialogue.SubmitChoiceForValidation(repeat == 0 ? 0 : 1); Step();
                if (repeat == 0)
                {
                    FailIf(!_secretList.IsActive, "Reading Farore's book must open MENU_SECRET_LIST $0a.");
                    var position = _player.Position; Step(22);
                    FailIf(_secretList.Screen is not { Visible: true } || _player.Position != position, "Secret list must acquire the shared menu fade/pause lifecycle.");
                    StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batch); Step(22);
                    FailIf(_secretList.IsActive, "B must return from the secrets list through the menu fade.");
                }
                Step(8);
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 || _player.CutsceneControlled,
                    "Farore's book must release controls after wait8 on both Yes and No.");
            }
            ApproachLibraryActor(book.Npc, batch); Press(); Step(2);
            LoadValidationRoom(0, 0x38);
            FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
                "Room cancellation must release the book's disabled mask.");
        }
    }

    private void ValidateSecretListRom()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool unlocked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            var data = new SecretListDatabase();
            foreach (var entry in data.Entries(_saveData)) _saveData.SetGlobalFlag(entry.Flag, unlocked);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x42); _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd + 1, 0x31);
            var rom = new MenuRom(_saveData, _currentRoom); rom.EnableVramDmaTransfers(); rom.OpenImmediately(0x0a);
            _menuLifecycle.OpenImmediately(_secretList);
            int frame = 0;
            void Compare()
            {
                if (unlocked && frame > 0)
                {
                    var expectedText = data.Text(data.Entries(_saveData)[_secretList.Cursor], _saveData);
                    var nativeText = Enumerable.Range(0, 24).Select(rom.SecretText).ToArray();
                    FailIf(!expectedText.SequenceEqual(nativeText), $"Secret list text selection={_secretList.Cursor}: port={Convert.ToHexString(expectedText)}, ROM={Convert.ToHexString(nativeText)}.");
                }
                FailIf(_secretList.Cursor != rom[0xcbb6] || _secretList.Scroll != rom[0xcbb7] ||
                    _secretList.Screen!.ScrollY != rom[0xc48c],
                    $"Secret list linked={linked}, unlocked={unlocked}: cursor/scroll/SCY port={_secretList.Cursor}/{_secretList.Scroll}/{_secretList.Screen!.ScrollY}, ROM={rom[0xcbb6]}/{rom[0xcbb7]}/{rom[0xc48c]}.");
                for (int bank = 0; bank < 2; bank++)
                for (int offset = 0; offset < 0x1000; offset++)
                    FailIf(_secretList.Screen!.VramByte(bank, offset) != rom.MapGfx(bank, 0x8000 + offset),
                        $"Secret list graphics linked={linked}, unlocked={unlocked}, selection={_secretList.Cursor}: ${bank}:${0x8000+offset:x4} port=${_secretList.Screen!.VramByte(bank,offset):x2}, ROM=${rom.MapGfx(bank,0x8000+offset):x2}.");
            }
            Compare();
            void Step(int count = 1, bool down = false, bool up = false)
            {
                StepGameplayUpdates(count, Vector2.Zero, down ? ["move_down"] : up ? ["move_up"] : [],
                    down ? ["move_down"] : up ? ["move_up"] : [], afterUpdate: () => {
                        rom.Update(down ? 0x80 : up ? 0x40 : 0, down ? 0x80 : up ? 0x40 : 0, ++frame); Compare();
                        down = up = false;
                    });
            }
            Step();
            for (int i = 0; i < data.Entries(_saveData).Count + 1; i++) { Step(down: true); Step(8); }
            for (int i = 0; i < data.Entries(_saveData).Count + 1; i++) { Step(up: true); Step(8); }
            for (int i = 0; i < 65; i++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["move_down"], i == 0 ? ["move_down"] : [], afterUpdate: () => {
                    rom.Update(i == 0 ? 0x80 : 0, 0x80, ++frame); Compare();
                });
            }
            using (var image = _secretList.Screen!.Map.GetImage())
            {
                // Compare rendered pixels with native unsigned VRAM addressing,
                // independently of the runtime's atlas/2bpp conversion.
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 160; x++)
                {
                    int offset = y / 8 * 32 + x / 8;
                    int tile = rom.MapGfx(0, 0x9c00 + offset), flags = rom.MapGfx(1, 0x9c00 + offset);
                    int px = (flags & 0x20) != 0 ? 7 - (x & 7) : x & 7;
                    int py = (flags & 0x40) != 0 ? 7 - (y & 7) : y & 7;
                    int address = 0x8000 + tile * 16 + py * 2, bank = (flags >> 3) & 1;
                    int shade = ((rom.MapGfx(bank, address) >> (7 - px)) & 1) | (((rom.MapGfx(bank, address + 1) >> (7 - px)) & 1) << 1);
                    int rgb = rom.HudColor(false, flags & 7, shade);
                    var expected = GbcRenderedColor(rgb & 31, (rgb >> 5) & 31, (rgb >> 10) & 31);
                    FailIf(image.GetPixel(x, y) != expected, $"Secret list pixel ({x},{y}) differs from native VRAM/palette, linked={linked}, unlocked={unlocked}, tile=${tile:x2}, flags=${flags:x2}, shade={shade}, RGB=${rgb:x4}, expected={expected}, actual={image.GetPixel(x,y)}.");
                }
            }
            _menuLifecycle.CloseImmediately(_secretList);
        }
    }
}
