using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class MoblinBoomerangRoomEntity(MoblinBoomerangProjectile boomerang)
    : RoomEntityAdapter<MoblinBoomerangProjectile>(
        boomerang, boomerang.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool Finished => Entity.Finished;
    public bool UpdatesDuringDialogue => Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => Entity.InitializationPending;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, frame.Counter);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        hitbox.Intersects(Entity.CollisionBounds) && Entity.Deflect();
}
