using Godot;
using System;

namespace oracleofages;

public sealed class RoomSession
{
    private readonly Func<long> _animationTick;
    private readonly Action _resetAnimationTick;
    private readonly OracleSaveData _saveData;
    private readonly SingleTileChangeDatabase _singleTileChanges;
    private readonly RoomTileChangeDatabase _tileChanges;
    private readonly DungeonKeyDoorDatabase _keyDoors;
    private readonly StandardTileSubstitutionDatabase _standardTileSubstitutions;
    private readonly DungeonToggleTileDatabase _toggleTiles;
    private readonly Func<byte> _toggleState;
    private readonly GashaSpotDatabase _gashaSpots;
    private readonly ChangedTileQueue _changedTiles = new();

    // INTERAC$14 state0 writes this shared byte for reserved and dynamic
    // blocks alike. Finishing/deleting the writer does not clear it.
    internal byte BlockPushAngle { get; private set; }
    internal void WriteBlockPushAngle(int angle) => BlockPushAngle = (byte)(angle | 0x80);

    internal int PendingTileGraphics => _changedTiles.Count;
    internal bool TrySetTile(byte position, byte tile) =>
        _changedTiles.TryWrite(position,tile,write => CurrentRoom.SetTileWithoutGraphicsReload(write,_animationTick()));
    internal void UpdateChangedTileGraphics(byte scrollMode) =>
        _changedTiles.UpdateGraphics(scrollMode,write => CurrentRoom.ApplyQueuedTileGraphics(write,_animationTick()));

    public event Action<int, OracleRoomData>? RoomChanged;
    public OracleWorldData World { get; }
    public DungeonMapDatabase DungeonMaps { get; }
    public OracleSaveData SaveData => _saveData;
    internal DungeonKeyDoorDatabase KeyDoors => _keyDoors;
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }
    public int CurrentDungeonIndex => World.GetDungeonIndex(ActiveGroup, CurrentRoom.Id);
    public int MinimapGroup => _saveData.MinimapGroup;
    public int MinimapRoom => _saveData.MinimapRoom;

    public RoomSession(
        int startingGroup,
        int startingRoom,
        Func<long> animationTick,
        Action resetAnimationTick,
        OracleSaveData saveData,
        bool countAsRoomEntry = true,
        Func<byte>? toggleState = null,
        RoomSessionResources? resources = null,
        OracleWorldData? world = null)
    {
        _animationTick = animationTick;
        _resetAnimationTick = resetAnimationTick;
        _saveData = saveData;
        _toggleState = toggleState ?? (() => 0);
        World = world ?? new OracleWorldData();
        resources ??= new RoomSessionResources();
        _singleTileChanges = resources.SingleTileChanges;
        _tileChanges = resources.TileChanges;
        _keyDoors = resources.KeyDoors;
        _standardTileSubstitutions = resources.StandardTileSubstitutions;
        _toggleTiles = resources.ToggleTiles;
        _gashaSpots = resources.GashaSpots;
        DungeonMaps = resources.DungeonMaps;
        ActiveGroup = startingGroup;
        if (countAsRoomEntry)
            _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        CurrentRoom = GetRoom(startingGroup, startingRoom);
        World.SetCurrentPaletteRoom(CurrentRoom);
        if (countAsRoomEntry)
            MarkRoomVisited(startingGroup, startingRoom);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    public OracleRoomData Load(int group, int room)
    {
        BlockPushAngle = 0; // clearMemoryOnScreenReload: $cc5c..$cce8 includes $cca6.
        _changedTiles.Clear(); // clearMemoryOnScreenReload includes $ccdf/$cce0.
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        World.SetCurrentPaletteRoom(CurrentRoom);
        MarkRoomVisited(group, room);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
        return CurrentRoom;
    }

    internal void EnterPreparedRoom()
    {
        _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        CurrentRoom = GetRoom(ActiveGroup, CurrentRoom.Id);
        World.SetCurrentPaletteRoom(CurrentRoom);
        MarkRoomVisited(ActiveGroup, CurrentRoom.Id);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    /// <summary>
    /// Implements the cutscene engine's disableLcdAndLoadRoom path. It swaps
    /// the room backing data without treating the viewed location as a room
    /// Link entered, so no visit/minimap state or ordinary room scripts change.
    /// </summary>
    public OracleRoomData LoadCutsceneRoom(int group, int room)
    {
        BlockPushAngle = 0;
        _changedTiles.Clear(); // disableLcdAndLoadRoom clears wLinkInAir..wcce9.
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        World.SetCurrentPaletteRoom(CurrentRoom);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        return CurrentRoom;
    }

    public void SetLoadedRoom(int group, OracleRoomData room)
    {
        BlockPushAngle = 0; // func_49c9 clears wDisabledObjects..$cce0, including $cca6.
        _changedTiles.Clear(); // Scroll-entry func_49c9 clears these indices before loading.
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = room;
        World.SetCurrentPaletteRoom(CurrentRoom);
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
        OracleRoomData loaded = World.LoadRoom(group, room, dataGroup, _saveData.ReadWramByte(WramAddress.wAnimalCompanion),
            World.ResolveLayoutOverride(group, room, _saveData));
        byte roomFlags = _saveData.GetRoomFlags(group, room);
        _singleTileChanges.Apply(
            group, loaded, _saveData, _animationTick());
        _standardTileSubstitutions.Apply(loaded, roomFlags, _animationTick());
        _toggleTiles.Apply(group, World.GetDungeonIndex(group, room), _toggleState(), loaded, _animationTick());
        _tileChanges.Apply(group, loaded, _saveData, World, _animationTick());
        _gashaSpots.ApplyRoomState(
            group, loaded, _saveData, _animationTick());
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
        // bank1.s:loadDungeonLayout_b01 and checkUpdateDungeonMinimap.
        // Side-view rooms retain the preceding top-down floor/cell.
        byte flags = CurrentRoom.TilesetFlags;
        int dungeon = CurrentDungeonIndex;
        if ((flags & 0x20) == 0 && dungeon >= 0 &&
            DungeonMaps.GetDungeon(dungeon).TryGetRoom(room, out DungeonCell cell))
        {
            _saveData.WriteWramByte(0xc662 + dungeon,
                (byte)(_saveData.DungeonVisitedFloors(dungeon) | (1 << cell.Floor)));
            if ((flags & 0x10) == 0 && (flags & 0x09) != 0)
            {
                _saveData.WriteWramByte(WramAddress.wMinimapDungeonMapPosition, (byte)(cell.Y * 8 + cell.X));
                _saveData.WriteWramByte(WramAddress.wMinimapDungeonFloor, (byte)cell.Floor);
            }
        }
        if ((flags & 0x30) == 0 && (flags & 0x09) != 0)
            _saveData.SetMinimapLocation(group, room);
    }

    public bool TryGetNeighbor(Vector2I direction, out int room)
    {
        return TryGetNeighbor(ActiveGroup, CurrentRoom.Id, direction, out room);
    }

    public bool TryGetNeighbor(
        int group,
        int sourceRoom,
        Vector2I direction,
        out int room)
    {
        int dungeon = World.GetDungeonIndex(group, sourceRoom);
        if (dungeon >= 0)
            return DungeonMaps.TryGetNeighbor(dungeon, sourceRoom, direction, out room);

        // updateActiveRoom adds the signed direction byte to wActiveRoom.
        // Outdoor boundary restrictions belong to screenTransitionState2;
        // forced transitions and indoor maps still use byte wraparound.
        room = (sourceRoom + direction.Y * 16 + direction.X) & 0xff;
        return true;
    }
}
