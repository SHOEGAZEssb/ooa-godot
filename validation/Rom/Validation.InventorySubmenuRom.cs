using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateInventorySubmenuHudPixelsRom() => RunInventorySubmenuRom(true);

    private void RunInventorySubmenuRom(bool compareHudPixels)
    {
        int hostCase1 = 0;
        foreach (int maximum in compareHudPixels ? new[] { 12, 57, 64 } : new[] { 12 })
        foreach (int item in new[] { 0x19, 0x0f, 0x11 })
        foreach (bool toA in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)maximum); save.WriteWramByte(0xc6ab, (byte)maximum);
            save.WriteWramByte(WramAddress.wInventoryStorage, (byte)item);
            int flagAddress = WramAddress.wObtainedTreasureFlags + item / 8;
            save.WriteWramByte(flagAddress, (byte)(save.ReadWramByte(flagAddress) | 1 << (item & 7)));
            // Sparse Ember/Pegasus/Mystery selection must retain original seed
            // type IDs; all three songs use the high bits of the same byte.
            save.WriteWramByte(0xc69e, (byte)(item == 0x11 ? 0xe0 : 0x15));
            save.WriteWramByte(WramAddress.wSelectedHarpSong, 1);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareHudPixels) { rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu(); }
            _inventoryMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(1);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(count, Vector2.Right, MenuRomActions(held ?? pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed, ++update);
                    edge = 0;
                    FailIf(_inventoryScreen.ItemSubmenuActive != (rom[0xcbcd] == 2),
                        $"Item submenu ${item:x2} update {update}: state differs from native $cbcd.");
                    if (_inventoryScreen.ItemSubmenuActive && rom[0xcbce] != 0)
                        FailIf(_inventoryScreen.ItemSubmenuReady != (rom[0xcbce] == 2) ||
                            _inventoryScreen.ItemSubmenuWidth != rom[0xcbc0] ||
                            _inventoryScreen.ItemSubmenuHeight != rom[0xcbc1] ||
                            _inventoryScreen.ItemSubmenuIndex != rom[0xcbb5],
                            $"Item submenu ${item:x2} update {update}: rectangle/selection differs from ROM.");
                    FailIf(_inventory.EquippedA != rom[0xc689] || _inventory.EquippedB != rom[0xc688] ||
                        _inventory.StorageItemAt(0) != rom[0xc68a] ||
                        _inventory.SelectedHarpSong != rom[0xc6b7] ||
                        _inventory.SatchelSelectedSeeds != rom[0xc6c4] ||
                        _inventory.ShooterSelectedSeeds != rom[0xc6c5],
                        $"Item submenu ${item:x2} update {update}: source equipment/selection write differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Item submenu ${item:x2} update {update}: menu/selection sound order differs.");
                    if (compareHudPixels) CompareHudPixelsRom(_hud, rom,
                        $"Item submenu HUD ${item:x2} slot={(toA ? 'A' : 'B')} update={update} batch={batched}");
                });
            }
            Step(1, toA ? 1 : 2);
            Step(9, 8); // Start cannot close or confirm the expanding panel.
            Step(16);
            FailIf(!_inventoryScreen.ItemSubmenuReady, "Three-option panel did not finish its bounded expansion.");
            Step(1, 0x20); // Wrap to the last sparse option.
            Step(1, 0x10);
            Step(40, held: 0x10);
            Step(1, held: 0x10);
            Step(1, 8); // Start confirms the selected option; inventory stays open.
            FailIf(!_inventoryMenu.IsOpen || _inventoryScreen.ItemSubmenuActive,
                "Submenu Start must equip and return to inventory without releasing pause.");
            if (compareHudPixels) CloseInventoryHudPixelsRom(rom, batched,
                $"Submenu HUD item=${item:x2} maximum=${maximum:x2}");
            else _inventoryMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US Satchel/Shooter sparse seed IDs and three Harp songs: expanding panel timing, blocked Start, A/B assignment, wrap/repeat and Start confirmation in split/batched application updates.");
    }
}
