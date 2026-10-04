using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateInventorySubmenuPositionsRom() => RunInventorySubmenuLayoutFramesRom(true);
    private void ValidateInventorySubmenuAvailabilityRom() => RunInventorySubmenuLayoutFramesRom(false);
    private void ValidateInventorySubmenuInitialSelectionRom() => RunInventorySubmenuLayoutFramesRom(false, true);

    private void RunInventorySubmenuLayoutFramesRom(bool allPositions, bool initialSelections = false)
    {
        int cases = 0;
        int hostCase = 0;
        foreach (int item in new[] { 0x19, 0x0f, 0x11 })
        foreach (int slot in allPositions ? Enumerable.Range(0, 16) : new[] { 0 })
        foreach (int mask in initialSelections ? (item == 0x11 ? new[] { 3, 7 } : new[] { 0x15, 0x1f }) :
            allPositions ? new[] { item == 0x11 ? 7 : 0x15 } :
            Enumerable.Range(0, item == 0x11 ? 8 : 32))
        foreach (int initialOption in initialSelections ?
            Enumerable.Range(0, item == 0x11 ? 3 : 5).Where(option => (mask & 1 << option) != 0) : new[] { -1 })
        foreach (bool batched in RomHostSchedules(hostCase++))
        {
            // Inventory dispatch separately checks both button routes. Rotate
            // the selected button while retaining every slot/mask/option.
            bool toA = ((allPositions ? slot : mask) & 1) != 0;
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc688, 0); save.WriteWramByte(0xc689, 0);
            for (int storage = 0; storage < 16; storage++) save.WriteWramByte(0xc68a + storage, 0);
            save.WriteWramByte(0xc68a + slot, (byte)item);
            int address = WramAddress.wObtainedTreasureFlags + item / 8;
            save.WriteWramByte(address, (byte)(save.ReadWramByte(address) | 1 << (item & 7)));
            save.WriteWramByte(0xc69e, (byte)(item == 0x11 ? mask << 5 : mask));
            int firstOption = initialOption >= 0 ? initialOption : Enumerable.Range(0, item == 0x11 ? 3 : 5)
                .FirstOrDefault(option => (mask & 1 << option) != 0);
            save.WriteWramByte(0xc6b7, (byte)(mask == 0 ? 0 : firstOption + 1));
            save.WriteWramByte(0xc6c4, (byte)firstOption); save.WriteWramByte(0xc6c5, (byte)firstOption);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.EnableVramDmaTransfers(); rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu();
            _inventoryMenu.OpenImmediatelyForValidation(); rom.OpenImmediately(1); rom.AdvanceText();
            var sounds = _sound.AttachPlayRequestAudit(); rom.ClearSounds();
            int update = 0, button = toA ? 1 : 2;
            string context = $"Inventory panel item=${item:x2} slot=${slot:x2} mask=${mask:x2} initial=${firstOption:x2} button={(toA ? 'A' : 'B')} batch={batched}";
            void Step(int count = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, pressed, ++update); rom.PublishHudGraphics(); rom.AdvanceText(); edge = 0;
                    FailIf(_inventoryScreen.ItemSubmenuActive != (rom[0xcbcd] == 2), context + $" update={update}: panel gate differs.");
                    if (_inventoryScreen.ItemSubmenuActive && rom[0xcbce] != 0)
                        FailIf(_inventoryScreen.ItemSubmenuReady != (rom[0xcbce] == 2) ||
                            _inventoryScreen.ItemSubmenuWidth != rom[0xcbc0] ||
                            _inventoryScreen.ItemSubmenuHeight != rom[0xcbc1] ||
                            _inventoryScreen.ItemSubmenuIndex != rom[0xcbb5], context + $" update={update}: panel rectangle/selection differs.");
                    FailIf(_inventory.EquippedA != rom[0xc689] || _inventory.EquippedB != rom[0xc688] ||
                        _inventory.StorageItemAt(slot) != rom[0xc68a + slot] ||
                        _inventory.SelectedHarpSong != rom[0xc6b7] ||
                        _inventory.SatchelSelectedSeeds != rom[0xc6c4] ||
                        _inventory.ShooterSelectedSeeds != rom[0xc6c5], context + $" update={update}: equipment/selection differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + $" update={update}: cue order differs.");
                    CompareInventoryFramePixelsRom(rom, context + $" update={update}");
                });
            }
            for (int row = 0; row < slot / 4; row++) { Step(1, 0x80); Step(); }
            for (int column = 0; column < slot % 4; column++) { Step(1, 0x10); Step(); }
            Step(2);
            int optionCount = Enumerable.Range(0, item == 0x11 ? 3 : 5).Count(option => (mask & 1 << option) != 0);
            Step(1, button);
            if (optionCount < 2)
            {
                // bank2.inventoryMenuState1 only enters state 2 when the
                // obtained-option popcount exceeds one. Zero/one equip now.
                FailIf(_inventoryScreen.ItemSubmenuActive || (toA ? _inventory.EquippedA : _inventory.EquippedB) != item,
                    context + ": zero/one-option path failed to equip immediately.");
                Step(2); Step(1, button); Step(2); Step(1, button); Step(2);
            }
            else
            {
                Step(1, 8); // Start is ignored while the panel grows.
                Step(25);
                FailIf(!_inventoryScreen.ItemSubmenuReady, context + ": panel failed to finish expansion.");
                Step(1, 0x20); Step(); Step(1, 0x10); Step();
                Step(1, 8); Step(2);
                FailIf(_inventoryScreen.ItemSubmenuActive || !_inventoryMenu.IsOpen ||
                    (toA ? _inventory.EquippedA : _inventory.EquippedB) != item,
                    context + ": Start confirmation failed to equip and retain inventory ownership.");
                Step(1, button); Step(); // Return the parent to its original storage cell.
                Step(1, button); Step(26); Step(1, 8); Step(2);
                FailIf(_inventoryScreen.ItemSubmenuActive || !_inventoryMenu.IsOpen,
                    context + ": repeated panel confirmation failed.");
            }
            _inventoryMenu.CloseImmediatelyForValidation();
            cases++;
        }
        GD.Print($"Validated {cases} clean-US complete inventory panel fixtures: allPositions={allPositions}, source option masks, immediate zero/one equip, both buttons, above/below masks, expansion, confirmation and repeat in split/batched gameplay. Fade/viewport/LCD timing remain outside the frame comparison.");
    }
}
