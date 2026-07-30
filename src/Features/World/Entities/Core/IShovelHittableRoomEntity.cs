using Godot;

namespace oracleofages;

/// <summary>
/// ITEM_SHOVEL ($15) owns an independently colliding child with radius 3.
/// Only species whose source collision table reacts to that child implement
/// this interface; the tile attempt remains a separate shovel operation.
/// </summary>
internal interface IShovelHittableRoomEntity
{
    bool ApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition);
}
