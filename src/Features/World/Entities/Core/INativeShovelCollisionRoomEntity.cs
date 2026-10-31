using Godot;

namespace Oracles;

internal interface INativeShovelCollisionRoomEntity
{
    bool HasShovelCollisionIdentity { get; }
    bool ApplyNativeShovelCollision(Rect2 bounds, Vector2 origin);
}
