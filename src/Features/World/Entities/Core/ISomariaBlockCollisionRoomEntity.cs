using System.Collections.Generic;

namespace Oracles;

// Called in the post-object item scan. Returning true terminates this
// target's scan, even when its imported collision effect is zero.
internal interface ISomariaBlockCollisionRoomEntity
{
    bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns);
}
