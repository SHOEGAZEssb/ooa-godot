using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal interface IBoomerangCollisionRoomEntity
{
    BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns);
}

// An effect$00 contact ends this target's scan without returning the item.
internal readonly record struct BoomerangCollisionResponse(bool Contact, bool Returns, Vector2? Clink = null)
{
    internal static Vector2 Midpoint(Vector2 target, Vector2 item)
    {
        static int Axis(float target, float item) => unchecked((byte)((int)target +
            (unchecked((sbyte)(byte)((int)item - (int)target)) >> 1)));
        return new(Axis(target.X, item.X), Axis(target.Y, item.Y));
    }
}
