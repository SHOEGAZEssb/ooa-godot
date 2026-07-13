using Godot;
using System;

namespace oracleofages;

public sealed class RoomSession
{
    public event Action<int, OracleRoomData>? RoomChanged;
    public OracleWorldData World { get; }
    public DungeonMapDatabase DungeonMaps { get; }
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }

    public RoomSession(int startingGroup, int startingRoom)
    {
        World = new OracleWorldData();
        DungeonMaps = new DungeonMapDatabase();
        ActiveGroup = startingGroup;
        CurrentRoom = World.LoadRoom(startingGroup, startingRoom);
    }

    public OracleRoomData Load(int group, int room)
    {
        ActiveGroup = group;
        CurrentRoom = World.LoadRoom(group, room);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
        return CurrentRoom;
    }

    public void SetLoadedRoom(int group, OracleRoomData room)
    {
        ActiveGroup = group;
        CurrentRoom = room;
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
    }

    public void SetActiveGroup(int group)
    {
        ActiveGroup = group;
    }

    public bool TryGetNeighbor(Vector2I direction, out int room)
    {
        int dungeon = World.GetDungeonIndex(ActiveGroup, CurrentRoom.Id);
        if (dungeon >= 0)
            return DungeonMaps.TryGetNeighbor(dungeon, CurrentRoom.Id, direction, out room);

        int x = CurrentRoom.Id & 0x0f;
        int y = (CurrentRoom.Id >> 4) & 0x0f;
        x += direction.X;
        y += direction.Y;
        if (x < 0 || x > 15 || y < 0 || y > 15)
        {
            room = -1;
            return false;
        }
        room = (y << 4) | x;
        return true;
    }
}
