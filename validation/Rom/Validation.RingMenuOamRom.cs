using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHudHeartBeepRingRom() => RunRingMenuOamRom(true);
    private void ValidateRingScrollPixelsRom() => RunRingMenuOamRom(false, true, true);
    private void ValidateRingAllIconsPixelsRom() => RunRingMenuOamRom(false, true, true, true);

    private void RunRingMenuOamRom(bool lowHealthWarning, bool comparePixels = false,
        bool compareScrollPixels = false, bool allIcons = false)
    {
        int hostCase1 = 0;
        foreach (RingMenuMode mode in allIcons ? new[] { RingMenuMode.List } : new[] { RingMenuMode.List, RingMenuMode.Appraisal })
        foreach (int level in new[] { 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(8); // Appraisal may exit with B.
            save.WriteWramByte(WramAddress.wRingBoxLevel, (byte)level);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            if (lowHealthWarning)
            {
                _entities.Clear();
                _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
                _saveData.WriteWramByte(0xc622, 63);
            }
            foreach (int ring in new[] { 0, 1, 8, 16, 63 }) _inventory.GrantAppraisedRingForDebug(ring);
            if (allIcons) for (int ring = 0; ring < 64; ring++) _inventory.GrantAppraisedRingForDebug(ring);
            int[] rings = [0, 1, 8, 16, 63];
            for (int slot = 0; slot < _inventory.RingBoxCapacity; slot++)
                _inventory.SetRingBoxSlotFromList(slot, rings[slot]);
            _inventory.EquipRingAt(0);
            for (int ring = 0; ring < 20; ring++) _inventory.GiveUnappraisedRing(ring);
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareScrollPixels) rom.EnableVramDmaTransfers();
            if (comparePixels) { rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu(); }
            bool completed = false;
            _ringMenu.OpenImmediatelyForValidation(mode, () => completed = true);
            rom.OpenImmediately(4, mode == RingMenuMode.List ? 1 : 0);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            int visitedPages = 0;
            string? previousPixels = null;
            int[]? previousExpectedPixels = null;
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(held ?? pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette();
                    int frame = lowHealthWarning ? _saveData.ReadWramByte(0xc622) : update + 1;
                    rom.Update(edge, held ?? pressed, frame); update++;
                    edge = 0;
                    if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != rom.Sounds.Count(sound => sound == 0x60),
                        $"Ring warning {mode} L{level} update={update}: active/closing/resumed native warning count differs.");
                    if (!_ringMenuScreen.Visible) return; // Saved gameplay OAM takes over at white.
                    if (compareScrollPixels) visitedPages |= 1 << rom[0xcbb6];
                    int nativeCount = (rom[0xff9f] - 0x10) / 4; // hOamTail; first four slots reserved.
                    var parts = _ringMenuScreen.SpriteOam;
                    FailIf(parts.Count != nativeCount,
                        $"Ring OAM {mode} L{level} update {update}: {parts.Count} parts != ROM {nativeCount}, native state=${rom[0xcbcd]:x2}/${rom[0xcbce]:x2}.");
                    for (int part = 0; part < parts.Count; part++)
                    {
                        int address = 0xcb10 + part * 4; // wOam + reserved $10 bytes.
                        FailIf(parts[part].Y != rom[address] || parts[part].X != rom[address + 1] ||
                            parts[part].Tile != rom[address + 2] || parts[part].Attributes != rom[address + 3],
                            $"Ring OAM {mode} L{level} update {update} part {part}: runtime=${parts[part].Y:x2}/${parts[part].X:x2}/${parts[part].Tile:x2}/${parts[part].Attributes:x2}, ROM=${rom[address]:x2}/${rom[address + 1]:x2}/${rom[address + 2]:x2}/${rom[address + 3]:x2}.");
                    }
                    if (comparePixels && (compareScrollPixels || !_ringMenuScreen.PageTransitionActive && rom[0xcbcd] != 2))
                        CompareRingFieldPixelsRom(rom, mode, ref previousPixels, ref previousExpectedPixels,
                            $"Ring pixels {mode} L{level} update={update} batch={batched}", compareScrollPixels);
                });
            }
            Step(34); // Both cursor flicker boundaries, including byte wrapping.
            if (mode == RingMenuMode.List)
            {
                Step(1, 0x10); // Box OAM uses the position before this input.
                Step(3);
                Step(1, 1); // A consumes input before list cursor/arrows draw.
            }
            Step(2); // Wrap below while the list cursor is visible.
            Step(1, 0x80);
            Step(1, 0x40);
            Step(1, 0x20); // Left edge wraps across a page.
            Step(22);
            Step(1, 4); // Select consumes list OAM; box/E survive the scroll.
            Step(22);
            Step(1, 0x10);
            Step(39, held: 0x10);
            Step(1, held: 0x10);
            Step(8, held: 0x10);
            Step(260); // List flicker wraps at $ff without resetting a page.
            if (allIcons)
            {
                for (int page = 0; page < 4; page++) { Step(1, 4); Step(22); }
                FailIf(visitedPages != 0x0f,
                    "Ring pixel fixture failed to visit all four native pages containing the 64 ring icons.");
            }
            Step(1, 2);
            if (mode == RingMenuMode.List) { Step(3); Step(1, 2); }
            Step(22);
            FailIf(!completed || _gameplayPause.IsLeased,
                "Ring OAM close did not release its gameplay owner at the native fade boundary.");
            FailIf(compareScrollPixels && rom.VramDmaTransfers == 0,
                "Ring scroll pixels did not execute any native LCD-off DMA transfers.");
            if (lowHealthWarning)
            {
                FailIf(sounds.RequestsFor(0x60) != 0,
                    "Ring list/appraisal ownership and its final closing dispatch must suppress the low-health warning.");
                int remaining = 64 - (_saveData.ReadWramByte(0xc622) & 63);
                Step(remaining, held: 0);
                FailIf(sounds.RequestsFor(0x60) != 1 || _gameplayPause.IsLeased,
                    "Ring closing must resume the warning at the next retained global boundary with idle input.");
            }
        }
        GD.Print("Validated clean-US ring box/list/appraisal OAM order, five-slot C/E markers, pre-input box cursor, arrow priority, flicker/wrap, scroll freezes and retained closing OAM in split/batched updates.");
    }
}
