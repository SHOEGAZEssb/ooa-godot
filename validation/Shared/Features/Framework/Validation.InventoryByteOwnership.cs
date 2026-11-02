
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateInventoryByteOwnership()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var save = OracleSaveData.CreateStandardGame(game);
            var inventory = new InventoryState(TreasureDatabase.For(OracleAssetPack.ForGame(game)), save,
                () => 1, OracleRuntimeState.CreateInitialized(game));
            // Clean-US include/wram.s / compiled symbols, independent of typed field resolution.
            int health = game == OracleGame.Ages ? 0xc6aa : 0xc6a2;
            int essences = game == OracleGame.Ages ? 0xc6bf : 0xc6bb;
            int keys = game == OracleGame.Ages ? 0xc672 : 0xc66e;
            int storage = game == OracleGame.Ages ? 0xc68a : 0xc682;
            int selection = game == OracleGame.Ages ? 0xc6c4 : 0xc6be;
            save.WriteWramByte(essences, 0x08);
            save.WriteWramByte(health, 5);
            save.WriteWramByte(keys + 1, 2);
            save.WriteWramByte(storage, 5);
            save.WriteWramByte(selection, 4);
            save.CommitInventoryChange();
            FailIf(inventory.Essences != 8 || inventory.HealthQuarters != 5 || inventory.GetDungeonSmallKeys(1) != 2 ||
                inventory.StorageItemAt(0) != 5 || inventory.SatchelSelectedSeeds != 4,
                $"{game}: typed inventory must immediately observe raw scalar and array bytes.");
            int commits = 0;
            string notifications = "";
            save.Changed += () => commits++;
            save.Changed += () => notifications += "S";
            inventory.Changed += () => notifications += "I";
            inventory.SetScriptedHealth(6);
            FailIf(save.ReadWramByte(health) != 6 || save.ReadWramByte(essences) != 8 ||
                save.ReadWramByte(keys + 1) != 2 || save.ReadWramByte(storage) != 5 || save.ReadWramByte(selection) != 4 || commits != 1,
                $"{game}: changing health must commit once without copying stale inventory over other native bytes.");
            FailIf(!inventory.TryUseDungeonSmallKey(1) || save.ReadWramByte(keys + 1) != 1,
                $"{game}: typed key consumption must mutate the same native array.");
            inventory.SelectSatchelSeeds(1);
            FailIf(save.ReadWramByte(selection) != 1 || inventory.SatchelSelectedSeeds != 1,
                $"{game}: seed selection must use the live byte without an auxiliary dirty set.");
            save.WriteFields("wTotalRupeesCollected", new byte[] { 0x99, 0x99 });
            commits = 0; notifications = "";
            inventory.AddRupees(1);
            FailIf(commits != 1 || notifications != "SI" || inventory.TotalRupeesCollected != 6363 ||
                !save.HasGlobalFlag(GlobalFlag.Flag10000RupeesCollected),
                $"{game}: the rupee award must publish completed BCD/stat/flag bytes once, then inventory change.");
            save.WriteFields("wTotalEnemiesKilled", new byte[] { 0xe7, 0x03 });
            commits = 0; notifications = "";
            inventory.RecordEnemyKill();
            FailIf(commits != 1 || notifications != "SI" || inventory.TotalEnemiesKilled != 1000 ||
                !save.HasGlobalFlag(GlobalFlag.Flag1000EnemiesKilled),
                $"{game}: the Slayer award must publish completed counters and flags once, then inventory change.");
            bool rejected = false;
            try { _ = save.FieldBytes("wInventoryStorage", OracleSaveData.FileSize); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            FailIf(!rejected, $"{game}: inventory views must remain bounded to the live $0550-byte image.");
        }
    }
}
