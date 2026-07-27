using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class HostileProjectileRoomEntity<TProjectile>(
    TProjectile projectile)
    : RoomEntityAdapter<TProjectile>(
        projectile,
        projectile.SetTransitionDrawOffset),
        IFixedRoomEntity,
        ISwordHittableRoomEntity,
        IRoomEntityLifetime
    where TProjectile : TransitionOffsetNode2D, IHostileProjectile
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) &&
        Entity.DeflectWithSword();
}
