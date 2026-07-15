using Godot;
using System;

namespace oracleofages;

public sealed class RoomSession
{
    private readonly Func<long> _animationTick;
    private readonly Action _resetAnimationTick;
    private readonly OracleSaveData _saveData;
    private readonly RoomTileChangeDatabase _tileChanges;

    public event Action<int, OracleRoomData>? RoomChanged;
    public OracleWorldData World { get; }
    public DungeonMapDatabase DungeonMaps { get; }
    public OracleSaveData SaveData => _saveData;
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }
    public int MinimapGroup => _saveData.MinimapGroup;
    public int MinimapRoom => _saveData.MinimapRoom;

    public RoomSession(
        int startingGroup,
        int startingRoom,
        Func<long> animationTick,
        Action resetAnimationTick,
        OracleSaveData saveData)
    {
        _animationTick = animationTick;
        _resetAnimationTick = resetAnimationTick;
        _saveData = saveData;
        World = new OracleWorldData();
        _tileChanges = new RoomTileChangeDatabase();
        DungeonMaps = new DungeonMapDatabase();
        ActiveGroup = startingGroup;
        CurrentRoom = GetRoom(startingGroup, startingRoom);
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

    /// <summary>
    /// Implements the cutscene engine's disableLcdAndLoadRoom path. It swaps
    /// the room backing data without treating the viewed location as a room
    /// Link entered, so no visit/minimap state or ordinary room scripts change.
    /// </summary>
    public OracleRoomData LoadCutsceneRoom(int group, int room)
    {
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
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
        int dataGroup = group is 0 or 1 &&
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap)
            ? group + 2
            : group;
        OracleRoomData loaded = World.LoadRoom(group, room, dataGroup);
        _tileChanges.Apply(group, loaded, _saveData, World, _animationTick());
        return loaded;
    }

    public void SetLayoutSwapped(int group, int room)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(
                nameof(group), "ROOMFLAG_LAYOUTSWAP only redirects overworld groups 0 and 1.");
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap);
    }

    internal bool IsLayoutSwapped(int group, int room) =>
        _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap);

    public bool HasVisited(int group, int room) =>
        _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagVisited);

    private void MarkRoomVisited(int group, int room)
    {
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagVisited);
        // wMinimapGroup/wMinimapRoom retain the exterior position while Link
        // is inside a house or cave, so preserve the most recently entered
        // present/past overworld room in the save image.
        if (group is 0 or 1)
            _saveData.SetMinimapLocation(group, room);
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
