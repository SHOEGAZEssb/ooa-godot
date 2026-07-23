using Godot;
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
            BreakableTileRecord record = new BreakableTileRecord(
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
}
