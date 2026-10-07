using System.Collections.Generic;

namespace oracleofages;

internal interface IBombCollisionRoomEntity
{
    bool HasBombCollisionIdentity { get; }
    bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns);
}
