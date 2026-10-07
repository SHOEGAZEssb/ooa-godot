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
    private readonly SwitchTileReplacementDatabase _switchTiles;
    private readonly DungeonToggleTileDatabase _toggleTiles;
    private readonly JabuWaterTileDatabase _jabuWaterTiles;
    private readonly Func<byte> _toggleState;
    private readonly GashaSpotDatabase _gashaSpots;
    private readonly ChangedTileQueue _changedTiles = new();
    private readonly OracleRuntimeState _runtimeState;
    private readonly TileInfoTextDatabase _tileInfoTexts = new();
    private readonly RoomShopDatabase _shopRooms = new();
    internal bool InShop => _runtimeState.ReadWramByte(WramAddress.wInShop) != 0;

    internal byte TilePushCounter
    {
        get => _runtimeState.ReadWramByte(WramAddress.wPushingAgainstTileCounter);
        set => _runtimeState.SetWramByte(WramAddress.wPushingAgainstTileCounter,value);
    }

    internal byte InformativeTextsShown => _runtimeState.ReadWramByte(WramAddress.wInformativeTextsShown);
    internal bool TileInfoTextShown(int textId) =>
        (InformativeTextsShown & _tileInfoTexts.Get(textId).Mask) != 0;
    internal string? PrepareTileInfoMessage(int textId)
    {
        TilePushCounter = 20; // showInfoTextForTile resets even a suppressed hint.
        var text = _tileInfoTexts.Get(textId);
        byte shown = InformativeTextsShown;
        if ((shown & text.Mask) != 0) return null;
        _runtimeState.SetWramByte(WramAddress.wInformativeTextsShown,(byte)(shown | text.Mask));
        return text.Message;
    }

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
        OracleWorldData? world = null,
        OracleRuntimeState? runtimeState = null)
    {
        _runtimeState = runtimeState ?? new OracleRuntimeState();
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
        _switchTiles = resources.SwitchTiles;
        _toggleTiles = resources.ToggleTiles;
        _jabuWaterTiles = resources.JabuWaterTiles;
        _gashaSpots = resources.GashaSpots;
        DungeonMaps = resources.DungeonMaps;
        ActiveGroup = startingGroup;
        if (countAsRoomEntry)
            _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        CurrentRoom = GetRoom(startingGroup, startingRoom);
        InitializeDungeonReturnState();
        InitializeShopState();
        World.SetCurrentPaletteRoom(CurrentRoom);
        if (countAsRoomEntry)
            MarkRoomVisited(startingGroup, startingRoom);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    public OracleRoomData Load(int group, int room)
    {
        TilePushCounter = 0; // clearMemoryOnScreenReload: $cc5c..$cce8.
        BlockPushAngle = 0; // clearMemoryOnScreenReload: $cc5c..$cce8 includes $cca6.
        _changedTiles.Clear(); // clearMemoryOnScreenReload includes $ccdf/$cce0.
        ClearActiveTileState();
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        InitializeShopState();
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
        InitializeShopState();
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
        TilePushCounter = 0; // disableLcdAndLoadRoom: $cc5c..$cce8.
        BlockPushAngle = 0;
        _changedTiles.Clear(); // disableLcdAndLoadRoom clears wLinkInAir..wcce9.
        ClearActiveTileState();
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        InitializeShopState();
        World.SetCurrentPaletteRoom(CurrentRoom);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        return CurrentRoom;
    }

    public void SetLoadedRoom(int group, OracleRoomData room, bool updateMinimap = true)
    {
        BlockPushAngle = 0; // func_49c9 clears wDisabledObjects..$cce0, including $cca6.
        _changedTiles.Clear(); // Scroll-entry func_49c9 clears these indices before loading.
        ClearActiveTileState();
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_gashaSpots.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = room;
        InitializeShopState();
        World.SetCurrentPaletteRoom(CurrentRoom);
        MarkRoomVisited(group, room.Id, updateMinimap);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
    }

    private void ClearActiveTileState()
    {
        // clearMemoryOnScreenReload and func_49c9 both include this mask.
        _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);
        // Both room reload's $cc5c clear and scroll's $cc8a clear include
        // these shared cube publications; outgoing state0 handlers see zero.
        _runtimeState.SetWramByte(WramAddress.wRotatingCubeColor,0);
        _runtimeState.SetWramByte(WramAddress.wRotatingCubePos,0);
        _runtimeState.SetWramByte(WramAddress.wInShop,0);
        // Both ordinary reload and scroll/cutscene clears include $ccd7.
        _runtimeState.SetWramByte(WramAddress.wInformativeTextsShown,0);
        // clearMemoryOnScreenReload / func_49c9 include $cc99/$cc9a/$cc9f.
        _runtimeState.SetWramByte(WramAddress.wActiveTilePos, 0);
        _runtimeState.SetWramByte(WramAddress.wActiveTileIndex, 0);
        _runtimeState.SetWramByte(WramAddress.wLinkOnChest, 0);
    }

    private void InitializeShopState() =>
        _runtimeState.SetWramByte(WramAddress.wInShop,_shopRooms.InitialFlags(ActiveGroup,CurrentRoom.Id));

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
            World.ResolveLayoutOverride(group, room, _saveData),
            World.ResolveJabuTilesetOverride(group, room, _saveData, DungeonMaps));
        byte roomFlags = _saveData.GetRoomFlags(group, room);
        _singleTileChanges.Apply(
            group, loaded, _saveData, _animationTick());
        _standardTileSubstitutions.Apply(loaded, roomFlags, _animationTick());
        _switchTiles.Apply(group,loaded,_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress),_animationTick());
        _toggleTiles.Apply(group, World.GetDungeonIndex(group, room), _toggleState(), loaded, _animationTick());
        int dungeon = World.GetDungeonIndex(group, room);
        int? floor = dungeon == 7 && DungeonMaps.GetDungeon(7).TryGetRoom(room, out DungeonCell cell)
            ? cell.Floor : null;
        _jabuWaterTiles.Apply(group, dungeon, floor, loaded, _saveData, _animationTick());
        _tileChanges.Apply(group, loaded, _saveData, World, _animationTick(), _runtimeState, floor);
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

    // bank0.loadTilesetAndRoomLayout copies the substituted layout to the
    // shared bank3 buffer without re-entering the room or parsing its objects.
    internal void ReloadCurrentRoomLayout()
    {
        byte[] background=CurrentRoom.CaptureBackgroundMappings();
        OracleRoomData room=GetRoom(ActiveGroup,CurrentRoom.Id);
        if (!ReferenceEquals(room,CurrentRoom))
            throw new InvalidOperationException($"Room${ActiveGroup:x}:${CurrentRoom.Id:x2}: native in-place layout reload changed backing room identity.");
        for (int position=0;position<room.Layout.Length;position++)
            room.SetUnderlyingStorageMetatile(position,room.Layout[position]);
        // loadTilesetAndRoomLayout does not regenerate w3VramTiles/Attributes.
        room.SetBackgroundMappingRectangle(Vector2I.Zero,room.WidthInTiles*2,background,_animationTick());
    }

    internal bool IsLayoutSwapped(int group, int room) =>
        _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap);

    public bool HasVisited(int group, int room) =>
        _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagVisited);

    internal void MarkCurrentRoomVisited() => MarkRoomVisited(ActiveGroup, CurrentRoom.Id);

    private void MarkRoomVisited(int group, int room, bool updateMinimap = true)
    {
        InitializeDungeonReturnState();
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
        }
        if (updateMinimap) UpdateMinimapLocation();
    }

    private void InitializeDungeonReturnState()
    {
        // loadDungeonLayout_b01 copies all eight dungeon-property bytes,
        // including $cc3e. Non-dungeon loads retain the previous properties.
        int dungeon = CurrentDungeonIndex;
        if (dungeon < 0) return;
        _runtimeState.SetWramByte(WramAddress.wDungeonWallmasterDestRoom,
            (byte)DungeonMaps.GetDungeon(dungeon).WallmasterDestinationRoom);
    }

    internal void UpdateMinimapLocation()
    {
        // bank1.s:checkUpdateDungeonMinimap runs after cutscene00 observes
        // scroll mode $01, independently of the destination visit bit.
        byte flags = CurrentRoom.TilesetFlags;
        if ((flags & 0x30) != 0 || (flags & 0x09) == 0) return;
        int dungeon = CurrentDungeonIndex;
        if (dungeon >= 0 && DungeonMaps.GetDungeon(dungeon).TryGetRoom(CurrentRoom.Id, out DungeonCell cell))
        {
            _saveData.WriteWramByte(WramAddress.wMinimapDungeonMapPosition, (byte)(cell.Y * 8 + cell.X));
            _saveData.WriteWramByte(WramAddress.wMinimapDungeonFloor, (byte)cell.Floor);
        }
        _saveData.SetMinimapLocation(ActiveGroup, CurrentRoom.Id);
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
