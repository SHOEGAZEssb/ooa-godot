using Godot;

namespace oracleofages;

// Declared ENEMY fields exercise the item/parent loop and native live-slot
// reference; this fixture does not stand in for an enemy AI/combat regression.
internal sealed class DeclaredBombchuTarget(Vector2 position) : RoomEntityAdapter<Node2D>(new Node2D { Position = position }, _ => { }),
    IBombchuTargetRoomEntity, IRoomEntityLifetime
{
    internal int Health { get; set; } = 8;
    internal int Id { get; set; } = 0x32;
    public bool Finished { get; set; }
    public BombchuTarget BombchuTarget => new(Id, Entity.Visible, Entity.Position,
        new(Entity.Position - new Vector2(4, 4), new Vector2(8, 8)), Health);
}
