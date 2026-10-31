using System.Collections.Generic;
using Godot;

namespace Oracles;

internal interface IRoomEntityFactory
{
    string AssetRoot { get; }
    bool CanReconstructDebugSpecialObject(OracleRuntimeState memory, int group, int room) => false;
    void ValidateDebugNativeInteractions(OracleRuntimeState memory) { }
    int OpenShutterTile { get; }
    IEnumerable<bool> PrepareResources();
    IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room);
    bool TryCreateRoomInitializationOverride(int group, OracleRoomData room, out IRoomEntity? entity)
    {
        entity = null;
        return false;
    }
    IEnumerable<IRoomEntity> CreateRoomInitializationEntities(int group, OracleRoomData room) => [];
    IRoomEntity CreateRememberedCompanion(RememberedCompanionAdmission source, OracleRoomData room) =>
        throw new System.NotSupportedException($"{source.Source}: {AssetRoot} has no bound remembered-companion content.");
    IRoomEntity CreateMaple(OracleRoomData room) => throw new System.NotSupportedException(
        $"code/roomInitialization.s:checkAndSpawnMaple: {AssetRoot} has no bound SPECIALOBJECT $0e encounter content.");
    IEnumerable<IRoomEntity> CreateRoomEntities(int group, OracleRoomData room, EnemyPlacementContext placement);
    IEnumerable<IRoomEntity> CreateStaticRoomEntities(int group, OracleRoomData room) => [];
    IRoomEntity CreateStaticObject(StaticObjectRecord source, OracleRoomData room) =>
        throw new System.NotSupportedException($"{source.Source}: {AssetRoot} has no content binding for static type${source.Type:x2}, ID${source.Id:x2}:${source.SubId:x2}.");
    IEnumerable<IRoomEntity> CreateRoomLoadEntities(int group, OracleRoomData room, EnemyPlacementContext placement) => [];
    IRoomEntity? CreateStreamInteraction(NativeObjectInstruction source, OracleRoomData room, EnemyPlacementContext placement) =>
        throw new System.NotSupportedException($"{source.Source}: native interaction ${source.Id:x2}:${source.SubId:x2} has no construction binding.");
    IRoomEntity CreatePlacedPart(RoomObjectRecord source, OracleRoomData room) =>
        throw new System.NotSupportedException($"{source.Source}: placed PART ${source.Id:x2}:${source.SubId:x2} has no construction binding.");
    void UpdateSeedTreeRefillState(int group, int room);
    IRoomEntity? CreatePlacedEnemy(EnemyHandlerDescriptor handler, RoomObjectRecord source,
        OracleRoomData room, Vector2 position, int instance, int killableIndex, EnemyPlacementContext placement);
    void RefreshState(IEnumerable<IRoomEntity> entities);
    void BindEntity(IRoomEntity entity);
    IRoomEntity? CreateStandaloneEnemy(int id, int subId, OracleRoomData room, Vector2 position,
        string source, out string error);
}
