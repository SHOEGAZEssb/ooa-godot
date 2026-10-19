using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal class HostileProjectileRoomEntity<TProjectile>(
    TProjectile projectile)
    : RoomEntityAdapter<TProjectile>(
        projectile,
        projectile.SetTransitionDrawOffset),
        IFixedRoomEntity,
        ISwordHittableRoomEntity,
        IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
    where TProjectile : TransitionOffsetNode2D, IHostileProjectile
{
    public bool Finished => Entity.Finished;
    // updateParts admits original state0 independently of text/mask/scroll.
    public bool UpdatesDuringDialogue => Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => Entity.InitializationPending;

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
