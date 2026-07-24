using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OctorokRockRoomEntity(OctorokRockProjectile rock)
    : RoomEntityAdapter<OctorokRockProjectile>(rock, rock.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.DeflectWithSword();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
