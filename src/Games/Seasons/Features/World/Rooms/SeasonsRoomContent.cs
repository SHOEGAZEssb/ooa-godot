
namespace Oracles;

internal sealed class SeasonsRoomContent(SeasonsWorldData data) : IRoomContent
{
    private DungeonMapDatabase? _maps;
    private ChestDatabase? _chests;
    private DungeonKeyDoorDatabase? _keyDoors;
    private RoomShopDatabase? _shops;
    private StandardTileSubstitutionDatabase? _substitutions;
    private SingleTileChangeDatabase? _singleTiles;
    public string AssetRoot => SeasonsGameContent.AssetRoot;
    public BackgroundPaletteState BackgroundPalettes => data.Palettes;
    public DungeonMapDatabase DungeonMaps => _maps ??= new(AssetRoot);
    public DungeonKeyDoorDatabase KeyDoors => _keyDoors ??= new(AssetRoot);
    // bank1.s:checkWarpsTopDown adds $05 before admitting the next room.
    public int RoomLoadMaturity => 5;
    public bool HasRoom(int group, int room) => data.HasRoom(group,room);
    public bool RequiresRoomPackFade(int group, int sourceRoom, int targetRoom) =>
        group == 0 && data.RoomPack(sourceRoom) != data.RoomPack(targetRoom);
    public RoomTexturePublicationScope DeferTexturePublication() => data.DeferTexturePublication();

    public OracleRoomData LoadRoom(int group, int room, OracleSaveData save,
        OracleRuntimeState memory, long animationTick, byte toggleState)
    {
        OracleRoomData loaded = data.LoadRoom(room, memory.ReadField("wRoomStateModifier"),group);
        loaded.ResetAndApplyRoomInitializationChanges(new Dictionary<int, byte>(), animationTick);
        (_singleTiles ??= new(AssetRoot)).Apply(group,loaded,save,animationTick);
        (_substitutions ??= new(AssetRoot)).Apply(loaded,save.GetRoomFlags(group,room),animationTick);
        (_chests ??= new(AssetRoot)).ApplyOpenedChest(group, loaded, save, memory, animationTick);
        data.ApplyScenery(loaded, save, animationTick);
        return loaded;
    }

    public void ActivateRoom(OracleRoomData room) => data.ActivateRoom(room);
    public void InitializeRoomState(int group, OracleRoomData room, OracleSaveData save, OracleRuntimeState memory)
    {
        // Seasons initializeRoom retains the season selected by initializeGame/warp/room-pack handling.
    }
    public int GetDungeonIndex(int group, int room)
    {
        return data.GetDungeonIndex(group,room);
    }

    public byte ShopFlags(int group, int room) => (_shops ??= new(AssetRoot)).InitialFlags(group,room);

}
