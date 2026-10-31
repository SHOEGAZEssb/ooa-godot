using Godot;

namespace Oracles;

// Isolate target dispatch while the real item producers remain pack-specific.
internal sealed class BombCollisionValidationEntity(Vector2 position)
    : RoomEntityAdapter<Node2D>(new Node2D(), _ => { }), IBombExplosionRoomEntity
{
    public bool CollisionEnabled { get; set; }
    public Rect2 CollisionBounds => new(position - new Vector2(8,8), new(16,16));
    public int CollisionZ => 0;
    public int CollisionZRadius => 7;
    public int Damage => 2;
}
