using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateInventoryFlagIsolation()
    {
        // Exercise every imported behavior against nonzero neighboring state,
        // including high dungeon bit indices and auxiliary inventory writes.
        for (int treasure = 0; treasure < _treasures.BehaviourCount; treasure++)
        foreach (int parameter in new[] { 0, 1 })
        {
            OracleSaveData save = OracleSaveData.CreateStandardGame();
            byte[] flags = new byte[0x400];
            for (int i = 0; i < flags.Length; i++) flags[i] = (byte)(i * 37 + 0x5a);
            save.WriteWramBytes(0xc700, flags);
            byte[] globals = new byte[0x10];
            for (int i = 0; i < globals.Length; i++) globals[i] = (byte)(i * 19 + 0xa5);
            save.WriteWramBytes(0xc6d0, globals);
            var inventory = new InventoryState(_treasures, save, () => 15);
            inventory.GiveTreasure(treasure, parameter);
            inventory.SelectSatchelSeeds(4);
            inventory.SelectShooterSeeds(3);
            inventory.LoseTreasure(treasure);
            byte[] actual = new byte[flags.Length];
            save.ReadWramBytes(0xc700, actual);
            FailIf(!actual.AsSpan().SequenceEqual(flags),
                $"Treasure ${treasure:x2}:${parameter:x2} corrupted room flags $c700-$caff.");
            actual = new byte[globals.Length];
            save.ReadWramBytes(0xc6d0, actual);
            FailIf(!actual.AsSpan().SequenceEqual(globals),
                $"Treasure ${treasure:x2}:${parameter:x2} corrupted global flags $c6d0-$c6df.");
        }

        OracleSaveData upgradeSave = OracleSaveData.CreateStandardGame();
        var runtime = new OracleRuntimeState();
        var upgrades = new InventoryState(_treasures, upgradeSave, runtimeState: runtime);
        runtime.SetWramByte(OracleRuntimeState.UpgradesObtainedAddress, 0x80);
        upgrades.GiveTreasure(0x60, 0);
        FailIf(!upgrades.HasUpgrade(7) || !upgrades.HasUpgrade(0) ||
            runtime.ReadWramByte(OracleRuntimeState.UpgradesObtainedAddress) != 0x81,
            "Treasure $60 mode $0b did not preserve and set the shared $cca8 upgrade flags.");
        FailIf(!upgrades.LoseTreasure(0x60) || !upgrades.HasTreasure(0x60) ||
            upgradeSave.HasTreasure(0x60) ||
            runtime.ReadWramByte(OracleRuntimeState.UpgradesObtainedAddress) != 0x81,
            "loseTreasure_helper must clear $c6a6 bit 0 without clearing $cca8 bit 0.");
        var sameSession = new InventoryState(_treasures, upgradeSave, runtimeState: runtime);
        FailIf(!sameSession.HasUpgrade(0) || !sameSession.HasUpgrade(7),
            "An inventory view lost the runtime owner's upgrade flags.");
        FailIf(!OracleSaveData.TryDeserialize(upgradeSave.Serialize(), out var restoredSave),
            "Upgrade flag regression could not round-trip the retail save.");
        var newSession = new InventoryState(_treasures, restoredSave);
        FailIf(newSession.HasUpgrade(0) || newSession.HasUpgrade(7),
            "Transient $cca8 upgrade flags leaked into the retail save image.");
        upgrades.GiveTreasure(0x60, 0);
        runtime.SetWramByte(OracleRuntimeState.UpgradesObtainedAddress, 0);
        FailIf(!upgrades.LoseTreasure(0x60) || upgradeSave.HasTreasure(0x60),
            "loseTreasure_helper incorrectly required transient ownership before clearing $c6a6.");
        upgrades.ToggleTreasureObjectForDebug(_treasures.GetObject("TREASURE_OBJECT_60_00"));
        upgrades.ToggleTreasureObjectForDebug(_treasures.GetObject("TREASURE_OBJECT_60_00"));
        FailIf(upgrades.HasUpgrade(0) || upgradeSave.HasTreasure(0x60),
            "The explicit debug upgrade toggle no longer clears both flag representations.");

        OracleSaveData flowerSave = OracleSaveData.CreateStandardGame();
        var flowerInventory = new InventoryState(_treasures, flowerSave);
        int commits = 0;
        bool completeAtCommit = true;
        flowerSave.Changed += () =>
        {
            commits++;
            completeAtCommit &= flowerSave.HasTreasure(0x49) && flowerSave.HasTreasure(0x58);
        };
        flowerInventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_BOMB_FLOWER_00"));
        FailIf(!flowerInventory.HasTreasure(0x49) || !flowerInventory.HasTreasure(0x58) ||
            commits != 1 || !completeAtCommit,
            "giveTreasure_body:@extraItemsToAddTable did not grant Bomb Flower $49 and lower half $58 atomically.");
        FailIf(!OracleSaveData.TryDeserialize(flowerSave.Serialize(), out restoredSave) ||
            !restoredSave!.HasTreasure(0x49) || !restoredSave.HasTreasure(0x58),
            "Bomb Flower $49/$58 ownership flags did not survive the retail save round trip.");
    }
}
