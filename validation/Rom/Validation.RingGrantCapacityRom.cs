using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingGrantCapacityRom()
    {
        var rom = new TreasureRom();
        int cases = 0;
        foreach (int count in new[] { 0, 1, 9, 10, 15, 16, 17, 63, 64 })
        foreach (int distribution in new[] { 0, 1, 2 })
        foreach (bool holes in new[] { false, true })
        for (int ring = 0; ring < 64; ring++)
        {
            var save = OracleSaveData.CreateStandardGame();
            for (int index = 0; index < count; index++)
            {
                int value = distribution switch { 0 => index, 1 => index & 3, _ => 7 };
                int slot = holes ? index * 64 / count : index;
                save.WriteWramByte(0xc5c0 + slot, (byte)(value | 0x40));
            }
            // A free appraisal can leave a hole with the preceding BCD count.
            // The grant helper realigns and republishes that count itself.
            save.WriteWramByte(0xc6cd, 0x64);
            var inventory = new InventoryState(_treasures, save);
            rom.Seed(save);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                rom.Give(0x2d, ring); inventory.GiveUnappraisedRing(ring);
                string context = $"Ring grant count=${count:x2} distribution={distribution} holes={holes} ring=${ring:x2} repeat={repeat}";
                CompareTreasureSave(rom, save, context);
                int expectedCount = Math.Min(64, count + repeat + 1);
                int expectedBcd = expectedCount / 10 * 16 + expectedCount % 10;
                FailIf(inventory.UnappraisedRingCount != expectedCount ||
                    save.ReadWramByte(0xc6cd) != expectedBcd ||
                    inventory.UnappraisedRingAt(expectedCount - 1) != (ring | 0x40),
                    context + ": stable realignment, append or BCD capacity boundary differs.");
                if (count == 64 && distribution == 0 && repeat == 0)
                    for (int index = 0; index < 63; index++)
                        FailIf(inventory.UnappraisedRingAt(index) != (index | 0x40),
                            context + ": all-unique tie must replace ring $3f at the last slot.");
                cases++;
            }
        }
        GD.Print($"Validated {cases} clean-US ring grants: all 64 IDs, empty/BCD/page/capacity boundaries, raw holes and stale BCD, stable realignment, highest-ID duplicate ties, reverse removal, append and repeated replacement with complete save-byte comparisons. Reward-consumer gameplay handoffs remain outside this helper comparison.");
    }
}
