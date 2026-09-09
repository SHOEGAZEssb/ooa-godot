using Godot;
using System;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateInventoryFidelity()
    {
        // All four ring-box levels, including upward entry into every ring
        // column and skipping nonexistent slots (bank2.s position mappings).
        for (int level = 0; level <= 3; level++)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6cc, (byte)level);
            var inventory = new InventoryState(_treasures, save);
            var screen = new InventoryScreen { Visible = false };
            AddChild(screen);
            screen.Initialize(_treasures, inventory);
            screen.Open();
            screen.BeginNextSubscreen();
            screen.UpdatePageTransition(13.0 / 60);
            for (int column = 0; column < 5; column++)
            {
                int expected = column < inventory.RingBoxCapacity ? 16 + column : 10 + column;
                screen.MoveCursor(Vector2I.Up);
                FailIf(screen.ActiveCursor != expected,
                    $"inventorySubmenu1CheckDirectionButtons: L{level}, column ${column:x2}, " +
                    $"up expected ${expected:x2}, got ${screen.ActiveCursor:x2}.");
                screen.MoveCursor(Vector2I.Down);
                FailIf(screen.ActiveCursor != column, "Ring-row down did not return to its source column.");
                if (column < 4) screen.MoveCursor(Vector2I.Right);
            }
            screen.Close();
            screen.Open();
            screen.BeginNextSubscreen();
            screen.UpdatePageTransition(13.0 / 60);
            FailIf(screen.ActiveCursor != 4, "inventoryMenuState0 reset persistent secondary cursor $cbd1.");
            screen.Close();
            screen.Free();
        }

        var ringSave = OracleSaveData.CreateStandardGame();
        ringSave.WriteWramByte(0xc6cc, 2);
        ringSave.WriteWramByte(0xc6c6, 7);
        ringSave.WriteWramByte(0xc6c7, 0xff);
        var rings = new InventoryState(_treasures, ringSave);
        FailIf(!rings.EquipRingAt(0) || !rings.EquipRingAt(1) || rings.ActiveRing != 0xff ||
            rings.EquipRingAt(1), "@checkEquipRing must allow an empty slot to unequip an active ring exactly once.");

        foreach (bool isA in new[] { false, true })
        {
            var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
            inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
            inventory.GiveTreasure(TreasureDatabase.TreasureShield, 1);
            inventory.GiveTreasure(InventoryState.ItemBiggoronSword, 0);
            int oldB = inventory.EquippedB, oldA = inventory.EquippedA;
            inventory.SwapStorageSlotWithButton(0, isA);
            FailIf(inventory.EquippedB != 0x0c || inventory.EquippedA != 0x0c ||
                inventory.StorageItemAt(0) != (isA ? oldA : oldB) ||
                inventory.StorageItemAt(1) != (isA ? oldB : oldA),
                $"@equipBiggoron did not preserve source storage order for {(isA ? "A" : "B")}.");
            inventory.SwapStorageSlotWithButton(0, isA);
            FailIf(inventory.StorageItemAt(0) != 0x0c ||
                (isA ? inventory.EquippedB : inventory.EquippedA) != 0,
                "@unequipBiggoron did not clear both buttons before the normal swap.");
        }

        var award = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
        award.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        int displacedSword = award.EquippedB;
        award.GiveTreasure(InventoryState.ItemBiggoronSword, 0);
        FailIf(award.EquippedB != 0x0c || award.EquippedA != 0x0c ||
            award.StorageItemAt(0) != displacedSword,
            "addTreasureToInventory did not equip a newly awarded $0c to both buttons and store the displaced sword.");

        var parts = MenuPresentationDatabase.Shared.InventoryMakuSeed;
        FailIf(parts.Count != 4 || parts[0].Tile != 0xfe || parts[0].Attributes != 0x0f ||
            parts[1].Attributes != 0x2f || parts[2].Tile != 0xfa || parts[3].Tile != 0xfc,
            "inventoryMenuDrawSprites@makuSeedSprite lost its source-ordered Ages foreground mask and seed cells.");

        _inventoryMenu.OpenImmediatelyForValidation();
        int initial = _inventoryScreen.Cursor;
        Tick(["move_right"], ["move_right"]);
        for (int update = 0; update < 39; update++) Tick(["move_right"], []);
        FailIf(_inventoryScreen.Cursor != ((initial + 1) & 15), "Inventory direction repeated before $28 held updates.");
        Tick(["move_right"], []);
        FailIf(_inventoryScreen.Cursor != ((initial + 2) & 15), "Inventory direction failed to repeat at $28.");
        for (int update = 0; update < 4; update++) Tick(["move_right"], []);
        FailIf(_inventoryScreen.Cursor != ((initial + 3) & 15), "Inventory repeat interval is not four updates.");
        Tick([], []);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSword, 1);
        int oldEquippedA = _inventory.EquippedA;
        int oldEquippedB = _inventory.EquippedB;
        int cursor = _inventoryScreen.Cursor;
        Tick(["attack", "item"], ["attack", "item"]);
        FailIf(_inventory.EquippedA != oldEquippedA || _inventory.StorageItemAt(cursor) != oldEquippedB,
            "inventoryMenuState1@subscreen0 did not prioritize B over A.");
        _inventoryMenu.CloseImmediatelyForValidation();
        _inventoryMenu.OpenImmediatelyForValidation();
        FailIf(_inventoryScreen.Cursor != cursor, "inventoryMenuState0 reset persistent item cursor $cbd0.");
        _inventoryScreen.BeginNextSubscreen();
        _inventoryScreen.UpdatePageTransition(13.0 / 60);
        // B is ignored on the secondary page; it must not suppress movement.
        int secondary = _inventoryScreen.ActiveCursor;
        Tick(["item", "move_right"], ["item", "move_right"]);
        FailIf(_inventoryScreen.ActiveCursor != (secondary + 1) % 15,
            "Secondary-page B incorrectly suppressed direction input.");
        _inventoryScreen.BeginNextSubscreen();
        _inventoryScreen.UpdatePageTransition(13.0 / 60);
        _inventoryScreen.MoveCursor(Vector2I.Down);
        _inventoryScreen.MoveCursor(Vector2I.Down);
        _inventoryScreen.MoveCursor(Vector2I.Right);
        _inventoryScreen.MoveCursor(Vector2I.Down);
        _inventoryMenu.CloseImmediatelyForValidation();
        _inventoryMenu.OpenImmediatelyForValidation();
        _inventoryScreen.BeginNextSubscreen();
        _inventoryScreen.UpdatePageTransition(13.0 / 60);
        _inventoryScreen.BeginNextSubscreen();
        _inventoryScreen.UpdatePageTransition(13.0 / 60);
        FailIf(_inventoryScreen.ActiveCursor != 0x80,
            "inventoryMenuState0 failed to preserve the right-side bit while resetting $cbb9.");
        _inventoryScreen.MoveCursor(Vector2I.Left);
        FailIf(_inventoryScreen.ActiveCursor != 2, "Reopening on the right lost the low essence bits in $cbd2.");
        int closeSounds = _sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu);
        Tick(["inventory"], ["inventory"]);
        FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != closeSounds + 1,
            "closeMenu did not request SND_CLOSEMENU $55 on Start.");
        _inventoryMenu.CloseImmediatelyForValidation();

        _inventoryMenu.BeginOpeningForValidation();
        Tick(["inventory", "map"], ["map"]);
        for (int update = 1; update < 22; update++) Tick([], []);
        FailIf(!_inventoryMenu.SaveMenuOpen || _inventoryScreen.Visible,
            "menuStateFadeIntoMenu failed to upgrade held Start+Select to MENU_SAVEQUIT $03.");
        Tick(["item"], ["item"]);
        FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndCloseMenu) != closeSounds + 1,
            "MENU_SAVEQUIT cancellation incorrectly requested SND_CLOSEMENU $55.");
        _inventoryMenu.CloseImmediatelyForValidation();

        foreach (int max in new[] { 12, 28, 32, 56, 60, 64 })
        {
            byte[] map = StatusBarLayout.ReadMap(max, 0);
            StatusBarLayout.WriteHearts(map, max, max - 1);
            int columns = max > 56 ? 8 : 7;
            int lastHeart = max / 4 - 1;
            int lastOffset = 0x0d + (max > 56 ? -1 : 0) + lastHeart / columns * 32 + lastHeart % columns;
            FailIf(map[lastOffset] != 0x0e,
                $"drawHeartDisplay lost the final partial heart at max health ${max:x2}.");
        }

        void Tick(string[] held, string[] pressed)
        {
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(held, pressed, Vector2.Zero));
            try { _inventoryMenu.Update(1.0 / 60); }
            finally { Input.EndOriginalUpdate(); }
        }
    }
}
