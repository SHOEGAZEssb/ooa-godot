using System.Collections.Generic;

namespace Oracles;

internal interface IRoomEntityFactory
{
    string AssetRoot { get; }
    int OpenShutterTile { get; }
    IEnumerable<bool> PrepareResources();
    IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room);
    IEnumerable<IRoomEntity> CreateRoomEntities(int group, OracleRoomData room, EnemyPlacementContext placement);
    IRoomEntity? CreateRoomLoadReward(int group, int room);
    void UpdateSeedTreeRefillState(int group, int room);
    IEnumerable<IRoomEntity> ParseRandomEnemies(RoomObjectRecord source, OracleRoomData room,
        EnemyPlacementContext placement, EnemyPlacementReservations? reservations = null);
    void RefreshState(IEnumerable<IRoomEntity> entities);
    void BindEntity(IRoomEntity entity);
}
