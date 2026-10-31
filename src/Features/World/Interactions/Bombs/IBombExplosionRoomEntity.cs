using Godot;

namespace Oracles;

internal interface IBombExplosionRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    int CollisionZ { get; }
    int CollisionZRadius { get; }
    int Damage { get; }
}
