using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BombRoomEntity(BombEffect bomb)
    : RoomEntityAdapter<BombEffect>(
        bomb, bomb.SetTransitionDrawOffset),
        IFixedRoomEntity, IBombExplosionRoomEntity, IRoomEntityLifetime
{
    internal BombEffect Bomb => Entity;
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.ExplosionCollisionEnabled;
    public Rect2 CollisionBounds => Entity.ExplosionBounds;
    public int CollisionZ => Entity.CollisionZ;
    public int CollisionZRadius => Entity.ExplosionRadius;
    public int Damage => Entity.Damage;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns);
}

internal interface IBombExplosionRoomEntity
{
    bool CollisionEnabled { get; }
    Rect2 CollisionBounds { get; }
    int CollisionZ { get; }
    int CollisionZRadius { get; }
    int Damage { get; }
}
