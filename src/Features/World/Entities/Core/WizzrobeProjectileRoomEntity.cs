using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WizzrobeProjectileRoomEntity(WizzrobeProjectile projectile, Func<Vector2> camera, Action<int> sound)
    : RoomEntityAdapter<WizzrobeProjectile>(projectile, projectile.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IPostObjectLinkContactRoomEntity, IObjectCollisionHeightRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, INativePartHealthRoomEntity
{
    private readonly WizzrobeBehaviorProfile _data = EnemyBehaviorTables.Shared.Wizzrobe;
    public bool Finished => Entity.Finished;
    public int CollisionZ => Entity.ZHigh;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(camera());
    public void HandleLinkContact(Player player)
    {
        if (Entity.Finished || !Entity.CollisionEnabled || Entity.PendingCollision || Entity.InvincibilityCounter != 0 ||
            !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && _data.PartActiveCollisions[shield].Value != 0 &&
            player.CanAcceptShieldCollision && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (_data.PartEffects[shield].Value != CollisionEffect.Effect1f)
                throw new NotSupportedException($"PART$1f shield collision${shield:x2} requires effect$1f.");
            Entity.InvincibilityCounter = -28; Entity.PublishCollision(); sound(SoundId.SndClink2);
            return;
        }
        if (_data.PartActiveCollisions[0].Value != 0 && player.NativeObjectVulnerable &&
            player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, (0x100 - _data.PartData[3].Value) / 2,
                RingDamageSource.Generic, 34, 15)) Entity.PublishCollision();
    }
}
