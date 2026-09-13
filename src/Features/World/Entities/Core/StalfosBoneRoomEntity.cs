using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class StalfosBoneRoomEntity(StalfosBoneProjectile bone)
    : RoomEntityAdapter<StalfosBoneProjectile>(bone, bone.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, ISwordHittableRoomEntity, ILinkContactEntity,
        IObjectCollisionHeightRoomEntity, INativePartHealthRoomEntity
{
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public bool Finished => Entity.Finished;
    public int CollisionZ => Entity.ZFixed >> 8;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, frame.Counter);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength knockbackStrength, ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
}
