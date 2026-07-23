using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
public sealed class DungeonInfo
{
    private readonly Dictionary<int, DungeonCell> _roomCells = new();
    private readonly Dictionary<(int Floor, int X, int Y), DungeonCell> _positionCells = new();
    public int Index { get; }
    public int Group { get; }
    public int WallmasterDestinationRoom { get; }
    public int FloorCount { get; }
    public int BaseFloor { get; }
    public byte CompassFloors { get; }
    public IEnumerable<DungeonCell> Cells => _positionCells.Values;

    internal DungeonInfo(
        int index,
        int group,
        int wallmasterDestinationRoom,
        int floorCount,
        int baseFloor,
        byte compassFloors)
    {
        Index = index;
        Group = group;
        WallmasterDestinationRoom = wallmasterDestinationRoom;
        FloorCount = floorCount;
        BaseFloor = baseFloor;
        CompassFloors = compassFloors;
    }

    internal void AddCell(DungeonCell cell)
    {
        _roomCells[cell.Room] = cell;
        _positionCells[(cell.Floor, cell.X, cell.Y)] = cell;
    }

    public bool TryGetRoom(int room, out DungeonCell cell) => _roomCells.TryGetValue(room, out cell);
    public bool TryGetCell(int floor, int x, int y, out DungeonCell cell) => _positionCells.TryGetValue((floor, x, y), out cell);
}
