using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MoblinBoomerangRoomEntity(MoblinBoomerangProjectile boomerang)
    : RoomEntityAdapter<MoblinBoomerangProjectile>(
        boomerang, boomerang.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, frame.Counter);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.Deflect();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
