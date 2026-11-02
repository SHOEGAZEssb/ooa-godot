
namespace Oracles;

public sealed partial class RoomSession
{
    private readonly IRoomContent _content;
    internal string AssetRoot => _content.AssetRoot;
    internal BackgroundPaletteState BackgroundPalettes => _content.BackgroundPalettes;
    internal bool HasRoom(int group, int room) => _content.HasRoom(group, room);
    internal bool RequiresRoomPackFade(int group, int sourceRoom, int targetRoom) =>
        _content.RequiresRoomPackFade(group, sourceRoom, targetRoom);
    internal RoomTexturePublicationScope DeferTexturePublication() => _content.DeferTexturePublication();
    internal int GetDungeonIndex(int group, int room) => _content.GetDungeonIndex(group, room);
    private readonly Func<long> _animationTick;
    private readonly Action _resetAnimationTick;
    private readonly OracleSaveData _saveData;
    private readonly Func<byte> _toggleState;
    private readonly ChangedTileQueue _changedTiles = new();
    private readonly OracleRuntimeState _runtimeState;
    private TileInfoTextDatabase? _tileInfoTextsSourceCache;
    private TileInfoTextDatabase _tileInfoTexts => _tileInfoTextsSourceCache ??= new(AssetRoot);
    internal bool InShop => _runtimeState.ReadField("wInShop") != 0;

    internal byte TilePushCounter
    {
        get => _runtimeState.ReadField("wPushingAgainstTileCounter");
        set => _runtimeState.SetField("wPushingAgainstTileCounter",value);
    }

    internal byte InformativeTextsShown => _runtimeState.ReadField("wInformativeTextsShown");
    internal string? PrepareTileInfoMessage(int textId)
    {
        TilePushCounter = 20; // showInfoTextForTile resets even a suppressed hint.
        var text = _tileInfoTexts.Get(textId);
        byte shown = InformativeTextsShown;
        if ((shown & text.Mask) != 0) return null;
        _runtimeState.SetField("wInformativeTextsShown",(byte)(shown | text.Mask));
        return text.Message;
    }

    // INTERAC$14 state0 writes this shared byte for reserved and dynamic
    // blocks alike. Finishing/deleting the writer does not clear it.
    internal byte BlockPushAngle => _runtimeState.ReadField("wBlockPushAngle");
    internal void WriteBlockPushAngle(int angle) => _runtimeState.SetField("wBlockPushAngle",(byte)(angle | 0x80));

    internal int PendingTileGraphics => _changedTiles.Count;
    internal bool TrySetTile(byte position, byte tile) =>
        _changedTiles.TryWrite(position,tile,write => CurrentRoom.SetTileWithoutGraphicsReload(write,_animationTick()));
    internal void UpdateChangedTileGraphics(byte scrollMode) =>
        _changedTiles.UpdateGraphics(scrollMode,write => CurrentRoom.ApplyQueuedTileGraphics(write,_animationTick()));

    public event Action<int, OracleRoomData>? RoomChanged;
    public DungeonMapDatabase DungeonMaps => _content.DungeonMaps ??
        throw new NotSupportedException($"{AssetRoot}: dungeon layouts are not bound.");
    public OracleSaveData SaveData => _saveData;
    internal OracleRuntimeState RuntimeState => _runtimeState;
    internal DungeonKeyDoorDatabase KeyDoors => _content.KeyDoors ??
        throw new NotSupportedException($"{AssetRoot}: dungeon key doors are not bound.");
    public int ActiveGroup { get; private set; }
    public OracleRoomData CurrentRoom { get; private set; }
    public int CurrentDungeonIndex => _content.GetDungeonIndex(ActiveGroup, CurrentRoom.Id);
    public int MinimapGroup => _saveData.MinimapGroup;
    public int MinimapRoom => _saveData.MinimapRoom;

    internal RoomSession(IRoomContent content, int startingGroup, int startingRoom,
        Func<long> animationTick, Action resetAnimationTick, OracleSaveData saveData,
        OracleRuntimeState runtimeState, bool countAsRoomEntry = true, Func<byte>? toggleState = null)
    {
        OracleGame game = OracleAssetPack.GameFor(content.AssetRoot);
        if (saveData.Game != game || runtimeState.Game != game)
            throw new ArgumentException("Room content, save and runtime state must belong to the same game.");
        _content = content;
        _animationTick = animationTick;
        _resetAnimationTick = resetAnimationTick;
        _saveData = saveData;
        _runtimeState = runtimeState;
        _toggleState = toggleState ?? (() => 0);
        if (countAsRoomEntry) _runtimeState.SetField("wScrollMode", 1);
        ActiveGroup = startingGroup;
        if (countAsRoomEntry) _saveData.AddGashaMaturity(_content.RoomLoadMaturity);
        CurrentRoom = GetRoom(startingGroup, startingRoom);
        InitializeDungeonReturnState();
        InitializeShopState();
        _content.ActivateRoom(CurrentRoom);
        _content.InitializeRoomState(ActiveGroup, CurrentRoom, _saveData, _runtimeState);
        if (countAsRoomEntry) MarkRoomVisited(startingGroup, startingRoom);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    public OracleRoomData GetRoom(int group, int room)
    {
        OracleRoomData loaded = _content.LoadRoom(group, room, _saveData, _runtimeState,
            _animationTick(), _toggleState());
        if (loaded.AssetRoot != AssetRoot)
            throw new InvalidOperationException($"Room ${group:x1}:${room:x2}: content returned a different game pack.");
        return loaded;
    }

    public OracleRoomData Load(int group, int room)
    {
        ClearScreenTransitionRequest();
        TilePushCounter = 0; // clearMemoryOnScreenReload: $cc5c..$cce8.
        _changedTiles.Clear(); // clearMemoryOnScreenReload includes $ccdf/$cce0.
        _runtimeState.ClearRoomReloadGlobals();
        _runtimeState.SetField("wScrollMode", 1);
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_content.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        InitializeShopState();
        _content.ActivateRoom(CurrentRoom);
        _content.InitializeRoomState(ActiveGroup, CurrentRoom, _saveData, _runtimeState);
        MarkRoomVisited(group, room);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
        return CurrentRoom;
    }

    internal void EnterPreparedRoom()
    {
        _runtimeState.SetField("wScrollMode", 1);
        _saveData.AddGashaMaturity(_content.RoomLoadMaturity);
        CurrentRoom = GetRoom(ActiveGroup, CurrentRoom.Id);
        InitializeShopState();
        _content.ActivateRoom(CurrentRoom);
        _content.InitializeRoomState(ActiveGroup, CurrentRoom, _saveData, _runtimeState);
        MarkRoomVisited(ActiveGroup, CurrentRoom.Id);
        CurrentRoom.UpdateAnimation(_animationTick());
    }

    /// <summary>
    /// Implements the cutscene engine's disableLcdAndLoadRoom path. It swaps
    /// the room backing data without treating the viewed location as a room
    /// Link entered, so no visit/minimap state or ordinary room scripts change.
    /// </summary>
    public OracleRoomData LoadCutsceneRoom(int group, int room,bool restartAnimations=false)
    {
        ClearScreenTransitionRequest();
        TilePushCounter = 0; // disableLcdAndLoadRoom: $cc5c..$cce8.
        _changedTiles.Clear(); // disableLcdAndLoadRoom clears wLinkInAir..wcce9.
        _runtimeState.ClearRoomReloadGlobals();
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        ActiveGroup = group;
        CurrentRoom = GetRoom(group, room);
        InitializeShopState();
        _content.ActivateRoom(CurrentRoom);
        if(restartAnimations)_resetAnimationTick();
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        // disableLcdAndLoadRoom_body restores $01 after func_131f; scripts
        // may subsequently stop the boundary owner by writing $00 again.
        _runtimeState.SetField("wScrollMode", 1);
        return CurrentRoom;
    }
    // bank0.func_131f replaces room layout/graphics without clearing WRAM1,
    // scripts, or Link's live state. This is distinct from a room arrival.
    internal OracleRoomData LoadCutsceneBackground(int group,int room)
    {
        int previousAnimationGroup=CurrentRoom.AnimationGroup;
        ActiveGroup=group;CurrentRoom=GetRoom(group,room);
        _content.ActivateRoom(CurrentRoom);
        SynchronizeAnimation(previousAnimationGroup,CurrentRoom);
        return CurrentRoom;
    }

    internal void ClearScreenVariables()
    {
        // bank0.clearScreenVariables clears this common $30-byte block,
        // independently of reload's wider control/item/tile clear ranges.
        int start = _runtimeState.FieldAddress("wScreenVariables");
        for (int index = 0; index < 0x30; index++) _runtimeState.SetWramByte(start + index, 0);
        _runtimeState.SetField("wLoadedTilesetUniqueGfx", 0xff);
        _runtimeState.SetField("wLoadedTilesetLayout", 0xff);
        _runtimeState.SetField("wLoadedTilesetAnimation", 0xff);
    }

    public void SetLoadedRoom(int group, OracleRoomData room, bool updateMinimap = true)
    {
        _changedTiles.Clear(); // Scroll-entry func_49c9 clears these indices before loading.
        _runtimeState.ClearScrollEntryGlobals();
        int previousAnimationGroup = CurrentRoom.AnimationGroup;
        _saveData.AddGashaMaturity(_content.RoomLoadMaturity);
        ActiveGroup = group;
        CurrentRoom = room;
        InitializeShopState();
        _content.ActivateRoom(CurrentRoom);
        _content.InitializeRoomState(ActiveGroup, CurrentRoom, _saveData, _runtimeState);
        MarkRoomVisited(group, room.Id, updateMinimap);
        SynchronizeAnimation(previousAnimationGroup, CurrentRoom);
        RoomChanged?.Invoke(ActiveGroup, CurrentRoom);
    }

    private void ClearScreenTransitionRequest()
    {
        // clearScreenVariables clears $cd00..$cd2f on full loads;
        // scrolling and func_131f background replacements retain them.
        _runtimeState.SetField("wScreenTransitionDirection",0);
        _runtimeState.SetField("wScreenTransitionDelay",0);
        _runtimeState.SetField("wScreenTransitionState",0);
        _runtimeState.SetField("wScreenTransitionState2",0);
        _runtimeState.SetField("wScreenTransitionState3",0);
    }

    private void InitializeShopState() =>
        _runtimeState.SetField("wInShop",_content.ShopFlags(ActiveGroup,CurrentRoom.Id));

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

    // bank0.loadTilesetAndRoomLayout copies the substituted layout to the
    // shared bank3 buffer without re-entering the room or parsing its objects.
    internal void ReloadCurrentRoomLayout()
    {
        byte[] background=CurrentRoom.CaptureBackgroundMappings();
        OracleRoomData room=GetRoom(ActiveGroup,CurrentRoom.Id);
        if (!ReferenceEquals(room,CurrentRoom))
            throw new InvalidOperationException($"Room${ActiveGroup:x}:${CurrentRoom.Id:x2}: native in-place layout reload changed backing room identity.");
        room.CopyLayoutToUnderlying();
        // loadTilesetAndRoomLayout does not regenerate w3VramTiles/Attributes.
        room.SetBackgroundMappingRectangle(Vector2I.Zero,room.WidthInTiles*2,background,_animationTick());
    }

    // func_131f performs the complete background/collision reload, rather
    // than retaining VRAM mappings as loadTilesetAndRoomLayout alone does.
    internal OracleRoomData ReloadCurrentRoomBackground()
    {
        var room=LoadCutsceneBackground(ActiveGroup,CurrentRoom.Id);
        room.CopyLayoutToUnderlying();
        return room;
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
            _saveData.WriteField("wDungeonVisitedFloors",
                (byte)(_saveData.DungeonVisitedFloors(dungeon) | (1 << cell.Floor)), dungeon);
        }
        if (updateMinimap) UpdateMinimapLocation();
    }

    private void InitializeDungeonReturnState()
    {
        // loadDungeonLayout_b01 copies all eight dungeon-property bytes,
        // including $cc3e. Non-dungeon loads retain the previous properties.
        int dungeon = CurrentDungeonIndex;
        if (dungeon < 0) return;
        _runtimeState.SetField("wDungeonWallmasterDestRoom",
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
            _saveData.WriteField("wMinimapDungeonMapPosition", (byte)(cell.Y * 8 + cell.X));
            _saveData.WriteField("wMinimapDungeonFloor", (byte)cell.Floor);
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
        int dungeon = _content.GetDungeonIndex(group, sourceRoom);
        if (dungeon >= 0)
            return DungeonMaps.TryGetNeighbor(dungeon, sourceRoom, direction, out room);

        // updateActiveRoom adds the signed direction byte to wActiveRoom.
        // Outdoor boundary restrictions belong to screenTransitionState2;
        // forced transitions and indoor maps still use byte wraparound.
        room = (sourceRoom + direction.Y * 16 + direction.X) & 0xff;
        return true;
    }
}
