using Godot;
using System.Collections.Generic;

namespace oracleofages;

// The original item ID, collision type and damage are independent: a flying
// Mystery Seed changes collisionType while retaining ITEM24's damage byte.
internal interface ISeedCollisionTarget : ISeedHittableRoomEntity
{
    SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 sourcePosition,
        SeedRecord seed, int collisionType, ICollection<RoomEntitySpawn> spawns);
}

// Effect00 ends this enemy's scan without changing either object's bytes.
// Effects0b/21 write a pending item hit but leave its collision bit enabled;
// effect20 also clears that bit, excluding later enemies in the same pass.
internal readonly record struct SeedCollisionResponse(bool Contact, SeedHitResult Effect, bool DisableCollision);
