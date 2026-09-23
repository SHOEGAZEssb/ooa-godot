using Godot;

namespace oracleofages;

// The physical reserved item is distinct from the native object it carries.
// Read its live geometry after objects have run, including deletion/bounces.
internal interface IReservedBraceletCollisionRoomEntity
{
    bool TryGetReservedBraceletCollision(out ReservedBraceletCollision collision);
}

internal readonly record struct ReservedBraceletCollision(Rect2 Bounds, int Z, int Radius, int Damage);
