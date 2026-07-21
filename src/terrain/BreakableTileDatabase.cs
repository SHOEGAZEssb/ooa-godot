using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class BreakableTileDatabase
{
    public const int SourceBracelet = 0x00;
    public const int SourceSwordLevel1 = 0x01;
    public const int SourceSwordLevel2 = 0x02;
    public const int SourceExpertsRing = 0x03;
    public const int SourceShovel = 0x06;
    public const int SourceEmberSeed = 0x0c;

    private readonly Dictionary<int, BreakableTileRecord> _records = new();

    public BreakableTileDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/breakable_tiles.tsv",
            new GeneratedTableSchema(
                "breakable tiles",
                GeneratedTableKeySemantics.Unique,
                [
                    "active-collisions", "tile", "mode", "source-mask", "drop",
                    "effect", "replacement", "room-flag-action", "gasha-maturity"
                ],
                ["active-collisions", "tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int activeCollisions = row.Decimal(0, 0, 0xff);
            int tile = row.HexByte(1);
            var record = new BreakableTileRecord(
                activeCollisions,
                tile,
                row.HexByte(2),
                row.HexInt(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.UnsignedDecimal(8));
            _records.Add((activeCollisions << 8) | tile, record);
        }
    }

    public bool TryGet(int activeCollisions, int tile, out BreakableTileRecord record) =>
        _records.TryGetValue((activeCollisions << 8) | tile, out record);

    public readonly record struct BreakableTileRecord(
        int ActiveCollisions,
        int Tile,
        int Mode,
        int SourceMask,
        int Drop,
        int Effect,
        int Replacement,
        int RoomFlagAction,
        int GashaMaturity)
    {
        public bool AllowsSource(int source) => (SourceMask & (1 << source)) != 0;

        public void ApplyPersistentEffects(
            OracleSaveData? saveData,
            int group,
            int room)
        {
            if ((Effect & 0x80) == 0)
                return;
            if (saveData is null)
            {
                throw new InvalidOperationException(
                    $"Breakable tile ${Tile:x2} requires live room-flag state.");
            }

            if (GashaMaturity != 0)
                saveData.AddGashaMaturity(GashaMaturity);
            if (RoomFlagAction == 0xff)
                return;
            if ((RoomFlagAction & 0xc0) != 0)
            {
                throw new InvalidOperationException(
                    $"Breakable tile ${Tile:x2} uses linked room-flag action " +
                    $"${RoomFlagAction:x2}, which is not supported by this break source.");
            }
            saveData.SetRoomFlag(
                group, room, (byte)(1 << (RoomFlagAction & 0x0f)));
        }
    }
}
