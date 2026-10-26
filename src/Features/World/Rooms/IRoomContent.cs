namespace Oracles;

internal interface IRoomContent
{
    string AssetRoot { get; }
    DungeonMapDatabase? DungeonMaps { get; }
    DungeonKeyDoorDatabase? KeyDoors { get; }
    int RoomLoadMaturity { get; }
    OracleRoomData LoadRoom(int group, int room, OracleSaveData save,
        OracleRuntimeState memory, long animationTick, byte toggleState);
    void ActivateRoom(OracleRoomData room);
    int GetDungeonIndex(int group, int room);
    byte ShopFlags(int group, int room);
}
