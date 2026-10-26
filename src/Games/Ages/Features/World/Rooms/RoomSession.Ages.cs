using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed partial class RoomSession
{
    private readonly SingleTileChangeDatabase _singleTileChanges;
    private readonly PollutedWaterTileDatabase _pollutedWaterTiles;
    private readonly AgesRoomTileChangeDatabase _tileChanges;
    private readonly StandardTileSubstitutionDatabase _standardTileSubstitutions;
    private readonly SwitchTileReplacementDatabase _switchTiles;
    private readonly DungeonToggleTileDatabase _toggleTiles;
    private readonly JabuWaterTileDatabase _jabuWaterTiles;
    public AgesWorldData World { get; }

    public RoomSession(
        int startingGroup,
        int startingRoom,
        Func<long> animationTick,
        Action resetAnimationTick,
        OracleSaveData saveData,
        bool countAsRoomEntry = true,
        Func<byte>? toggleState = null,
        AgesRoomSessionResources? resources = null,
        AgesWorldData? world = null,
        OracleRuntimeState? runtimeState = null)
    {
        if (saveData.Game != OracleGame.Ages || runtimeState is not null && runtimeState.Game != OracleGame.Ages)
            throw new ArgumentException("The Ages room binding requires Ages save and runtime state.");
        _runtimeState = runtimeState ?? OracleRuntimeState.CreateInitialized(saveData.Game);
        // The session starts at the ordinary initialized-room boundary.
        // bank1.setScreenTransitionState02 selects wScrollMode=$01.
        if (countAsRoomEntry) _runtimeState.SetField("wScrollMode", 1);
        _animationTick = animationTick;
        _resetAnimationTick = resetAnimationTick;
        _saveData = saveData;
        _toggleState = toggleState ?? (() => 0);
        World = world ?? new AgesWorldData();
        resources ??= new AgesRoomSessionResources();
        _singleTileChanges = resources.SingleTileChanges;
        _pollutedWaterTiles = resources.PollutedWaterTiles;
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

    public OracleRoomData GetRoom(int group, int room)
    {
        using var diagnostics = OracleRoomLoadDiagnostics.Begin("layout", group, room);
        using var publication = World.DeferTexturePublication();
        int dataGroup = group is 0 or 1 &&
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap)
            ? group + 2
            : group;
        OracleRoomData loaded = World.LoadRoom(group, room, dataGroup, _saveData.ReadField("wAnimalCompanion"),
            World.ResolveLayoutOverride(group, room, _saveData),
            World.ResolveJabuTilesetOverride(group, room, _saveData, DungeonMaps));
        diagnostics?.Mark("data");
        byte roomFlags = _saveData.GetRoomFlags(group, room);
        // loadTilesetAndRoomLayout reloads the source layout before any
        // substitutions; decoded rooms are cached, including montage writes.
        loaded.ResetAndApplyRoomInitializationChanges(
            new Dictionary<int, byte>(), _animationTick());
        _pollutedWaterTiles.Apply(loaded, _saveData, _animationTick());
        _singleTileChanges.Apply(
            group, loaded, _saveData, _animationTick());
        _standardTileSubstitutions.Apply(loaded, roomFlags, _animationTick());
        _switchTiles.Apply(group,loaded,_runtimeState.ReadField("wSwitchState"),_animationTick());
        _toggleTiles.Apply(group, World.GetDungeonIndex(group, room), _toggleState(), loaded, _animationTick());
        int dungeon = World.GetDungeonIndex(group, room);
        int? floor = dungeon == 7 && DungeonMaps.GetDungeon(7).TryGetRoom(room, out DungeonCell cell)
            ? cell.Floor : null;
        _jabuWaterTiles.Apply(group, dungeon, floor, loaded, _saveData, _animationTick());
        _tileChanges.Apply(group, loaded, _saveData, World, _animationTick(), _runtimeState, floor);
        _gashaSpots.ApplyRoomState(
            group, loaded, _saveData, _animationTick());
        diagnostics?.Mark("substitutions");
        return loaded;
    }

    public void SetLayoutSwapped(int group, int room)
    {
        if (group is not (0 or 1))
            throw new ArgumentOutOfRangeException(
                nameof(group), "ROOMFLAG_LAYOUTSWAP only redirects overworld groups 0 and 1.");
        _saveData.SetRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap);
    }
}
