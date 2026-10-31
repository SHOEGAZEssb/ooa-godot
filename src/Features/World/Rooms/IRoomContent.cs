namespace Oracles;

internal interface IRoomContent
{
    string AssetRoot { get; }
    BackgroundPaletteState BackgroundPalettes { get; }
    DungeonMapDatabase? DungeonMaps { get; }
    DungeonKeyDoorDatabase? KeyDoors { get; }
    int RoomLoadMaturity { get; }
    bool HasRoom(int group, int room);
    bool RequiresRoomPackFade(int group, int sourceRoom, int targetRoom);
    RoomTexturePublicationScope DeferTexturePublication();
    OracleRoomData LoadRoom(int group, int room, OracleSaveData save,
        OracleRuntimeState memory, long animationTick, byte toggleState);
    void ActivateRoom(OracleRoomData room);
    void InitializeRoomState(int group, OracleRoomData room, OracleSaveData save, OracleRuntimeState memory);
    int GetDungeonIndex(int group, int room);
    byte ShopFlags(int group, int room);
}
