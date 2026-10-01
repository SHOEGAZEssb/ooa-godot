using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNameEntryKeyboardRom()
    {
        foreach (bool batched in new[] { false, true }) RunNameEntryRom(batched, 0);
        GD.Print("Validated clean-US name keyboard: all 60 glyphs, four-way wrapping, lower-option mapping and all 256 button masks through split/batched application updates.");
    }

    private void ValidateNameEntryEditingRom()
    {
        foreach (bool batched in new[] { false, true }) RunNameEntryRom(batched, 1);
        GD.Print("Validated clean-US name insertion, overwrite, five-character clamp, deletion, entry arrows, blank cancellation, trimming, confirmation and primary/backup save bytes.");
    }

    private void ValidateNameEntryAutofireRom()
    {
        foreach (bool batched in new[] { false, true }) RunNameEntryRom(batched, 2);
        GD.Print("Validated clean-US name direction repeat: $28 delay, four-update cadence, long holds, release/chord changes, non-repeating buttons and retained state across file-name entry.");
    }

    private void RunNameEntryRom(bool batched, int scenario)
    {
        using var root = new FrontendValidationRoot();
        AddChild(root);
        try
        {
            root.Initialize(persistSaveData: true);
            root.Step(241, batched);
            root.Step(1, batched, "inventory");
            root.Step(1, batched);
            root.Step(1, batched, "inventory");
            root.Step(32, batched);
            var application = new ApplicationValidationFixture(root);
            var menu = root._mainMenu!;
            var screen = root._mainMenuScreen!;
            var rom = new FrontendRom();
            rom.InitializeMenu();
            rom.Sounds.Clear();
            var sounds = root._sound.AttachPlayRequestAudit();
            OracleRandomState seed = root._random.CaptureState();
            rom[0xff94] = seed.Rng1;
            rom[0xff95] = seed.Rng2;
            int update = 0;
            string[] Actions(int mask) => new[] { "attack", "item", "map", "inventory",
                "move_right", "move_left", "move_up", "move_down" }
                .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();

            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, Actions(held ?? pressed), Actions(pressed), batched, () =>
                {
                    rom.UpdateMenu(edge, held ?? pressed);
                    edge = 0;
                    update++;
                    string context = $"Name scenario={scenario} batch={batched} update={update} ROM=${rom[0xcbb3]:x2}/${rom[0xcbb4]:x2}";
                    Page expected = rom[0xcbb3] switch
                    {
                        1 => Page.FileSelect, 2 => Page.NameEntry, 5 => Page.NewFileOptions,
                        _ => throw new InvalidOperationException($"{context}: left the name-entry path.")
                    };
                    FailIf(menu.CurrentPage != expected, $"{context}: runtime page {menu.CurrentPage} != {expected}.");
                    if (expected == Page.NameEntry && rom[0xcbb4] is 1 or 2)
                    {
                        FailIf(screen.NameCursor != rom[0xcbbc] || screen.NameEntryPosition != rom[0xcbbe],
                            $"{context}: keyboard/entry cursor runtime=${screen.NameCursor:x2}/${screen.NameEntryPosition:x2}, native=${rom[0xcbbc]:x2}/${rom[0xcbbe]:x2}.");
                        if (rom[0xcbbc] >= 0x50)
                            FailIf(screen.NameLowerChoice != rom[0xcbbd],
                                $"{context}: lower option {screen.NameLowerChoice} != native ${rom[0xcbbd]:x2}.");
                        for (int index = 0; index < 5; index++)
                            FailIf(screen.RawEnteredName[index] != (char)rom.NameByte(index),
                                $"{context}: name byte {index} runtime=${(int)screen.RawEnteredName[index]:x2}, native=${rom.NameByte(index):x2}.");
                        FailIf(rom.NameByte(5) != 0, $"{context}: native name terminator was overwritten.");
                    }
                    else if (rom[0xcbb4] != 0)
                        FailIf(menu.Cursor != rom[0xcbbc], $"{context}: file/options cursor differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"{context}: sound request order differs.");
                    OracleRandomState random = root._random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        root._random.Calls - seed.Calls != rom.RandomCalls,
                        $"{context}: name entry changed shared RNG state/order.");
                    for (int slot = 0; slot < 3; slot++)
                        FailIf(root.FileSlotExists(slot) != (rom.SavedByte(slot, 0xc5b2) == 0x5a),
                            $"{context}: slot ${slot:x2} existence/persistence differs from native SRAM.");
                });
            }
            void Press(int keys) => Step(pressed: keys);
            void OpenName(int slot, int directions = 0)
            {
                FailIf(menu.CurrentPage != Page.FileSelect || menu.Cursor != 0,
                    "Name scenario must approach from initialized file selection.");
                for (int index = 0; index < slot; index++) Step(pressed: 0x80, held: directions | 0x80);
                Step(pressed: 1, held: directions | 1); // Select empty slot.
                Step(pressed: 1, held: directions | 1); // Options initialization consumes A.
                Step(pressed: 1, held: directions | 1); // New Game.
                Step(pressed: 0x11, held: directions | 0x11); // Name initialization consumes both A/Right.
                FailIf(screen.NameCursor != 0 || screen.NameEntryPosition != 0 || root.FileSlotExists(slot),
                    "Name initialization accepted input or persisted an unfinished file.");
            }
            void MoveTo(int target)
            {
                // Approach solely through input, including the grid/lower-row
                // wrapping paths. Every intermediate update is ROM-compared.
                for (int guard = 0; (rom[0xcbbc] & 0xf0) != (target & 0xf0); guard++)
                {
                    FailIf(guard >= 6, $"Could not approach source name row ${target:x2}.");
                    Press(0x80);
                }
                for (int guard = 0; rom[0xcbbc] != target; guard++)
                {
                    FailIf(guard >= 12, $"Could not approach source name cell ${target:x2}.");
                    Press(0x10);
                }
            }
            void ClearName()
            {
                MoveTo(0x53);
                for (int index = 0; index < 5; index++) Press(1);
                for (int index = 0; index < 6; index++) Press(2);
                FailIf(screen.EnteredName.Length != 0 || screen.NameEntryPosition != 0,
                    "Name B deletion failed to clear the five-byte field and clamp at zero.");
                MoveTo(0);
            }
            void Submit(int slot, string? expectedName, bool withA = false)
            {
                Press(8);
                FailIf(rom[0xcbb4] != 1 || root.FileSlotExists(slot),
                    "First Start submitted before selecting the source OK option.");
                Press(withA ? 1 : 8);
                FailIf(root.FileSlotExists(slot), "Name confirmation wrote SRAM before the commit dispatch.");
                Step(pressed: 0xff); // Commit owns the complete update.
                Step(pressed: 0xff); // File-selection initialization consumes input.
                FailIf(menu.CurrentPage != Page.FileSelect || menu.Cursor != 0 ||
                    root.FileSlotExists(slot) != (expectedName is not null),
                    "Name completion/cancellation failed to reset file selection or persistence.");
                if (expectedName is null)
                {
                    for (int address = 0xc5b0; address < 0xcb00; address++)
                        FailIf(rom.SavedByte(slot, address) != 0 || rom.SavedByte(slot, address, backup: true) != 0,
                            $"Blank name retained native primary/backup byte ${address:x4}.");
                    return;
                }
                FailIf(!OracleSaveData.TryDeserialize(root.FileSlotSnapshot(slot), out var save) || save!.LinkName != expectedName,
                    $"Name commit did not persist source-derived '{expectedName}'.");
                for (int address = 0xc5b0; address < 0xcb00; address++)
                    FailIf(save!.ReadWramByte(address) != rom.SavedByte(slot, address) ||
                        rom.SavedByte(slot, address) != rom.SavedByte(slot, address, backup: true),
                        $"Name slot ${slot:x2}: persisted byte ${address:x4} differs from native primary/backup.");
            }

            OpenName(0);
            switch (scenario)
            {
                case 0:
                    for (int row = 0; row < 5; row++)
                    for (int column = 0; column < 12; column++)
                    {
                        int cell = row * 0x10 + column;
                        foreach (int direction in new[] { 0x10, 0x20, 0x40, 0x80 })
                        {
                            MoveTo(cell);
                            Press(direction);
                        }
                        MoveTo(cell);
                        int alphabet = row * 6 + column % 6;
                        char glyph = alphabet < 26 ? (char)((column < 6 ? 'A' : 'a') + alphabet) : ' ';
                        FailIf(!screen.TryGetSelectedNameCharacter(out char actual) || actual != glyph,
                            $"US name cell ${cell:x2} did not select source glyph '{glyph}'.");
                        int position = screen.NameEntryPosition;
                        Press(1); // Execute the native tilemap-to-character path.
                        FailIf(rom.NameByte(position) != glyph, $"Native name cell ${cell:x2} did not encode '{glyph}'.");
                    }
                    foreach (int option in new[] { 0x50, 0x53, 0x56 })
                    foreach (int direction in new[] { 0x10, 0x20, 0x40, 0x80 })
                    {
                        MoveTo(option);
                        Press(direction);
                    }
                    for (int mask = 0; mask <= 0xff; mask++)
                    {
                        MoveTo(0);
                        Press(mask);
                    }
                    MoveTo(0); ClearName(); Submit(0, null);
                    break;
                case 1:
                    Submit(0, null); // Untouched field cancels without a save.
                    OpenName(2);
                    MoveTo(0x42); Press(1); Press(1); // Space cells, including all-blank cancellation.
                    Submit(2, null, withA: true);
                    OpenName(1);
                    for (int column = 0; column < 5; column++) { MoveTo(column); Press(1); }
                    FailIf(screen.EnteredName != "ABCDE" || screen.NameEntryPosition != 4,
                        "Name did not clamp at its fifth character.");
                    MoveTo(5); Press(1);
                    FailIf(screen.EnteredName != "ABCDF" || screen.NameEntryPosition != 4,
                        "Sixth character failed to overwrite position $04.");
                    Press(2);
                    FailIf(screen.EnteredName != "ABCD" || screen.NameEntryPosition != 3,
                        "B did not clear the current position before moving left.");
                    MoveTo(0x50); Press(1); Press(1); Press(1); Press(1); // Left arrow clamps at zero.
                    MoveTo(0x53); for (int index = 0; index < 6; index++) Press(1); // Right arrow clamps at four.
                    MoveTo(4); Press(1);
                    MoveTo(0x50); Press(1); Press(1);
                    MoveTo(9); Press(1); // Lowercase d overwrites position $02.
                    Submit(1, "ABdDE", withA: true);
                    OpenName(0);
                    MoveTo(0x42); Press(1); // Preserve a leading space.
                    MoveTo(6); Press(1); // Lowercase a.
                    MoveTo(0x42); Press(1); Press(1); Press(1); // Trim trailing spaces and retain terminator.
                    Submit(0, " a");
                    break;
                case 2:
                    Press(0x10);
                    Step(39, held: 0x10);
                    FailIf(screen.NameCursor != 1, "Name repeat began before $28 held updates.");
                    Step(1, held: 0x10);
                    FailIf(screen.NameCursor != 2, "Name repeat missed the $28 boundary.");
                    Step(3, held: 0x10);
                    FailIf(screen.NameCursor != 2, "Name repeat ignored its four-update cadence.");
                    Step(1, held: 0x10);
                    Step(256, held: 0x10);
                    Step(4, pressed: 0x80, held: 0x90); // Shared Right preserves the repeat clock; Down wins.
                    Step(39, held: 0x20); // Disjoint Left resets the initial delay.
                    Step(1, held: 0x20);
                    Step(); // Release resets repeat eligibility.
                    Press(0x10); Step(40, held: 0x10);
                    MoveTo(0); ClearName();
                    Press(1); string name = screen.RawEnteredName;
                    Step(64, held: 1);
                    FailIf(screen.RawEnteredName != name, "Held A repeated name insertion.");
                    Press(8); Step(64, held: 8);
                    FailIf(root.FileSlotExists(0) || rom[0xcbb4] != 1, "Held Start submitted the name twice.");
                    Press(0x10); // Lower OK -> left arrow; new direction starts its clock.
                    Step(pressed: 0x20, held: 0x30); // Left back to OK; retain shared Right.
                    Step(37, held: 0x10);
                    Step(pressed: 8, held: 0x18); // Submit at repeat counter $27, before its first repeat.
                    Step(held: 0x10); Step(held: 0x10);
                    FailIf(!root.FileSlotExists(0), "Autofire retention scenario did not commit its first file.");
                    OpenName(1, 0x10);
                    Step(held: 0x10); // Retained $27 increments to $28 immediately on re-entry.
                    FailIf(screen.NameCursor != 1, "Name initialization discarded the native shared autofire history.");
                    ClearName(); Submit(1, null);
                    break;
                default: throw new InvalidOperationException("Unknown name-entry ROM scenario.");
            }
        }
        finally { root.Free(); }
    }
}
