using System;

namespace oracleofages;

/// <summary>Source work of Link's active-tile sampling and ordered tile-type lookup.</summary>
internal sealed class OracleActiveTileWork
{
    internal static OracleActiveTileWork Shared { get; } = new();
    private readonly int[,,] _clocks = new int[6, 256, 3];

    private OracleActiveTileWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/active_tile_cpu.tsv",
            new GeneratedTableSchema("Link active-tile work", GeneratedTableKeySemantics.Unique,
                ["collisions", "tile", "change", "cpu-cycles", "source"],
                ["collisions", "tile", "change"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int collisions = row.HexByte(0);
            if (collisions >= 6) throw row.Invalid(0, "one of the six source collision lists");
            _clocks[collisions, row.HexByte(1), row.Decimal(2, 0, 2)] = row.Decimal(3, 1, 100_000);
            _ = row.RequiredString(4);
        }
        foreach (int clocks in _clocks)
            if (clocks == 0) throw new InvalidOperationException("Incomplete linkGetActiveTileType work table.");
    }

    internal int Get(int collisions, byte tile, byte position, byte previousTile, byte previousPosition) =>
        _clocks[collisions, tile, position != previousPosition ? 1 : tile != previousTile ? 2 : 0];
}
