
namespace Oracles;

internal sealed class SeedTreeRefillData
{
    private readonly (int Group, int Room, int BitIndex)[] _locations;

    internal SeedTreeRefillData(string assetRoot, int count)
    {
        if (count is < 1 or > OracleRuntimeState.SeedTreeRefillLocationCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        var rows = GeneratedTable.Load(assetRoot + "metadata/seed_tree_refills.tsv",
            new("seed-tree refill locations", GeneratedTableKeySemantics.Unique,
                ["index", "group", "room", "bit-index"], ["index"], headerRequired: true)).Rows;
        if (rows.Count != count) throw new InvalidOperationException($"{assetRoot}: expected {count} native seed-tree refill locations.");
        _locations = new (int, int, int)[count];
        for (int index = 0; index < count; index++)
        {
            var row = rows[index];
            if (row.UnsignedDecimal(0) != index) throw row.Invalid(0, "native seed-tree table order");
            _locations[index] = (row.Decimal(1, 0, 7), row.HexByte(2), row.Decimal(3, 0, 15));
        }
    }

    private static bool IsRefilled(OracleRuntimeState memory, int bit) =>
        (memory.ReadField("wSeedTreeRefilledBitset", bit >> 3) & (1 << (bit & 7))) != 0;
    private static void SetRefilled(OracleRuntimeState memory, int bit) =>
        memory.SetField("wSeedTreeRefilledBitset", (byte)(memory.ReadField("wSeedTreeRefilledBitset", bit >> 3) | (1 << (bit & 7))), bit >> 3);

    internal void UpdateRefillState(
        OracleRuntimeState runtime,
        int activeGroup,
        int activeRoom)
    {
        if (activeGroup is < 0 or > 7 || activeRoom is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(activeGroup));

        for (int index = 0; index < _locations.Length; index++)
        {
            var location = _locations[index];
            if (location.Group == activeGroup && location.Room == activeRoom)
            {
                bool full = true;
                for (int slot = 0;
                     slot < OracleRuntimeState.SeedTreeRefillRoomsPerLocation;
                     slot++)
                {
                    if (runtime.ReadSeedTreeRefillRoom(index, slot) == 0)
                    {
                        full = false;
                        break;
                    }
                }
                if (full)
                    SetRefilled(runtime, location.BitIndex);
                // The source clears the history on every tree-screen visit,
                // including visits made before all eight rooms were recorded.
                runtime.ClearSeedTreeRefillRooms(index);
                continue;
            }

            if (IsRefilled(runtime, location.BitIndex))
                continue;

            for (int slot = 0;
                 slot < OracleRuntimeState.SeedTreeRefillRoomsPerLocation;
                 slot++)
            {
                byte remembered =
                    runtime.ReadSeedTreeRefillRoom(index, slot);
                if (remembered == activeRoom)
                    break;
                if (remembered != 0)
                    continue;
                // Only the room byte is stored. Room $00 therefore remains
                // indistinguishable from an empty slot, matching WRAM.
                runtime.SetSeedTreeRefillRoom(
                    index, slot, (byte)activeRoom);
                break;
            }
        }
    }

}
