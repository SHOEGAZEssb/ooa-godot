using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSaveAndQuitToTitle()
    {
        GameSceneGraph gameplayScene = _scene;
        bool quitAfterFailure = false;
        var failedMenu = new InventoryMenuController(
            _inventoryScreen,
            _saveQuitScreen,
            _menuLifecycle,
            () => true,
            () => true,
            () => OracleSaveStore.SaveResult.Failed("validation failure"),
            () => quitAfterFailure = true,
            _sound.PlaySound);
        failedMenu.OpenSaveImmediatelyForValidation();
        _saveQuitScreen.Move(1);
        _saveQuitScreen.Move(1);
        failedMenu.SelectSaveOptionForValidation();
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames; frame++)
            failedMenu.Update(1.0 / 60.0);
        if (!_saveQuitScreen.SaveErrorVisible ||
            failedMenu.LastSaveError != "validation failure" ||
            !failedMenu.SaveMenuOpen || quitAfterFailure)
        {
            throw new InvalidOperationException(
                "A failed Save and Quit did not remain open with a surfaced retryable error.");
        }
        failedMenu.SelectSaveOptionForValidation();
        if (_saveQuitScreen.SaveErrorVisible || !failedMenu.SaveMenuOpen)
            throw new InvalidOperationException("The Save and Quit failure could not be dismissed for retry.");
        failedMenu.CloseImmediatelyForValidation();

        _inventoryMenu.OpenSaveImmediatelyForValidation();
        _saveQuitScreen.Move(1);
        _saveQuitScreen.Move(1);
        int saveRequests = _inventoryMenu.SaveRequests;
        int saveWrites = _saveWriteRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        for (int frame = 0; frame < InventoryMenuController.SaveSelectionDelayFrames; frame++)
            _inventoryMenu.Update(1.0 / 60.0);
        if (_inventoryMenu.SaveRequests != saveRequests + 1 ||
            _saveWriteRequests != saveWrites + 1 ||
            _inventoryMenu.QuitRequests != 1 || _mainMenu is null || _mainMenuScreen is null ||
            _inventoryMenu.IsActive || !gameplayScene.IsQueuedForDeletion() ||
            _sound.IsQueuedForDeletion() || _mainMenuScreen.GetParent() != this)
        {
            throw new InvalidOperationException(
                "Save and Quit did not save, free the gameplay scene as one lifecycle unit, " +
                "preserve application audio, and return to title/file select after 30 updates.");
        }
        GD.Print("Validated retryable Save and Quit failure handling, successful persistence " +
            "request, one-root gameplay cleanup, persistent application audio, and return to " +
            "title after 30 updates.");
    }

    private void ValidateExplicitSavePersistence()
    {
        int saveWrites = _saveWriteRequests;
        bool flagWasSet = _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagSavedNayru);
        int rupees = _inventory.Rupees;
        int rupeeDelta = rupees == 999 ? -1 : 1;

        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, !flagWasSet);
        _inventory.AddRupees(rupeeDelta);
        _ExitTree();
        _inventory.AddRupees(-rupeeDelta);
        _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, flagWasSet);

        _inventoryMenu.OpenSaveImmediatelyForValidation();
        int menuSaveRequests = _inventoryMenu.SaveRequests;
        _inventoryMenu.SelectSaveOptionForValidation();
        if (_saveWriteRequests != saveWrites ||
            _inventoryMenu.SaveRequests != menuSaveRequests ||
            _saveQuitScreen.DelayCounter != InventoryMenuController.SaveSelectionDelayFrames)
        {
            throw new InvalidOperationException(
                "Ordinary save-image changes, application exit, or Continue unexpectedly wrote the active file.");
        }
        _inventoryMenu.CloseImmediatelyForValidation();

        GD.Print("Validated explicit-only persistence: gameplay changes and Continue remain " +
            "in memory, and application exit does not save.");
    }

    private void ValidateSaveDataFoundation()
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var standardInventory = new InventoryState(_treasures, save);
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared) ||
            save.GetRoomFlags(0, 0x38) != 0 ||
            save.RespawnGroup != 0 || save.RespawnRoom != 0x8a ||
            save.RespawnStateModifier != 0 || save.RespawnFacing != 0 ||
            save.RespawnY != 0x38 || save.RespawnX != 0x48 ||
            standardInventory.HasTreasure(TreasureDatabase.TreasureSword) ||
            standardInventory.SwordLevel != 0 ||
            standardInventory.EquippedA == InventoryState.ItemSword ||
            standardInventory.EquippedB == InventoryState.ItemSword)
        {
            throw new InvalidOperationException(
                "A standard file did not begin swordless, with clear flags and the " +
                "original 0:8a/$38/$48 checkpoint.");
        }

        save.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
        save.SetRoomFlag(2, 0x38, OracleSaveData.RoomFlagLayoutSwap);
        save.SetRoomFlag(6, 0x87, OracleSaveData.RoomFlagItem);
        save.SetRoomFlag(7, 0xa6, OracleSaveData.RoomFlag80);
        save.SetRoomFlag(1, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered);
        save.SetMinimapLocation(1, 0x41);
        save.SetMakuTreeState(1);
        if (!save.HasGlobalFlag(0x0c) ||
            !save.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            !save.HasRoomFlag(4, 0x87, OracleSaveData.RoomFlagItem) ||
            !save.HasRoomFlag(5, 0xa6, OracleSaveData.RoomFlag80) ||
            !save.HasRoomFlag(3, 0x41, OracleSaveData.RoomFlagPortalSpotDiscovered) ||
            save.MinimapGroup != 1 || save.MinimapRoom != 0x41 || save.MakuTreeState != 1)
        {
            throw new InvalidOperationException(
                "Global flags, room flags, or the original 0/2, 1/3, 4/6 group aliases diverged.");
        }

        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWITCH_HOOK_00"));
        inventory.AddRupees(987);
        save.SetDeathRespawnPoint(5, 0xa6, 2, 3, 0x78, 0x88);
        byte[] encoded = save.Serialize();
        if (encoded.Length != OracleSaveData.FileSize ||
            !encoded.AsSpan(2, 8).SequenceEqual("Z21216-0"u8) ||
            !OracleSaveData.TryDeserialize(encoded, out OracleSaveData? decoded))
        {
            throw new InvalidOperationException(
                "The $550-byte Ages save image did not pass its signature/checksum round trip.");
        }

        var restoredInventory = new InventoryState(_treasures, decoded!);
        if (!restoredInventory.HasTreasure(TreasureDatabase.TreasureSwitchHook) ||
            restoredInventory.SwitchHookLevel != 1 ||
            restoredInventory.EquippedB != TreasureDatabase.TreasureSwitchHook ||
            restoredInventory.Rupees != 987 || !decoded!.HasGlobalFlag(0x0c) ||
            !decoded.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap) ||
            decoded.RespawnGroup != 5 || decoded.RespawnRoom != 0xa6 ||
            decoded.RespawnStateModifier != 2 || decoded.RespawnFacing != 3 ||
            decoded.RespawnY != 0x78 || decoded.RespawnX != 0x88)
        {
            throw new InvalidOperationException(
                "Inventory, BCD rupees, story flags, or room flags were lost across save reload.");
        }

        encoded[^1] ^= 0x80;
        if (OracleSaveData.TryDeserialize(encoded, out _))
            throw new InvalidOperationException("A corrupted Ages save checksum was accepted.");

        GD.Print("Validated original $550-byte save signature/checksum, 128 global flags, " +
            "four aliased room-flag tables, initial/current death checkpoint, inventory fields, " +
            "swordless standard-game state, and BCD rupee round trip.");
    }

    private static void ValidateSaveStore()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"ooa-save-validation-{Guid.NewGuid():N}");
        string primary = Path.Combine(directory, "save.sav");
        string backup = primary + ".bak";
        try
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("ONE");
            OracleSaveStore.SaveResult result = OracleSaveStore.Save(save, primary);
            if (!result.Success || !File.Exists(primary) || File.Exists(backup) ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "ONE")
            {
                throw new InvalidOperationException(
                    $"The first-save path failed or invented a backup generation: {result.ErrorMessage}");
            }

            save.SetLinkName("TWO");
            result = OracleSaveStore.Save(save, primary);
            if (!result.Success ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "TWO" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? first) ||
                first!.LinkName != "ONE")
            {
                throw new InvalidOperationException(
                    $"The second save did not rotate generation ONE into .bak: {result.ErrorMessage}");
            }

            save.SetLinkName("THREE");
            result = OracleSaveStore.Save(save, primary);
            if (!result.Success ||
                OracleSaveStore.LoadOrCreate(primary).LinkName != "THREE" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? second) ||
                second!.LinkName != "TWO")
            {
                throw new InvalidOperationException(
                    $"A later save did not preserve the immediately previous generation: {result.ErrorMessage}");
            }

            File.WriteAllBytes(primary, new byte[] { 0xde, 0xad, 0xbe, 0xef });
            OracleSaveData recovered = OracleSaveStore.LoadOrCreate(primary);
            if (recovered.LinkName != "TWO")
                throw new InvalidOperationException("A corrupt primary did not load generation TWO from .bak.");
            recovered.SetLinkName("FOUR");
            result = OracleSaveStore.Save(recovered, primary);
            if (!result.Success || OracleSaveStore.LoadOrCreate(primary).LinkName != "FOUR" ||
                !OracleSaveData.TryDeserialize(File.ReadAllBytes(backup), out OracleSaveData? preserved) ||
                preserved!.LinkName != "TWO")
            {
                throw new InvalidOperationException(
                    "Saving after backup recovery overwrote the good backup with the corrupt primary.");
            }

            string blockingFile = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blockingFile, "blocked");
            result = OracleSaveStore.Save(
                recovered, Path.Combine(blockingFile, "save.sav"));
            if (result.Success || string.IsNullOrWhiteSpace(result.ErrorMessage))
                throw new InvalidOperationException("A save I/O failure escaped or returned success.");
            if (File.Exists(primary + ".tmp") || File.Exists(primary + ".previous.tmp"))
                throw new InvalidOperationException("A completed save left temporary generation files behind.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        GD.Print("Validated durable temporary save serialization, first-save promotion, " +
            "previous-generation .bak rotation, corrupt-primary recovery, cleanup, and surfaced I/O errors.");
    }

    private void ValidateTreasureInterpreter()
    {
        if (_treasures.BehaviourCount != 0x68)
        {
            throw new InvalidOperationException(
                $"Expected `$68 typed treasure behaviours, found {_treasures.BehaviourCount}.");
        }
        for (int treasure = 0; treasure < 0x68; treasure++)
        {
            TreasureDatabase.BehaviourRecord behaviour = _treasures.GetBehaviour(treasure);
            if (behaviour.TreasureId != treasure)
                throw new InvalidOperationException($"Treasure ${treasure:x2} resolved to another behaviour.");
        }

        static TreasureDatabase.TreasureObjectRecord Reward(int treasure, int parameter) => new(
            $"VALIDATION_TREASURE_{treasure:x2}", treasure, 0, parameter, 0xff, 0, string.Empty);

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        int inventoryCommits = 0;
        int saveCommits = 0;
        int rupeeNotifications = 0;
        inventory.Changed += () => inventoryCommits++;
        inventory.RupeesChanged += () => rupeeNotifications++;
        save.Changed += () => saveCommits++;

        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_RUPEES_04"));
        if (inventory.Rupees != 30 || inventoryCommits != 1 || saveCommits != 1 ||
            rupeeNotifications != 1)
        {
            throw new InvalidOperationException(
                "A mode `$0e rupee grant did not commit exactly once.");
        }

        inventoryCommits = saveCommits = 0;
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureSeedSatchel, 1));
        if (inventory.SeedSatchelLevel != 1 || inventory.EmberSeeds != 0x20 ||
            save.ReadWramByte(0xc6b9) != 0x20 || inventoryCommits != 1 || saveCommits != 1)
        {
            throw new InvalidOperationException(
                "The Seed Satchel did not grant and persist its extra 20 Ember Seeds in one transaction.");
        }

        inventory.GiveTreasure(Reward(0x62, 0));
        inventory.GiveTreasure(Reward(0x20, 0x15));
        inventory.GiveTreasure(Reward(0x21, 0x09));
        inventory.GiveTreasure(Reward(0x22, 0x09));
        inventory.GiveTreasure(Reward(0x23, 0x09));
        inventory.GiveTreasure(Reward(0x24, 0x09));
        if (inventory.SeedSatchelLevel != 2 || inventory.EmberSeeds != 0x35 ||
            inventory.ScentSeeds != 0x09 || inventory.PegasusSeeds != 0x09 ||
            inventory.GaleSeeds != 0x09 || inventory.MysterySeeds != 0x09)
        {
            throw new InvalidOperationException(
                "Typed mode `$0f seed counters or the satchel capacity table diverged.");
        }

        inventory.GiveTreasure(Reward(0x0d, 0x10));
        inventory.GiveTreasure(Reward(0x0e, 0x0c));
        inventory.GiveTreasure(Reward(0x52, 0x07));
        inventory.GiveTreasure(Reward(0x07, 0x02));
        inventory.GiveTreasure(Reward(0x08, 0x01));
        inventory.GiveTreasure(Reward(0x51, 0x22));
        inventory.GiveTreasure(Reward(0x15, 0x00));
        if (inventory.Bombchus != 0x10 || inventory.AnimalCompanion != 0x0c ||
            inventory.RememberedCompanionId != 0x07 || inventory.ObtainedSeasons != 0x04 ||
            inventory.MagnetGlovePolarity != 0x01 ||
            save.ReadWramByte(0xc6fb) != 0x23)
        {
            throw new InvalidOperationException(
                "An imported typed WRAM binding did not execute or persist its collection mode.");
        }

        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureBoomerang, 1));
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureFeather, 1));
        inventory.GiveTreasure(Reward(TreasureDatabase.TreasureSlingshot, 1));
        inventory.SelectSatchelSeeds(2);
        inventory.SelectShooterSeeds(3);
        inventory.SelectSlingshotSeeds(4);
        inventory.GiveTreasure(Reward(0x60, 0));
        if (save.ReadWramByte(0xc700) != 1 || save.ReadWramByte(0xc701) != 1 ||
            save.ReadWramByte(0xc6ff) != 1 || save.ReadWramByte(0xc703) != 2 ||
            save.ReadWramByte(0xc704) != 3 || save.ReadWramByte(0xc705) != 4 ||
            !inventory.HasUpgrade(0) || !inventory.HasTreasure(0x60))
        {
            throw new InvalidOperationException(
                "Item levels, selected seeds, or transient wUpgradesObtained were not updated.");
        }

        // These linked-item bytes alias the first room-flag bytes in the original layout.
        // An unrelated inventory update must consume the live byte instead of restoring a stale copy.
        save.SetRoomFlag(0, 0x00, 0x80);
        inventory.GiveTreasure(Reward(0x20, 0x01));
        if (save.ReadWramByte(0xc700) != 0x81)
            throw new InvalidOperationException("Inventory persistence clobbered aliased room flag 0:00.");

        if (!OracleSaveData.TryDeserialize(save.Serialize(), out OracleSaveData? restoredSave))
            throw new InvalidOperationException("Typed treasure WRAM state did not deserialize.");
        var restored = new InventoryState(_treasures, restoredSave);
        if (restored.SeedSatchelLevel != 2 || restored.EmberSeeds != 0x36 ||
            restored.ScentSeeds != 0x09 || restored.Bombchus != 0x10 ||
            restored.BoomerangLevel != 0x81 || restored.FeatherLevel != 1 ||
            restored.SlingshotLevel != 1 || restored.SatchelSelectedSeeds != 2 ||
            restored.ShooterSelectedSeeds != 3 || restored.SlingshotSelectedSeeds != 4)
        {
            throw new InvalidOperationException(
                "Typed treasure variables were lost across the `$550-byte save round trip.");
        }

        OracleSaveData ringSave = OracleSaveData.CreateStandardGame();
        var ringInventory = new InventoryState(_treasures, ringSave);
        ringInventory.GiveTreasure(Reward(TreasureDatabase.TreasureRing, 0x1e));
        if (ringInventory.UnappraisedRingCount != 1 ||
            ringInventory.UnappraisedRingAt(0) != 0x5e ||
            ringInventory.UnappraisedRingAt(1) != 0xff ||
            ringSave.ReadWramByte(0xc6cd) != 0x01)
        {
            throw new InvalidOperationException(
                "Treasure mode `$09 did not append an unappraised ring and update its BCD count.");
        }

        var fullRings = new byte[0x40];
        Array.Fill(fullRings, (byte)0x41);
        ringSave.WriteWramBytes(0xc5c0, fullRings);
        ringSave.WriteWramByte(0xc6cd, 0x64);
        ringInventory = new InventoryState(_treasures, ringSave);
        ringInventory.GiveTreasure(Reward(TreasureDatabase.TreasureRing, 0x02));
        if (ringInventory.UnappraisedRingCount != 0x40 ||
            ringInventory.UnappraisedRingAt(0x3e) != 0x41 ||
            ringInventory.UnappraisedRingAt(0x3f) != 0x42 ||
            ringSave.ReadWramByte(0xc6cd) != 0x64)
        {
            throw new InvalidOperationException(
                "A full unappraised-ring list did not replace a duplicate like mode `$09.");
        }

        GD.Print("Validated all `$68 typed treasure behaviours, seed counters/capacities, " +
            "mode `$09 ring storage, linked item/selection aliases, transient upgrade bits, " +
            "single-commit treasure transactions, and save persistence.");
    }

    private void ValidateDungeonCollectibles()
    {
        int currentDungeon = -1;
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(
            _treasures, save, () => currentDungeon);
        TreasureDatabase.TreasureObjectRecord smallKey =
            _treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03");
        TreasureDatabase.TreasureObjectRecord bossKey =
            _treasures.GetObject("TREASURE_OBJECT_BOSS_KEY_03");
        TreasureDatabase.TreasureObjectRecord compass =
            _treasures.GetObject("TREASURE_OBJECT_COMPASS_02");
        TreasureDatabase.TreasureObjectRecord map =
            _treasures.GetObject("TREASURE_OBJECT_MAP_02");

        void GiveDungeonSet()
        {
            inventory.GiveTreasure(smallKey);
            inventory.GiveTreasure(bossKey);
            inventory.GiveTreasure(compass);
            inventory.GiveTreasure(map);
        }

        // A malformed/non-dungeon reward must not silently mutate dungeon 0.
        GiveDungeonSet();
        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            if (inventory.GetDungeonSmallKeys(dungeon) != 0 ||
                inventory.HasDungeonBossKey(dungeon) ||
                inventory.HasDungeonCompass(dungeon) ||
                inventory.HasDungeonMap(dungeon))
            {
                throw new InvalidOperationException(
                    "A dungeon collectible obtained with wDungeonIndex `$ff mutated dungeon state.");
            }
        }

        int[] boundaryDungeons = { 0, 7, 8, 15 };
        foreach (int dungeon in boundaryDungeons)
        {
            currentDungeon = dungeon;
            GiveDungeonSet();
        }
        currentDungeon = 8;
        inventory.GiveTreasure(smallKey);

        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            bool expected = boundaryDungeons.Contains(dungeon);
            int expectedKeys = dungeon == 8 ? 2 : expected ? 1 : 0;
            if (inventory.GetDungeonSmallKeys(dungeon) != expectedKeys ||
                inventory.HasDungeonBossKey(dungeon) != expected ||
                inventory.HasDungeonCompass(dungeon) != expected ||
                inventory.HasDungeonMap(dungeon) != expected)
            {
                throw new InvalidOperationException(
                    $"Dungeon collectible indexing failed for dungeon {dungeon}: " +
                    $"keys={inventory.GetDungeonSmallKeys(dungeon)}, " +
                    $"boss={inventory.HasDungeonBossKey(dungeon)}, " +
                    $"compass={inventory.HasDungeonCompass(dungeon)}, " +
                    $"map={inventory.HasDungeonMap(dungeon)}.");
            }
        }

        if (save.ReadWramByte(0xc672) != 1 ||
            save.ReadWramByte(0xc679) != 1 ||
            save.ReadWramByte(0xc67a) != 2 ||
            save.ReadWramByte(0xc681) != 1 ||
            save.ReadWramByte(0xc682) != 0x81 ||
            save.ReadWramByte(0xc683) != 0x81 ||
            save.ReadWramByte(0xc684) != 0x81 ||
            save.ReadWramByte(0xc685) != 0x81 ||
            save.ReadWramByte(0xc686) != 0x81 ||
            save.ReadWramByte(0xc687) != 0x81)
        {
            throw new InvalidOperationException(
                "Dungeon collectibles did not retain the original 16 key bytes and " +
                "two-byte boss-key/compass/map bitsets in WRAM.");
        }

        byte[] encoded = save.Serialize();
        if (!OracleSaveData.TryDeserialize(encoded, out OracleSaveData? decoded))
            throw new InvalidOperationException("Dungeon collectible save data did not deserialize.");
        var restored = new InventoryState(_treasures, decoded);
        for (int dungeon = 0; dungeon < 16; dungeon++)
        {
            bool expected = boundaryDungeons.Contains(dungeon);
            int expectedKeys = dungeon == 8 ? 2 : expected ? 1 : 0;
            if (restored.GetDungeonSmallKeys(dungeon) != expectedKeys ||
                restored.HasDungeonBossKey(dungeon) != expected ||
                restored.HasDungeonCompass(dungeon) != expected ||
                restored.HasDungeonMap(dungeon) != expected)
            {
                throw new InvalidOperationException(
                    $"Dungeon {dungeon} collectibles were lost across save serialization.");
            }
        }

        GD.Print("Validated treasure behaviour modes `$87/`$86 against live dungeon " +
            "indices 0, 7, 8, and 15, non-dungeon rejection, both bitset bytes, " +
            "and save round-trip persistence.");
    }

    private void ValidateDeathRespawnCheckpoints()
    {
        if (!_deathRespawnPoints.UpdatesContinuously(0, 0x38) ||
            !_deathRespawnPoints.UpdatesContinuously(1, 0x38) ||
            _deathRespawnPoints.UpdatesContinuously(0, 0x39))
        {
            throw new InvalidOperationException(
                "The imported roomSpecificCode $06 death-respawn room table regressed.");
        }

        int saveWrites = _saveWriteRequests;
        LoadValidationRoom(0, 0x38);
        _player.WarpTo(new Vector2(0x38, 0x58));
        _player.Face(Vector2I.Left);
        _deathRespawnPoints.Update();
        if (_saveData.RespawnGroup != 0 || _saveData.RespawnRoom != 0x38 ||
            _saveData.RespawnFacing != 3 || _saveData.RespawnY != 0x58 ||
            _saveData.RespawnX != 0x38 || _saveWriteRequests != saveWrites)
        {
            throw new InvalidOperationException(
                "Room 0:38 did not update its live death checkpoint without autosaving.");
        }

        LoadValidationRoom(0, 0x39);
        _player.WarpTo(new Vector2(0x78, 0x68));
        _player.Face(Vector2I.Down);
        _deathRespawnPoints.Update();
        if (!OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? restored) ||
            restored!.RespawnGroup != 0 || restored.RespawnRoom != 0x38 ||
            restored.RespawnFacing != 3 || restored.RespawnY != 0x58 ||
            restored.RespawnX != 0x38)
        {
            throw new InvalidOperationException(
                "An ordinary room replaced the checkpoint, or saving captured Link's arbitrary position.");
        }

        GD.Print("Validated imported roomSpecificCode $06 checkpoint rooms and save-image " +
            "retention of the maintained checkpoint instead of Link's arbitrary position.");
    }
}
