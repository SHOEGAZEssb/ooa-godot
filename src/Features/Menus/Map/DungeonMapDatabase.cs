using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class DungeonMapDatabase
{
    private readonly Dictionary<(int Dungeon, int Room, Vector2I Direction), int> _neighbors = new();
    private readonly Dictionary<int, DungeonInfo> _dungeons = new();
    private readonly Dictionary<(int Group, int Room), int> _dungeonByRoom = new();

    public DungeonMapDatabase()
    {
        GeneratedTable adjacency = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_adjacency.tsv",
            new GeneratedTableSchema(
                "dungeon adjacency",
                GeneratedTableKeySemantics.Unique,
                ["dungeon", "room", "direction", "neighbor"],
                ["dungeon", "room", "direction"],
                headerRequired: true));
        foreach (GeneratedTableRow row in adjacency.Rows)
        {
            int dungeon = row.UnsignedDecimal(0);
            int room = row.HexByte(1);
            int neighbor = row.HexByte(3);
            Vector2I direction = row.RequiredString(2) switch
            {
                "up" => Vector2I.Up,
                "right" => Vector2I.Right,
                "down" => Vector2I.Down,
                "left" => Vector2I.Left,
                _ => throw row.Invalid(2, "one of up, right, down, left")
            };
            _neighbors.Add((dungeon, room, direction), neighbor);
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

    public bool TryGetDungeonForRoom(
        int group,
        int room,
        out DungeonInfo dungeon)
    {
        if (_dungeonByRoom.TryGetValue((group, room), out int index) &&
            index >= 0)
        {
            dungeon = GetDungeon(index);
            return true;
        }
        dungeon = null!;
        return false;
    }

    private void LoadMapLayouts()
    {
        GeneratedTable maps = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_maps.tsv",
            new GeneratedTableSchema(
                "dungeon map layouts",
                GeneratedTableKeySemantics.Grouped,
                [
                    "dungeon", "group", "wallmaster-destination", "floors",
                    "base-floor", "compass-floors", "floor", "x", "y", "room",
                    "properties"
                ],
                ["dungeon"],
                headerRequired: true));
        foreach (GeneratedTableRow row in maps.Rows)
        {
            int dungeon = row.UnsignedDecimal(0);
            int group = row.Decimal(1, 0, 7);
            int wallmasterDestination = row.HexByte(2);
            int floorCount = row.UnsignedDecimal(3);
            int baseFloor = row.UnsignedDecimal(4);
            byte compassFloors = (byte)row.HexByte(5);
            int floor = row.UnsignedDecimal(6);
            int x = row.UnsignedDecimal(7);
            int y = row.UnsignedDecimal(8);
            int room = row.HexByte(9);
            byte properties = (byte)row.HexByte(10);

            if (!_dungeons.TryGetValue(dungeon, out DungeonInfo? info))
            {
                info = new DungeonInfo(
                    dungeon, group, wallmasterDestination,
                    floorCount, baseFloor, compassFloors);
                _dungeons.Add(dungeon, info);
            }
            if (info.Group != group || info.FloorCount != floorCount ||
                info.WallmasterDestinationRoom != wallmasterDestination ||
                info.BaseFloor != baseFloor || info.CompassFloors != compassFloors)
                throw new InvalidOperationException($"Inconsistent metadata for dungeon {dungeon:x2}.");
            info.AddCell(new DungeonCell(floor, x, y, room, properties));
            if (!_dungeonByRoom.TryAdd((group, room), dungeon) &&
                _dungeonByRoom[(group, room)] != dungeon)
            {
                // The source includes alternate dungeon layouts that share a
                // room ID. Callers requiring an unambiguous reverse lookup
                // must use RoomSession's active tileset dungeon index there.
                _dungeonByRoom[(group, room)] = -1;
            }
        }
        if (_dungeons.Count != 16)
            throw new InvalidOperationException($"Expected 16 dungeon map layouts, got {_dungeons.Count}.");
    }
}

public readonly record struct DungeonCell(int Floor, int X, int Y, int Room, byte Properties);
