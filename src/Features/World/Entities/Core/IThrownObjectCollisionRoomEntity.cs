using System.Collections.Generic;

namespace Oracles;

internal interface IThrownObjectCollisionRoomEntity
{
    bool HasThrownObjectCollisionIdentity { get; }
    bool ApplyThrownObjectCollision(ReservedBraceletCollision collision, ICollection<RoomEntitySpawn> spawns);
}
