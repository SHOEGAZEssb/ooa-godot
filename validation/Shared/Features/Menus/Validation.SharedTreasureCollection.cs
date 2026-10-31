using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedTreasureCollection()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool seasons = game == OracleGame.Seasons;
            var save = OracleSaveData.CreateStandardGame(game);
            var treasures = TreasureDatabase.For(OracleAssetPack.ForGame(game));
            var inventory = new InventoryState(treasures, save);
            // treasureCollectionBehaviourTable has $68 triples in both native builds.
            FailIf(treasures.BehaviourCount != 0x68, $"{game}: incomplete native treasure collection dispatch.");
            inventory.GiveTreasure(0x19, 1);
            FailIf(inventory.SeedSatchelLevel != 1 || inventory.EmberSeeds != 0x20 || !inventory.HasTreasure(0x20),
                $"{game}: native Seed Satchel grant must also give $20 Ember Seeds through the shared grant body.");
            inventory.GiveTreasure(0x25, 0);
            FailIf(inventory.HasTreasure(0x11) == seasons,
                "Only Ages' extra-items table gives the Harp when granting Tune of Echoes $25.");

            inventory.GiveTreasure(0x1e, 0);
            inventory.SetScriptedEquippedItems(0x1e, 0);
            inventory.GiveTreasure(0x17, 1);
            FailIf(inventory.HasTreasure(0x1e) == seasons || inventory.EquippedB != (seasons ? 0x17 : 0x1e) ||
                seasons && inventory.FeatherLevel != 1,
                "Seasons' Feather grant must remove Fools' Ore from obtained flags and the first matching inventory slot before granting the Feather.");
            inventory.GiveTreasure(0x45, 0);
            inventory.GiveTreasure(0x46, 0);
            FailIf(inventory.HasTreasure(0x45) == seasons,
                "Only Seasons' Ribbon grant removes Star Ore; Ages' native key grants retain each other.");
            inventory.GiveTreasure(0x50, 0); inventory.GiveTreasure(0x51, 0); inventory.GiveTreasure(0x52, 0);
            FailIf(inventory.HasTreasure(0x50) == seasons || inventory.HasTreasure(0x51) == seasons,
                "Hard Ore removes Red/Blue Ore through Seasons' ordered removal row; Ages' unrelated items retain their flags.");
            int maturity = save.GashaMaturity;
            inventory.GiveTreasure(0x2b, 1);
            FailIf(save.GashaMaturity - maturity != (seasons ? 100 : 36),
                "checkIncreaseGashaMaturityForGettingTreasure adds 100 for a Seasons heart piece, 36 for Ages.");
        }

        var nativeSave = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        nativeSave.WriteField("wNumOreChunks", 0x95); nativeSave.WriteField("wNumOreChunks", 0x09, 1);
        nativeSave.WriteField("wNumRupees", 0x23); nativeSave.WriteField("wNumRupees", 0x01, 1);
        var nativeTreasures = TreasureDatabase.For(SeasonsGameContent.AssetRoot);
        var nativeInventory = new InventoryState(nativeTreasures, nativeSave);
        int totalRupees = nativeInventory.TotalRupeesCollected, capSounds = 0, rupeeChanges = 0;
        nativeInventory.RupeeCapExceeded += () => capSounds++;
        nativeInventory.RupeesChanged += () => rupeeChanges++;
        nativeInventory.GiveTreasure(nativeTreasures.GetObject("TREASURE_OBJECT_ORE_CHUNKS_00"));
        FailIf(nativeInventory.OreChunks != 999 || nativeInventory.Rupees != 123 || capSounds != 1 || rupeeChanges != 0 ||
            nativeInventory.TotalRupeesCollected != totalRupees || nativeSave.ReadField("wNumOreChunks") != 0x99 ||
            nativeSave.ReadField("wNumOreChunks", 1) != 0x09,
            "Seasons mode $0e must add the $0b currency value to Ore Chunks, cap at $0999 and cue overflow without changing rupees or lifetime rupee statistics.");
        nativeInventory.GiveTreasure(nativeTreasures.GetObject("TREASURE_OBJECT_PIRATES_BELL_02"));
        FailIf(nativeSave.ReadField("wPirateBellState") != 1 ||
            nativeTreasures.GetTreasureDisplay(0x4a, 0, nativeInventory) is not { LeftSprite: 0xee, RightSprite: 0xef, TextLow: 0x49 },
            "The cleaned Pirates' Bell grant must write its native state before its shared inventory display selects the cleaned row.");
        var reloaded = new InventoryState(nativeTreasures, nativeSave);
        FailIf(reloaded.OreChunks != 999 || nativeTreasures.GetTreasureDisplay(0x4a, 0, reloaded).LeftSprite != 0xee,
            "Shared Seasons inventory rebind must preserve native Ore Chunks and Pirates' Bell state.");
        nativeSave.WriteField("wNumOreChunks", 0x23); nativeSave.WriteField("wNumOreChunks", 0x01, 1);
        nativeSave.WriteField("wPirateBellState", 0);
        nativeInventory.GiveTreasure(0x05, 1);
        FailIf(nativeInventory.OreChunks != 123 || nativeTreasures.GetTreasureDisplay(0x4a, 0, nativeInventory).LeftSprite != 0xec,
            "Direct native script writes to Ore Chunks/Bell state must remain authoritative through an unrelated inventory grant.");
    }
}
