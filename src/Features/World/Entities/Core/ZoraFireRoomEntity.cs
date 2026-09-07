using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class ZoraFireRoomEntity(ZoraFireProjectile projectile)
    : RoomEntityAdapter<ZoraFireProjectile>(projectile, projectile.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, ISwordHittableRoomEntity,
        IItemCollisionHittableRoomEntity
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.Strike();
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        collision == RoomEntityItemCollision.SwordBeam &&
        hitbox.Intersects(Entity.CollisionBounds) && Entity.Strike();
}
