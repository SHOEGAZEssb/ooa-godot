using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class ZoraFireRoomEntity(ZoraFireProjectile projectile)
    : RoomEntityAdapter<ZoraFireProjectile>(projectile, projectile.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, ISwordHittableRoomEntity,
        IItemCollisionHittableRoomEntity, INativePartHealthRoomEntity,
        ILinkContactEntity, IPostObjectLinkContactRoomEntity
{
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public bool Finished => Entity.Finished;
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        false; // PART $19/$31 active masks exclude all sword collision types.
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        false;
}
