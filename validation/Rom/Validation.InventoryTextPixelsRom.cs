using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateInventoryMissingEssenceTextRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.MissingEssences);
    private void ValidateInventoryEssenceReplayRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.EssenceReplay);
    private void ValidateInventoryItemFramesRom()
    {
        RunInventoryTextPixelsRom(InventoryTextCaseKind.Items, true);
        RunInventoryTextPixelsRom(InventoryTextCaseKind.ItemVariants, true);
    }
    private void ValidateInventoryPassiveFramesRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.PassiveTreasures, true);
    private void ValidateInventoryQuestFramesRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.QuestPage, true);
    private void ValidateInventoryRingFramesRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.RingSlots, true);
    private void ValidateInventorySubmenuFramesRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.SubmenuTexts, true);
    private void ValidateInventorySharedSlotFramesRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.SharedPassiveSlots, true);
    private void ValidateInventoryRetainedPaletteRom() => RunInventoryTextPixelsRom(InventoryTextCaseKind.PassiveTreasures, true, true);

    private void RunInventoryTextPixelsRom(InventoryTextCaseKind kind, bool compareFramePixels = false,
        bool retainedSpritePalette = false)
    {
        bool questPage = kind is InventoryTextCaseKind.QuestPage or InventoryTextCaseKind.MissingEssences or InventoryTextCaseKind.EssenceReplay;
        bool missingEssences = kind == InventoryTextCaseKind.MissingEssences;
        bool essenceReplay = kind == InventoryTextCaseKind.EssenceReplay;
        bool ringSlots = kind == InventoryTextCaseKind.RingSlots;
        bool submenuTexts = kind == InventoryTextCaseKind.SubmenuTexts;
        bool sharedSlots = kind == InventoryTextCaseKind.SharedPassiveSlots;
        bool passivePage = kind is InventoryTextCaseKind.PassiveTreasures or InventoryTextCaseKind.RingSlots or InventoryTextCaseKind.SharedPassiveSlots;
        var fixtures = kind switch
        {
            InventoryTextCaseKind.SharedPassiveSlots => new[]
            {
                (0x4a, 1, 0), (0x36, 0, 3), (0x54, 1, 6),
                (0x43, 1, 7), (0x44, 1, 10), (0x45, 1, 11)
            },
            InventoryTextCaseKind.SubmenuTexts => new[]
            {
                (0x19, 1, 0), (0x19, 1, 1), (0x19, 1, 2), (0x19, 1, 3), (0x19, 1, 4),
                (0x0f, 1, 0), (0x0f, 1, 1), (0x0f, 1, 2), (0x0f, 1, 3), (0x0f, 1, 4),
                (0x11, 0, 1), (0x11, 0, 2), (0x11, 0, 3)
            },
            InventoryTextCaseKind.RingSlots => Enumerable.Range(0, 64).Select(ring => (0x2c, 3, ring)).ToArray(),
            // bank2.s:subscreen1TreasureData slots; trade/Tuni Nut variants
            // come from data/ages/treasureDisplayData.s. Omit unused Empty
            // Bottle/Member's Card records from these supported-game inputs.
            InventoryTextCaseKind.PassiveTreasures => new[]
            {
                (0x2e, 1, 0), (0x4a, 1, 0), (0x2f, 1, 1),
                (0x41, 0, 2), (0x41, 1, 2), (0x41, 2, 2), (0x41, 3, 2),
                (0x41, 4, 2), (0x41, 5, 2), (0x41, 6, 2), (0x41, 7, 2),
                (0x41, 8, 2), (0x41, 9, 2), (0x41, 10, 2), (0x41, 11, 2), (0x41, 12, 2),
                (0x51, 1, 3), (0x4e, 1, 3), (0x4f, 1, 3), (0x36, 0, 3),
                (0x34, 1, 4), (0x42, 1, 5), (0x52, 1, 6), (0x48, 1, 6), (0x54, 1, 6),
                (0x4d, 1, 7), (0x4c, 0, 7), (0x4c, 1, 7), (0x4c, 2, 7),
                (0x49, 1, 7), (0x58, 1, 7), (0x43, 1, 7), (0x5b, 1, 8), (0x4b, 1, 9),
                (0x5a, 1, 10), (0x59, 1, 10), (0x44, 1, 10),
                (0x5e, 1, 11), (0x5c, 1, 11), (0x5d, 1, 11), (0x45, 1, 11),
                (0x46, 1, 12), (0x55, 1, 13), (0x2d, 0, 14),
                (0x2c, 1, 15), (0x2c, 2, 15), (0x2c, 3, 15)
            },
            InventoryTextCaseKind.QuestPage => new[] { (0, 0, 0), (0, 1, 0), (0, 2, 0), (0, 3, 0),
                (0, 0, 1), (0, 1, 1), (0, 2, 1), (0, 3, 1) },
            InventoryTextCaseKind.MissingEssences => new[] { (0, 0, 0), (0, 0x55, 0), (0, 0xaa, 0),
                (0, 0, 1), (0, 0x55, 1), (0, 0xaa, 1) },
            InventoryTextCaseKind.EssenceReplay => new[] { (0, 0, 0), (0, 1, 0), (0, 2, 0), (0, 3, 0),
                (0, 4, 0), (0, 5, 0), (0, 6, 0), (0, 7, 0) },
            InventoryTextCaseKind.ItemVariants => new[] { (0x01, 2, 0), (0x01, 3, 0), (0x05, 1, 0), (0x05, 2, 0),
                (0x16, 1, 0), (0x0a, 1, 0), (0x0e, 0, 0), (0x0e, 1, 0),
                (0x0e, 2, 0), (0x0e, 3, 0), (0x11, 0, 2), (0x11, 0, 3),
                (0x16, 2, 0), (0x17, 1, 0), (0x03, 0x30, 0), (0x04, 0, 0),
                (0x0d, 0x30, 0), (0x15, 0, 0) },
            _ => new[] { (0x01, 1, 0), (0x05, 3, 0), (0x06, 1, 0),
                (0x0a, 2, 0), (0x0f, 1, 0), (0x11, 0, 1), (0x19, 1, 0), (0x0c, 0, 0) }
        };
        // PALH_0a writes OBJ0-5, retaining OBJ6/7. Exercise the Maku Seed's
        // palette-7 mask with explicit live bank-2 colors, not a black default.
        if (retainedSpritePalette) fixtures = fixtures.Where(value => value.Item1 == 0x36).ToArray();
        int[][] retainedColors = retainedSpritePalette
            ? new[] { new[] { 0, 0x001f, 0x03e0, 0x7c00 }, new[] { 0x7fff, 0x1d72, 0x5294, 0x475f }, new[] { 0x3210, 0x7fff, 0, 0x56a5 } }
            : new[] { Array.Empty<int>() };
        int hostCase1 = 0, cases = 0;
        foreach (int[] colors in retainedColors)
        foreach (bool retainedText in submenuTexts ? new[] { false, true } : new[] { false })
        foreach (var fixture in fixtures)
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc688, 0); save.WriteWramByte(0xc689, 0);
            for (int slot = 0; slot < 16; slot++) save.WriteWramByte(0xc68a + slot, 0);
            save.WriteWramByte(0xc68a, questPage || passivePage ? (byte)0 : (byte)fixture.Item1);
            if (questPage)
            {
                save.WriteWramByte(0xc6ac, kind == InventoryTextCaseKind.QuestPage ? (byte)fixture.Item2 : (byte)0);
                save.WriteWramByte(0xc6bf, missingEssences ? (byte)fixture.Item2 : (byte)0xff);
            }
            InitializeTransientSession(save);
            LoadValidationRoom(questPage ? fixture.Item3 : 0, 0x45);
            if (!questPage) _inventory.GiveTreasure(fixture.Item1, fixture.Item2);
            if (sharedSlots)
            {
                // Grant the earlier table rows after the winning row. The
                // display's source order, rather than collection order, must
                // decide the slot's final text key.
                int[] predecessors = fixture.Item3 switch
                {
                    0 => new[] { 0x2e }, 3 => new[] { 0x51, 0x4e, 0x4f },
                    6 => new[] { 0x52, 0x48 }, 7 => new[] { 0x4d, 0x4c, 0x49, 0x58 },
                    10 => new[] { 0x5a, 0x59 }, 11 => new[] { 0x5e, 0x5c, 0x5d },
                    _ => throw new InvalidOperationException("Unsupported shared-slot fixture.")
                };
                foreach (int treasure in predecessors.Reverse()) _inventory.GiveTreasure(treasure, 0);
            }
            if (ringSlots)
            {
                _inventory.GrantAppraisedRingForDebug(fixture.Item3);
                _inventory.SetRingBoxSlotFromList(fixture.Item3 % 5, fixture.Item3);
            }
            if (fixture.Item1 is 0x0f or 0x19) _inventory.GiveTreasure(0x20, 0x30);
            if (fixture.Item1 == 0x11)
            {
                _inventory.GiveTreasure(0x24 + fixture.Item3, 0);
                _inventory.SelectHarpSong(fixture.Item3);
            }
            if (submenuTexts)
            {
                if (fixture.Item1 == 0x11)
                {
                    for (int song = 1; song <= 3; song++) _inventory.GiveTreasure(0x24 + song, 0);
                    _inventory.SelectHarpSong(1);
                }
                else for (int seed = 0; seed < 5; seed++) _inventory.GiveTreasure(0x20 + seed, 0x30);
            }
            var rom = new MenuRom(_saveData, _currentRoom);
            if (retainedSpritePalette)
            {
                rom.SeedRetainedSpritePalette(7, colors);
                for (int shade = 0; shade < 4; shade++)
                {
                    int address = 0xdef8 + shade * 2;
                    _inventory.RuntimeState.SetWramByte(address, (byte)colors[shade]);
                    _inventory.RuntimeState.SetWramByte(address + 1, (byte)(colors[shade] >> 8));
                }
            }
            rom.EnableVramDmaTransfers();
            rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu();
            _inventoryMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(1);
            if (retainedSpritePalette)
                for (int shade = 0; shade < 4; shade++)
                    FailIf(rom.HudColor(true, 7, shade) != colors[shade],
                        $"Inventory PALH_0a overwrote retained OBJ7 shade {shade}.");
            rom.AdvanceText(); // Initialization clears the retained text strip.
            var sounds = _sound.AttachPlayRequestAudit(); rom.ClearSounds();
            int update = 0, states = 0, lastFrameTextState = -1;
            bool replayingText = false;
            string context = $"Inventory text {kind} item=${fixture.Item1:x2} parameter=${fixture.Item2:x2} variant=${fixture.Item3:x2} batch={batched} retained={retainedText} OBJ7={string.Join(',', colors)}";
            byte[]? previousGfx = null;
            int[] expectedPixels = new int[128 * 16];
            void Step(int count, int pressed = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622));
                    if (compareFramePixels) rom.PublishHudGraphics();
                    rom.AdvanceText(); edge = 0; update++;
                    if (rom[0xcba1] == 2) states |= 1 << rom.TextState;
                    // Full-width name replay can shift the staging buffer
                    // without uploading it. Compare the published VRAM bytes,
                    // not w7TextGfxBuffer before its next native DMA request.
                    byte[] glyphs = Enumerable.Range(0, 512).Select(offset => rom.MapGfx(1, 0x9200 + offset)).ToArray();
                    if (previousGfx is null || !previousGfx.SequenceEqual(glyphs))
                    {
                        previousGfx = glyphs;
                        for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 128; x++)
                        {
                            int offset = (x / 8) * 32 + (y / 8) * 16 + (y & 7) * 2;
                            int shade = (glyphs[offset] >> (7 - (x & 7)) & 1) |
                                (glyphs[offset + 1] >> (7 - (x & 7)) & 1) << 1;
                            expectedPixels[y * 128 + x] = rom.HudColor(false, 1, shade);
                        }
                    }
                    using Image frame = _inventoryScreen.ComposeInventoryTextImage();
                    byte[] pixels = frame.GetData();
                    for (int pixel = 0; pixel < expectedPixels.Length; pixel++)
                    {
                        int offset = pixel * 4;
                        int observed = (pixels[offset] * 31 + 127) / 255 |
                            ((pixels[offset + 1] * 31 + 127) / 255) << 5 |
                            ((pixels[offset + 2] * 31 + 127) / 255) << 10;
                        if (pixels[offset + 3] != 255 || observed != expectedPixels[pixel]) FailIf(true,
                            context + $" update={update} pixel=({pixel % 128},{pixel / 128}): runtime=${observed:x4}, native=${expectedPixels[pixel]:x4}, state=${rom.TextState:x2}/active=${rom[0xcba0]:x2}, text=${rom[0xcba2]:x2}.");
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $" update={update}: ordered text/menu sounds differ.");
                    // Text-strip pixels still compare every update. During
                    // long replay, sample complete layers at state boundaries
                    // and character intervals; transitions/input compare fully.
                    if (compareFramePixels && (!replayingText || rom.TextState != lastFrameTextState || update % 8 == 0))
                    {
                        CompareInventoryFramePixelsRom(rom, context + $" update={update}");
                        lastFrameTextState = rom.TextState;
                    }
                });
            }
            if (questPage) { Step(1, 4); Step(13); Step(1, 4); Step(13); }
            if (passivePage)
            {
                Step(1, 4); Step(13);
                // inventorySubscreen1_drawTreasures reserves indices $10-$14
                // for ring-box contents and stores ring | $c0 as the text key.
                int selectedSlot = ringSlots ? 16 + fixture.Item3 % 5 : fixture.Item3;
                for (int slot = 0; slot < selectedSlot; slot++) { Step(1, 0x10); Step(1); }
                Step(1); // Page completion itself does not dispatch the slot's text.
                if (ringSlots) FailIf(rom[0xcbd1] != selectedSlot || rom[0xcbbb] != (0xc0 | fixture.Item3) ||
                    _inventoryScreen.ActiveCursor != selectedSlot || _inventoryScreen.ActiveTextKey != (0xc0 | fixture.Item3),
                    context + ": ring slot did not dispatch its native $c0-$ff text key.");
                if (sharedSlots)
                {
                    int textKey = fixture.Item3 switch { 0 => 0x45, 3 => 0x15, 6 => 0x47,
                        7 => 0x4c, 10 => 0x51, 11 => 0x52, _ => 0 };
                    FailIf(rom[0xcbbb] != textKey || _inventoryScreen.ActiveTextKey != textKey,
                        context + $": shared passive slot lost its source-ordered winning row, native=${rom[0xcbbb]:x2}, runtime=${_inventoryScreen.ActiveTextKey:x2}, expected=${textKey:x2}.");
                }
            }
            if (essenceReplay)
                for (int essence = 0; essence < fixture.Item2; essence++) { Step(1, 0x80); Step(1); }
            if (submenuTexts)
            {
                if (retainedText) Step(43); // Enter with the original item description already scrolling.
                Step(1, 1); Step(40); // Actual A input, initialization and panel expansion.
                int option = fixture.Item1 == 0x11 ? fixture.Item3 - 1 : fixture.Item3;
                for (int index = 0; index < option; index++) { Step(1, 0x10); Step(1); }
                Step(2);
                // Source treasureDisplayData seed keys $32-$36; state 2 adds
                // five for Shooter. Song keys are $42-$44.
                int textKey = fixture.Item1 == 0x11 ? 0x42 + option :
                    (fixture.Item1 == 0x0f ? 0x37 : 0x32) + option;
                FailIf(rom[0xcbcd] != 2 || rom[0xcbce] != 2 || rom[0xcbbb] != textKey ||
                    !_inventoryScreen.ItemSubmenuReady || _inventoryScreen.ActiveTextKey != textKey,
                    context + $": selected submenu option did not dispatch its native text key, menu=${rom[0xcbcd]:x2}/${rom[0xcbce]:x2}, native=${rom[0xcbbb]:x2}, runtime=${_inventoryScreen.ActiveTextKey:x2}, ready={_inventoryScreen.ItemSubmenuReady}, expected=${textKey:x2}.");
                states = 0; // Require this option's complete cycle, not earlier item/option text.
            }
            bool emptyText = passivePage && (fixture.Item1 == 0x41 && fixture.Item2 == 12 ||
                fixture.Item1 == 0x4c && fixture.Item2 == 1);
            // All records exercise centering and the exact first scroll. One
            // record per dispatch kind additionally completes the state cycle;
            // essence replay has distinct streams and retains every variant.
            bool replayText = !missingEssences && !emptyText &&
                (essenceReplay || fixture == fixtures[0] && !retainedSpritePalette);
            int textUpdates = 43;
            Step(textUpdates);
            if (replayText)
            {
                replayingText = true;
                int began = update;
                while ((states & 0x1e) != 0x1e && update - began < 1600) Step(8);
                Step(8); // Cross the next character boundary after entering replay.
                replayingText = false;
            }
            FailIf(replayText && (states & 0x1e) != 0x1e,
                context + $": fixture missed native pause/description/trailing-space/replay states, mask=${states:x2}.");
            if (questPage)
            {
                if (!essenceReplay)
                    for (int essence = 1; essence < 8; essence++) { Step(1, 0x80); Step(43); }
                if (!essenceReplay && !missingEssences)
                {
                    Step(1, 0x10); Step(textUpdates); // Present/past blurb.
                    Step(1, 0x80); Step(textUpdates); // Source heart-piece count text.
                    Step(1, 0x80); Step(textUpdates); // Save screen label without activating it.
                    Step(1, 0x40); Step(3); // Repeated heart-piece selection.
                    Step(1, 0x20); Step(3); // Return ownership to essences before reopening.
                }
                else { Step(1, 0x80); Step(3); Step(1, 0x40); Step(3); }
            }
            else if (passivePage)
            {
                Step(1, 0x10); Step(3); Step(1, 0x20); Step(43);
            }
            else if (submenuTexts)
            {
                Step(1, 0x10); Step(3); Step(1, 0x20); Step(43);
                Step(1, 8); Step(12); // Start confirms; item swap clears the strip.
                FailIf(_inventoryScreen.ItemSubmenuActive || rom[0xcbcd] != 1 ||
                    _inventory.EquippedA != rom[0xc689] || _inventory.StorageItemAt(0) != rom[0xc68a],
                    context + ": submenu confirmation equipment/ownership differs.");
            }
            else
            {
                Step(1, 0x10); Step(12); // Empty slot clears the complete strip.
                Step(1, 0x20); Step(3); // Repeated selection resets the source pause.
                Step(1, 1); Step(12); // Equipping clears text before its next selection.
            }
            _inventoryMenu.CloseImmediatelyForValidation();
            cases++;
        }
        GD.Print($"Validated {cases} clean-US inventory fixtures, {kind}: every record's centered name/first scroll, representative complete text cycles, complete layers={compareFramePixels}, cursor/page transitions and repeated selection through representative split/batched gameplay. Palette fades and LCD timing remain outside this comparison.");
    }
}

internal enum InventoryTextCaseKind
{
    Items,
    ItemVariants,
    QuestPage,
    MissingEssences,
    EssenceReplay,
    PassiveTreasures,
    RingSlots,
    SubmenuTexts,
    SharedPassiveSlots
}
