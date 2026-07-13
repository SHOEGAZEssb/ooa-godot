using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomSession
{
    private readonly Func<long> _animationTick;
    private readonly Action _resetAnimationTick;

    public event Action<int, OracleRoomData>? RoomChanged;
    public OracleWorldData World { get; }
    public DungeonMapDatabase DungeonMaps { get; }
    public IReadOnlySet<(int Group, int Room)> VisitedRooms => _visitedRooms;
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }
    public int MinimapGroup { get; private set; }
    public int MinimapRoom { get; private set; }

    private readonly HashSet<(int Group, int Room)> _visitedRooms = new();
    private readonly HashSet<(int Group, int Room)> _layoutSwappedRooms = new();

    public RoomSession(
        int startingGroup,
        int startingRoom,
        Func<long> animationTick,
        Action resetAnimationTick)
    {
        _animationTick = animationTick;
        _resetAnimationTick = resetAnimationTick;
        World = new OracleWorldData();
        DungeonMaps = new DungeonMapDatabase();
        ActiveGroup = startingGroup;
        CurrentRoom = GetRoom(startingGroup, startingRoom);
        MinimapGroup = startingGroup;
        MinimapRoom = startingRoom;
        MarkRoomVisited(startingGroup, startingRoom);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    public OracleRoomData Load(int group, int room)
    {
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        MarkRoomVisited(group, room);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
        return CurrentRoom;
    }

    public void SetLoadedRoom(int group, OracleRoomData room)
    {
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = room;
        MarkRoomVisited(group, room.Id);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
    }

    private void SynchronizeAnimation(int previousAnimationGroup, OracleRoomData room)
    {
        // loadTilesetAnimation preserves wAnimationState when the tileset's
        // animation group is unchanged, and reloads its counters otherwise.
        if (room.AnimationGroup != previousAnimationGroup)
            _resetAnimationTick();
        room.UpdateAnimation(_animationTick());
    }

    public void SetActiveGroup(int group)
    {
        ActiveGroup = group;
    }

    public OracleRoomData GetRoom(int group, int room)
    {
        int dataGroup = group is 0 or 1 && _layoutSwappedRooms.Contains((group, room))
            ? group + 2
            : group;
        return World.LoadRoom(group, room, dataGroup);
    }

    public void SetLayoutSwapped(int group, int room)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(
                nameof(group), "ROOMFLAG_LAYOUTSWAP only redirects overworld groups 0 and 1.");
        _layoutSwappedRooms.Add((group, room));
    }

    internal bool IsLayoutSwapped(int group, int room) =>
        _layoutSwappedRooms.Contains((group, room));

    public bool HasVisited(int group, int room) => _visitedRooms.Contains((group, room));

    private void MarkRoomVisited(int group, int room)
    {
        _visitedRooms.Add((group, room));
        // wMinimapGroup/wMinimapRoom retain the exterior position while Link
        // is inside a house or cave. The current runtime has no save state, so
        // preserve the most recently entered present/past overworld room.
        if (group is 0 or 1)
        {
            MinimapGroup = group;
            MinimapRoom = room;
        }
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
