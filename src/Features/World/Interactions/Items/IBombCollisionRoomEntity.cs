using System.Collections.Generic;

namespace Oracles;

internal interface IBombCollisionRoomEntity
{
    bool HasBombCollisionIdentity { get; }
    bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns);
}
