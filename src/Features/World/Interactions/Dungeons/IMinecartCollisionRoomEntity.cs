using System.Collections.Generic;

namespace Oracles;

internal interface IMinecartCollisionRoomEntity
{
    bool ApplyMinecartCollision(MinecartCollisionRoomEntity item, ICollection<RoomEntitySpawn> spawns);
}
