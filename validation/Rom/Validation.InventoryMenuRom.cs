using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static string[] MenuRomActions(int keys) => new[]
        { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
        .Where((_, bit) => (keys & (1 << bit)) != 0).ToArray();

    private void ValidateInventoryFramePixelsRom() => RunInventoryMenuRom(true, true);

    private void RunInventoryMenuRom(bool compareHudPixels, bool compareFramePixels = false)
    {
        int hostCase1 = 0;
        foreach (int maximum in compareHudPixels ? new[] { 12, 56, 57, 64 } : compareFramePixels ? new[] { 12, 57, 64 } : new[] { 12 })
        foreach (int level in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)maximum); save.WriteWramByte(0xc6ab, (byte)maximum);
            save.WriteWramByte(WramAddress.wRingBoxLevel, (byte)level);
            save.WriteWramByte(WramAddress.wRingBoxContents, 7);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.GiveTreasure(TreasureId.BiggoronSword, 0);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareFramePixels) rom.EnableVramDmaTransfers();
            if (compareHudPixels || compareFramePixels) { rom.LoadHudGraphics(); rom.SaveGraphicsBeforeMenu(); }
            _inventoryMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(1);
            if (compareFramePixels) rom.AdvanceText();
            var sounds = _sound.AttachPlayRequestAudit();
            Vector2 position = _player.Position;
            int update = 0;
            void Step(int updates = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(updates, Vector2.Right, MenuRomActions(held ?? pressed),
                    MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed, ++update);
                    if (compareFramePixels) { rom.PublishHudGraphics(); rom.AdvanceText(); }
                    edge = 0;
                    FailIf(_player.Position != position || !_gameplayPause.IsLeased,
                        "Inventory ROM comparison leaked gameplay movement during modal ownership.");
                    FailIf(_inventoryScreen.PageTransitionActive != (rom[0xcbcd] == 3),
                        $"Inventory update {update}: page transition differs from native $cbcd=${rom[0xcbcd]:x2}.");
                    if (!_inventoryScreen.PageTransitionActive)
                    {
                        int cursor = rom[0xcbcf] switch
                        {
                            0 => rom[0xcbd0], 1 => rom[0xcbd1],
                            _ => (rom[0xcbd2] & 0x80) != 0 ? 0x80 | rom[0xcbb9] : rom[0xcbd2]
                        };
                        FailIf((int)_inventoryScreen.Subscreen != rom[0xcbcf] ||
                            _inventoryScreen.ActiveCursor != cursor,
                            $"Inventory L{level} update {update}: page/cursor {_inventoryScreen.Subscreen}/${_inventoryScreen.ActiveCursor:x2} != ROM ${rom[0xcbcf]:x2}/${cursor:x2}.");
                    }
                    for (int slot = 0; slot < 16; slot++)
                        FailIf(_inventory.StorageItemAt(slot) != rom[0xc68a + slot],
                            $"Inventory update {update}: storage ${slot:x2} differs from ROM.");
                    FailIf(_inventory.EquippedB != rom[0xc688] || _inventory.EquippedA != rom[0xc689] ||
                        _inventory.ActiveRing != rom[0xc6cb],
                        $"Inventory update {update}: button/ring writes differ from native $c688/$c689/$c6cb.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Inventory update {update}: sounds {string.Join(',', sounds.Requests)} != ROM {string.Join(',', rom.Sounds)}.");
                    if (compareHudPixels) CompareHudPixelsRom(_hud, rom,
                        $"Inventory HUD maximum=${maximum:x2} ring-box={level} update={update} batch={batched}");
                    if (compareFramePixels) CompareInventoryFramePixelsRom(rom,
                        $"Inventory frame maximum=${maximum:x2} ring-box={level} update={update} batch={batched}");
                });
            }
            Step(1, 3); // B wins A+B; removing two-button Biggoron sword.
            Step(1, 1); // Re-equip it; native first-empty storage order.
            Step(1, 0x10);
            Step(39, held: 0x10);
            Step(1, held: 0x10);
            Step(12, held: 0x10);
            Step(1, 0xf0); // Right wins all directions.
            Step(1, 4);
            Step(12, 0x10); // Input blocked through the page scroll.
            Step();
            for (int column = 0; column < 5; column++)
            {
                Step(1, 0x40); // Up skips nonexistent ring slots at every level.
                Step(1, 1); // Toggle a ring or an empty slot.
                Step(1, 1);
                Step(1, 0x80);
                Step(1, 0x12); // B ignored on secondary page; Right still moves.
            }
            Step(1, 4);
            Step(13);
            Step(1, 0x20);
            Step(1, 0x80);
            Step(1, 0x10);
            Step(1, 0x40);
            _inventoryMenu.CloseImmediatelyForValidation();
            _inventoryMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(1);
            if (compareFramePixels) rom.AdvanceText();
            sounds.Clear();
            Step(); // Item cursor persists; third-page right cursor resets.
            Step(1, 4);
            Step(13);
            Step(1, 4);
            Step(13);
            if (compareHudPixels) CloseInventoryHudPixelsRom(rom, batched,
                $"Inventory HUD maximum=${maximum:x2} ring-box={level}");
            else _inventoryMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US inventory direction/repeat priority, all ring-box cursor levels, ring toggles, Biggoron A/B storage order, 13-update pages and re-entry through split/batched application updates.");
    }
}
