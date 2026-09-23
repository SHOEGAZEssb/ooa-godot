using System.Collections.Generic;

namespace oracleofages;

// Native collision runs after every object, including interaction scripts.
internal interface IPostObjectLinkContactRoomEntity : ILinkContactEntity
{
    // Collision-created effects join the same post-object spawn batch,
    // before a following dialogue or freeze can defer the part handler.
    void CollectContactSpawns(ICollection<RoomEntitySpawn> spawns) { }
}
