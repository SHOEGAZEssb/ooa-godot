using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WaterfallWarpDatabase
{
    internal IReadOnlyList<WaterfallWarpRecord> Records { get; }
    internal WaterfallWarpDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/waterfall_warps.tsv",
            new GeneratedTableSchema("specialWarp.s subids $01/$02", GeneratedTableKeySemantics.Unique,
                ["group", "room", "subid", "y", "x", "radius-y", "radius-x", "destination-group",
                 "destination-room", "destination-position", "transition", "exit-y"], ["group", "room"], headerRequired: true));
        var records = new List<WaterfallWarpRecord>();
        foreach (var row in table.Rows)
            records.Add(new(row.HexByte(0), row.HexByte(1), row.HexByte(2),
                new(row.UnsignedDecimal(4), row.UnsignedDecimal(3)),
                new(row.UnsignedDecimal(6), row.UnsignedDecimal(5)),
                new(row.HexByte(0), row.HexByte(1), -1, 0, 0, row.HexByte(7), row.HexByte(8),
                    row.HexByte(9), 0, row.HexByte(10)), row.UnsignedDecimal(11)));
        Records = records;
    }
}

internal sealed record WaterfallWarpRecord(int Group, int Room, int Subid, Vector2 Position,
    Vector2 Radii, Warp Warp, int ExitY);
