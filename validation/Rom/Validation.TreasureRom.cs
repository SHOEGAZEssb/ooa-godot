using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void CompareTreasureSave(TreasureRom rom, OracleSaveData save, string context)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++)
            if (rom[address] != save.ReadWramByte(address))
                FailIf(true, $"ROM treasure {context}: WRAM ${address:x4}, ROM=${rom[address]:x2}, runtime=${save.ReadWramByte(address):x2}.");
    }
    private void ValidateTreasureArithmeticRom()
    {
        var rom = new TreasureRom();
        int cases = 0;
        // Original rupee table, including shop prices beyond the drop values.
        int[] values = [0, 1, 2, 5, 10, 20, 40, 30, 60, 70, 25, 50, 100, 200, 400, 150, 300, 500, 900, 80, 999];
        for (int current = 0; current <= 999; current++)
        for (int type = 0; type < values.Length; type++)
        foreach (bool subtract in new[] { false, true })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6ad, (byte)((current / 10 % 10) * 16 + current % 10));
            save.WriteWramByte(0xc6ae, (byte)(current / 100));
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save);
            if (subtract)
            {
                rom.RemoveRupees(type); inventory.AddRupees(-values[type]);
            }
            else { rom.Give(0x28, type); inventory.GiveTreasure(0x28, type); }
            CompareTreasureSave(rom, save, $"rupees={current}, type=${type:x2}, subtract={subtract}");
            cases++;
        }
        foreach (int cap in new[] { 0x10, 0x30, 0x50 })
        for (int current = 0; current <= 99; current++)
        foreach (int amount in new[] { 0, 1, 5, 0x10, 0x30, 0x50, 0x99 })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wMaxBombs, (byte)cap);
            save.WriteWramByte(WramAddress.wNumBombs, (byte)(current / 10 * 16 + current % 10));
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save); rom.Give(0x03, amount); inventory.GiveTreasure(0x03, amount);
            CompareTreasureSave(rom, save, $"bombs={current}, cap=${cap:x2}, amount=${amount:x2}"); cases++;
        }
        for (int level = 1; level <= 3; level++)
        for (int treasure = 0x20; treasure <= 0x24; treasure++)
        for (int current = 0; current <= 99; current++)
        foreach (int amount in new[] { 0, 1, 5, 0x10, 0x20, 0x50, 0x99 })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wSeedSatchelLevel, (byte)level);
            save.WriteWramByte(WramAddress.wNumEmberSeeds + treasure - 0x20, (byte)(current / 10 * 16 + current % 10));
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save); rom.Give(treasure, amount); inventory.GiveTreasure(treasure, amount);
            CompareTreasureSave(rom, save, $"seeds=${treasure:x2}, level={level}, current={current}, amount=${amount:x2}"); cases++;
        }
        foreach (int max in new[] { 12, 16, 40, 80 })
        for (int health = 0; health <= max; health++)
        foreach (int amount in new[] { 0, 1, 4, 0x40 })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wLinkMaxHealth, (byte)max);
            save.WriteWramByte(WramAddress.wLinkHealth, (byte)health);
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save); rom.Give(0x29, amount); inventory.GiveTreasure(0x29, amount);
            CompareTreasureSave(rom, save, $"health={health}/{max}, amount=${amount:x2}"); cases++;
        }
        GD.Print($"Validated {cases} ROM treasure arithmetic cases across rupees/payments, bomb/seed BCD caps and healing, comparing the entire save image.");
    }

    private void ValidateTreasureGrantsRom()
    {
        var rom = new TreasureRom();
        int cases = 0;
        for (int dungeon = 0; dungeon < 16; dungeon++)
        // Native empty-slot search is unbounded. Do not manufacture a full
        // bag of unused item IDs that makes it run into the ownership flags.
        foreach (int occupancy in new[] { 0, 1, 2, 17 })
        foreach (var grant in new (int Id, int Parameter)[]
        {
            (0x01, 1), (0x01, 2), (0x01, 3), (0x05, 1), (0x05, 2), (0x05, 3),
            (0x06, 0), (0x08, 0), (0x0a, 1), (0x0a, 2), (0x0c, 0), (0x0d, 0x10),
            (0x11, 3), (0x16, 2), (0x17, 0), (0x19, 1), (0x25, 0),
            (0x2a, 4), (0x2b, 0), (0x2c, 1), (0x2e, 0),
            (0x30, 0), (0x31, 0), (0x32, 0), (0x33, 0), (0x34, 1),
            (0x40, 7), (0x41, 5), (0x49, 0),
            (0x60, 0), (0x61, 1), (0x62, 2), (0x63, 3), (0x64, 4), (0x65, 5), (0x66, 6), (0x67, 7)
        })
        {
            var save = OracleSaveData.CreateStandardGame();
            for (int slot = 0; slot < occupancy; slot++) save.WriteWramByte(WramAddress.wInventoryB + slot, (byte)(slot + 1));
            save.WriteWramByte(WramAddress.wNumHeartPieces, 2);
            save.WriteWramByte(WramAddress.wLinkHealth, 4);
            save.WriteWramByte(WramAddress.wDungeonSmallKeys + dungeon, 0xfe);
            var runtime = new OracleRuntimeState();
            runtime.SetWramByte(WramAddress.wUpgradesObtained, 0x55);
            var inventory = new InventoryState(_treasures, save, () => dungeon, runtime);
            rom.Seed(save, dungeon, 0x55);
            int notifications = 0;
            inventory.Changed += () => notifications++;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                rom.Give(grant.Id, grant.Parameter); inventory.GiveTreasure(grant.Id, grant.Parameter);
                CompareTreasureSave(rom, save, $"id=${grant.Id:x2}, parameter=${grant.Parameter:x2}, dungeon=${dungeon:x2}, occupancy={occupancy}, repeat={repeat}");
                FailIf(rom[0xcca8] != runtime.ReadWramByte(WramAddress.wUpgradesObtained) || notifications != repeat + 1,
                    $"ROM upgrade/transaction mismatch for treasure ${grant.Id:x2}.");
                cases++;
            }
        }
        foreach (int total in new[] { 0, 1, 999, 9600, 9998, 9999 })
        foreach (bool awarded in new[] { false, true })
        for (int type = 0; type <= 0x14; type++)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc627, (byte)(total / 10 % 10 * 16 + total % 10));
            save.WriteWramByte(0xc628, (byte)(total / 1000 * 16 + total / 100 % 10));
            save.SetGlobalFlag(GlobalFlag.Flag10000RupeesCollected, awarded);
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                rom.Give(0x28, type); inventory.GiveTreasure(0x28, type);
                CompareTreasureSave(rom, save, $"lifetime total={total}, awarded={awarded}, type=${type:x2}, repeat={repeat}");
                cases++;
            }
        }
        GD.Print($"Validated {cases} ROM treasure grants/repeats, inventory placement, extra grants, heart pieces/containers, dungeon indices and upgrade flags with full-save comparisons.");
    }
}
