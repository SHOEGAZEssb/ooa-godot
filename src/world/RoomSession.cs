using Godot;
using System;

namespace oracleofages;

public sealed class RoomSession
{
    private readonly Func<long> _animationTick;
    private readonly Action _resetAnimationTick;

    public event Action<int, OracleRoomData>? RoomChanged;
    public OracleWorldData World { get; }
    public DungeonMapDatabase DungeonMaps { get; }
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }

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
        CurrentRoom = World.LoadRoom(startingGroup, startingRoom);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    public OracleRoomData Load(int group, int room)
    {
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = World.LoadRoom(group, room);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
        return CurrentRoom;
    }

    public void SetLoadedRoom(int group, OracleRoomData room)
    {
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = room;
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
