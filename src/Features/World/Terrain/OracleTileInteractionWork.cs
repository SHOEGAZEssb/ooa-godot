using System;

namespace oracleofages;

/// <summary>Front-tile sampling and dispatch lookup, before any matched tile handler.</summary>
internal sealed class OracleTileInteractionWork
{
    internal static OracleTileInteractionWork Shared { get; } = new();
    private readonly int[,] _clocks = new int[6, 256];

    private OracleTileInteractionWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/tile_interaction_cpu.tsv",
            new GeneratedTableSchema("front-tile interaction work", GeneratedTableKeySemantics.Unique,
                ["collisions", "tile", "cpu-cycles", "source"], ["collisions", "tile"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int collisions = row.HexByte(0);
            if (collisions >= 6) throw row.Invalid(0, "one of the six source collision lists");
            _clocks[collisions, row.HexByte(1)] = row.Decimal(2, 1, 100_000);
            _ = row.RequiredString(3);
        }
        foreach (int clocks in _clocks)
            if (clocks == 0) throw new InvalidOperationException("Incomplete interactWithTileBeforeLink work table.");
    }

    internal int Get(int collisions, byte tile) => _clocks[collisions, tile];
}
