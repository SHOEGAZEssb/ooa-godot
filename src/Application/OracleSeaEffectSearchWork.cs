using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Room-entry tile searches, before any sea-effect part allocation.</summary>
internal sealed class OracleSeaEffectSearchWork
{
    internal static OracleSeaEffectSearchWork Shared { get; } = new();
    private readonly Dictionary<(int Collisions, int Tile, int Position), int> _clocks = new();
    private readonly int[][] _tiles = new int[6][];

    private OracleSeaEffectSearchWork()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/timing/sea_search_cpu.tsv",
            new GeneratedTableSchema("original sea-effect search work", GeneratedTableKeySemantics.Unique,
                ["collisions", "order", "tile", "position", "cpu-cycles", "source"],
                ["collisions", "order", "position"], headerRequired: true));
        var lists = Enumerable.Range(0, 6).Select(_ => new SortedDictionary<int, int>()).ToArray();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int collisions = Convert.ToInt32(row.RequiredString(0), 16);
            if (collisions is < 0 or >= 6) throw new InvalidOperationException("Unsupported sea-effect collision set.");
            int order = row.Decimal(1, 0, 15);
            int tile = Convert.ToInt32(row.RequiredString(2), 16);
            if (lists[collisions].TryGetValue(order, out int previous) && previous != tile)
                throw new InvalidOperationException("Sea-effect search order changed within a timing group.");
            lists[collisions][order] = tile;
            _clocks.Add((collisions, tile, row.Decimal(3, 0, 191)), row.Decimal(4, 1, 1_000_000));
            _ = row.RequiredString(5);
        }
        for (int collisions = 0; collisions < 6; collisions++)
        {
            _tiles[collisions] = lists[collisions].Values.ToArray();
            if (_tiles[collisions].Length == 0 || _tiles[collisions][^1] != 0)
                throw new InvalidOperationException("Unterminated sea-effect search list.");
        }
    }

    internal int Search(int collisions, ReadOnlySpan<byte> layout)
    {
        if (collisions is < 0 or >= 6 || layout.Length is not (80 or 176))
            throw new ArgumentOutOfRangeException(nameof(collisions));
        foreach (int tile in _tiles[collisions])
        {
            if (tile == 0) return _clocks[(collisions, 0, 0)];
            // findTileInRoom starts at $cfbf and scans backwards. Small
            // rooms occupy ten columns of the same sixteen-byte stride;
            // the remaining storage was cleared by loadRoomLayout.
            for (int position = 191; position >= 0; position--)
            {
                int index = layout.Length == 176 ? position :
                    (position & 15) < 10 && (position >> 4) < 8 ?
                        (position >> 4) * 10 + (position & 15) : -1;
                if (index >= 0 && index < layout.Length && layout[index] == tile)
                    return _clocks[(collisions, tile, position)];
            }
        }
        throw new InvalidOperationException("Missing sea-effect search terminator.");
    }
}
