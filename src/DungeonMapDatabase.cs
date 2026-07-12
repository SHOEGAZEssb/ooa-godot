using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class DungeonMapDatabase
{
    private readonly Dictionary<(int Dungeon, int Room, Vector2I Direction), int> _neighbors = new();

    public DungeonMapDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/dungeon_adjacency.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 4)
                throw new InvalidOperationException($"Malformed dungeon adjacency row: {line}");

            int dungeon = int.Parse(columns[0]);
            int room = Convert.ToInt32(columns[1], 16);
            int neighbor = Convert.ToInt32(columns[3], 16);
            Vector2I direction = columns[2] switch
            {
                "up" => Vector2I.Up,
                "right" => Vector2I.Right,
                "down" => Vector2I.Down,
                "left" => Vector2I.Left,
                _ => throw new InvalidOperationException($"Unknown dungeon direction: {columns[2]}")
            };
            _neighbors[(dungeon, room, direction)] = neighbor;
        }
    }

    public bool TryGetNeighbor(int dungeon, int room, Vector2I direction, out int neighbor)
    {
        return _neighbors.TryGetValue((dungeon, room, direction), out neighbor);
    }
}
