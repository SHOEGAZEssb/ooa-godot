using Godot;
using System;

namespace oracleofages;

/// <summary>bank5.calculateAdjacentWallsBitset and checkPositionSurroundedByWalls.</summary>
internal sealed class LinkWallProbe
{
    internal static LinkWallProbe Shared { get; } = new();
    private readonly Vector2I[,] _offsets = new Vector2I[2, 8];

    private LinkWallProbe()
    {
        // This source table was first imported for Switch Hook. The native
        // function is shared by Link placement and enemy capture handlers.
        var table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_wall_probes.tsv",
            new GeneratedTableSchema("Link raw wall probes", GeneratedTableKeySemantics.Unique,
                ["sidescroll", "index", "y", "x", "source"],
                ["sidescroll", "index"], headerRequired: true));
        if (table.Rows.Count != 16)
            throw new InvalidOperationException("calculateAdjacentWallsBitset requires two ordered sets of eight probes.");
        foreach (var row in table.Rows)
        {
            _offsets[row.Decimal(0, 0, 1), row.Decimal(1, 0, 7)] =
                new(row.Decimal(3, -128, 127), row.Decimal(2, -128, 127));
            _ = row.RequiredString(4);
        }
    }

    internal int RawWalls(Vector2 position, bool sidescroll, Func<Vector2, bool> solid)
    {
        var point = (Vector2I)position.Floor();
        int bits = 0;
        for (int i = 0; i < 8; i++)
        {
            point += _offsets[sidescroll ? 1 : 0, i];
            point = new(point.X & 0xff, point.Y & 0xff);
            bits = (bits << 1) | (solid(point) ? 1 : 0);
        }
        return bits;
    }

    internal bool SurroundedByWalls(Vector2 position, bool sidescroll, Func<Vector2, bool> solid) =>
        AllSidesBlocked(RawWalls(position, sidescroll, solid));

    internal static bool AllSidesBlocked(int rawWalls) =>
        // The RRA/RL loop rejects any pair with BOTH probes clear. Do not
        // apply specialObjectUpdateAdjacentWallsBitset's $db/$ee rewrites.
        (rawWalls & 0xc0) != 0 && (rawWalls & 0x30) != 0 &&
        (rawWalls & 0x0c) != 0 && (rawWalls & 0x03) != 0;
}
