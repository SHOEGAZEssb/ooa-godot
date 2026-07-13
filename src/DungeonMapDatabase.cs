using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class DungeonMapDatabase
{
    private readonly Dictionary<(int Dungeon, int Room, Vector2I Direction), int> _neighbors = new();
    private readonly Dictionary<int, DungeonInfo> _dungeons = new();

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

        LoadMapLayouts();
    }

    public bool TryGetNeighbor(int dungeon, int room, Vector2I direction, out int neighbor)
    {
        return _neighbors.TryGetValue((dungeon, room, direction), out neighbor);
    }

    public DungeonInfo GetDungeon(int dungeon)
    {
        if (!_dungeons.TryGetValue(dungeon, out DungeonInfo? result))
            throw new InvalidOperationException($"Dungeon {dungeon:x2} has no imported map layout.");
        return result;
    }

    private void LoadMapLayouts()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/dungeon_maps.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 9)
                throw new InvalidOperationException($"Malformed dungeon map row: {line}");

            int dungeon = int.Parse(columns[0]);
            int group = int.Parse(columns[1]);
            int floorCount = int.Parse(columns[2]);
            int baseFloor = int.Parse(columns[3]);
            int floor = int.Parse(columns[4]);
            int x = int.Parse(columns[5]);
            int y = int.Parse(columns[6]);
            int room = Convert.ToInt32(columns[7], 16);
            byte properties = Convert.ToByte(columns[8], 16);

            if (!_dungeons.TryGetValue(dungeon, out DungeonInfo? info))
            {
                info = new DungeonInfo(dungeon, group, floorCount, baseFloor);
                _dungeons.Add(dungeon, info);
            }
            if (info.Group != group || info.FloorCount != floorCount || info.BaseFloor != baseFloor)
                throw new InvalidOperationException($"Inconsistent metadata for dungeon {dungeon:x2}.");
            info.AddCell(new DungeonCell(floor, x, y, room, properties));
        }
        if (_dungeons.Count != 16)
            throw new InvalidOperationException($"Expected 16 dungeon map layouts, got {_dungeons.Count}.");
    }

    public sealed class DungeonInfo
    {
        private readonly Dictionary<int, DungeonCell> _roomCells = new();
        private readonly Dictionary<(int Floor, int X, int Y), DungeonCell> _positionCells = new();

        public int Index { get; }
        public int Group { get; }
        public int FloorCount { get; }
        public int BaseFloor { get; }
        public IEnumerable<DungeonCell> Cells => _positionCells.Values;

        internal DungeonInfo(int index, int group, int floorCount, int baseFloor)
        {
            Index = index;
            Group = group;
            FloorCount = floorCount;
            BaseFloor = baseFloor;
        }

        internal void AddCell(DungeonCell cell)
        {
            _roomCells[cell.Room] = cell;
            _positionCells[(cell.Floor, cell.X, cell.Y)] = cell;
        }

        public bool TryGetRoom(int room, out DungeonCell cell) => _roomCells.TryGetValue(room, out cell);
        public bool TryGetCell(int floor, int x, int y, out DungeonCell cell) =>
            _positionCells.TryGetValue((floor, x, y), out cell);
    }

    public readonly record struct DungeonCell(int Floor, int X, int Y, int Room, byte Properties);
}
