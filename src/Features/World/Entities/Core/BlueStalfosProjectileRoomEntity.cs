using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlueStalfosProjectileRoomEntity(BlueStalfosProjectile part)
    : RoomEntityAdapter<BlueStalfosProjectile>(part, part.SetTransitionDrawOffset),
    IFixedRoomEntity, IRoomEntityLifetime, INativePartHealthRoomEntity,
    IPostObjectLinkContactRoomEntity, ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity, IItemCollisionHittableRoomEntity, IObjectCollisionHeightRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly BlueStalfosProfile _data = BlueStalfosProfile.Shared;
    private readonly BlueStalfosProjectileDatabase _parts = BlueStalfosProjectileDatabase.Shared;
    private int _sword = ItemCollisionType.L1Sword;
    public bool Finished => Entity.Finished;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public int CollisionZ => Entity.ZFixed >> 8;
    public bool MeleeReportsContact { get; private set; }
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget ?? frame.Player.EnemyContactPosition, frame.Counter);
    public void SetLinkSwordState(SwordActionState state, int level, int? itemCollisionType = null) => _sword = itemCollisionType ?? SwordCollision.Type(state, level);
    private bool Hit(int item, Rect2 bounds)
    {
        MeleeReportsContact = false;
        if (!Entity.CollisionEnabled || !_parts.Active(item) || !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        int effect = _data.Effect(Entity.CollisionMode, item);
        if (effect == 0) return true;
        if (effect != CollisionEffect.Effect1c) throw new NotSupportedException($"PART$3d mode${Entity.CollisionMode:x2}: item${item:x2}, effect${effect:x2}.");
        Entity.PublishCollision(item); MeleeReportsContact = true; return true;
    }
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision item, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit((int)item, bounds);
    public void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && player.CanAcceptShieldCollision && _parts.Active(shield) &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int shieldEffect = _data.Effect(Entity.CollisionMode, shield);
            if (shieldEffect == CollisionEffect.Effect1e)
            { player.ApplyShieldCollisionRecoil(Entity.Position, 0, 16); Entity.PublishShieldCollision(); return; }
            if (shieldEffect != 0) throw new NotSupportedException($"PART$3d shield effect${shieldEffect:x2}.");
        }
        if (!player.NativeObjectVulnerable || !player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ)) return;
        int effect = _data.Effect(Entity.CollisionMode, ItemCollisionType.Link);
        if (effect == CollisionEffect.DamageLink)
        {
            if (player.ApplyEnemyContactDamage(Entity.Position, Entity.DamageQuarters, RingDamageSource.Generic, 34, 15)) Entity.PublishCollision(ItemCollisionType.Link);
            return;
        }
        // collisionEffect3b queues special object$02 without applying ordinary
        // contact damage, then publishes JUST_HIT to the baby-ball part.
        if (effect != 0x3b) throw new NotSupportedException($"PART$3d Link effect${effect:x2}.");
        player.QueueBabyTransformation(); Entity.PublishCollision(ItemCollisionType.Link);
    }
}
