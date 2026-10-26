using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class AgesRoomContent : IRoomContent
{
    internal static RoomSession CreateSession(
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
        return new RoomSession(new AgesRoomContent(resources, world), startingGroup, startingRoom,
            animationTick, resetAnimationTick, saveData,
            runtimeState ?? OracleRuntimeState.CreateInitialized(saveData.Game), countAsRoomEntry, toggleState);
    }

    private readonly AgesRoomSessionResources _resources;
    private RoomShopDatabase? _shops;
    internal AgesWorldData World { get; }
    public string AssetRoot => World.AssetRoot;
    public DungeonMapDatabase DungeonMaps => _resources.DungeonMaps;
    public DungeonKeyDoorDatabase KeyDoors => _resources.KeyDoors;
    public int RoomLoadMaturity => _resources.GashaSpots.RoomLoadMaturity;

    internal AgesRoomContent(AgesRoomSessionResources? resources, AgesWorldData? world)
    {
        _resources = resources ?? new AgesRoomSessionResources();
        World = world ?? new AgesWorldData();
    }

    public void ActivateRoom(OracleRoomData room) => World.SetCurrentPaletteRoom(room);
    public int GetDungeonIndex(int group, int room) => World.GetDungeonIndex(group, room);
    public byte ShopFlags(int group, int room) =>
        (_shops ??= new RoomShopDatabase(AssetRoot)).InitialFlags(group, room);

    public OracleRoomData LoadRoom(int group, int room, OracleSaveData saveData,
        OracleRuntimeState runtimeState, long animationTick, byte toggleState)
    {
        using var diagnostics = OracleRoomLoadDiagnostics.Begin("layout", group, room);
        using var publication = World.DeferTexturePublication();
        int dataGroup = group is 0 or 1 &&
            saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagLayoutSwap)
            ? group + 2
            : group;
        OracleRoomData loaded = World.LoadRoom(group, room, dataGroup, saveData.ReadField("wAnimalCompanion"),
            World.ResolveLayoutOverride(group, room, saveData),
            World.ResolveJabuTilesetOverride(group, room, saveData, _resources.DungeonMaps));
        diagnostics?.Mark("data");
        byte roomFlags = saveData.GetRoomFlags(group, room);
        // loadTilesetAndRoomLayout reloads the source layout before any
        // substitutions; decoded rooms are cached, including montage writes.
        loaded.ResetAndApplyRoomInitializationChanges(
            new Dictionary<int, byte>(), animationTick);
        _resources.PollutedWaterTiles.Apply(loaded, saveData, animationTick);
        _resources.SingleTileChanges.Apply(
            group, loaded, saveData, animationTick);
        _resources.StandardTileSubstitutions.Apply(loaded, roomFlags, animationTick);
        _resources.SwitchTiles.Apply(group,loaded,runtimeState.ReadField("wSwitchState"),animationTick);
        _resources.ToggleTiles.Apply(group, World.GetDungeonIndex(group, room), toggleState, loaded, animationTick);
        int dungeon = World.GetDungeonIndex(group, room);
        int? floor = dungeon == 7 && _resources.DungeonMaps.GetDungeon(7).TryGetRoom(room, out DungeonCell cell)
            ? cell.Floor : null;
        _resources.JabuWaterTiles.Apply(group, dungeon, floor, loaded, saveData, animationTick);
        _resources.TileChanges.Apply(group, loaded, saveData, World, animationTick, runtimeState, floor);
        _resources.GashaSpots.ApplyRoomState(
            group, loaded, saveData, animationTick);
        diagnostics?.Mark("substitutions");
        return loaded;
    }

}
