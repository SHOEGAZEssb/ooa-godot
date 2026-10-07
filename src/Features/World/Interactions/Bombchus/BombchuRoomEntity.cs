using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BombchuRoomEntity(BombchuItem item) : RoomEntityAdapter<BombchuItem>(item, item.SetTransitionDrawOffset),
    IFixedRoomEntity, IRoomEntityLifetime, IBombExplosionRoomEntity
{
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.ExplosionCollisionEnabled;
    public Rect2 CollisionBounds => Entity.ExplosionBounds;
    public int CollisionZ => Entity.CollisionZ;
    public int CollisionZRadius => Entity.ExplosionRadius;
    public int Damage => Entity.Damage;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateBombchu(frame, spawns);
}
