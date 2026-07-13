using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class BreakableTileDatabase
{
    public const int SourceBracelet = 0x00;

    private readonly Dictionary<int, BreakableTileRecord> _records = new();

    public BreakableTileDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/metadata/breakable_tiles.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 7)
                throw new InvalidOperationException($"Malformed breakable tile row: {line}");

            int activeCollisions = int.Parse(columns[0]);
            int tile = Convert.ToInt32(columns[1], 16);
            var record = new BreakableTileRecord(
                activeCollisions,
                tile,
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                Convert.ToInt32(columns[5], 16),
                Convert.ToInt32(columns[6], 16));
            _records[(activeCollisions << 8) | tile] = record;
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
        int Replacement)
    {
        public bool AllowsSource(int source) => (SourceMask & (1 << source)) != 0;
    }
}
