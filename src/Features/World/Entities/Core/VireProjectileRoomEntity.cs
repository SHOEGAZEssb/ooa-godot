using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VireProjectileRoomEntity(VireProjectile projectile, Action<int> sound)
    : RoomEntityAdapter<VireProjectile>(projectile, projectile.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, INativePartHealthRoomEntity, IObjectCollisionHeightRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectLinkContactRoomEntity, IPostObjectItemCollisionRoomEntity, IBombCollisionRoomEntity,
        IBoomerangCollisionRoomEntity, ISeedCollisionTarget
{
    private readonly VireProjectileProfile _data = EnemyBehaviorTables.Shared.VireProjectile;
    private int _swordCollision = ItemCollisionType.L1Sword;
    public bool Finished => Entity.Finished;
    public int CollisionZ => Entity.ZHigh;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool MeleeReportsContact => true;
    public bool HasBombCollisionIdentity => true;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget ?? frame.Player.Position);
    public void SetLinkSwordState(SwordActionState state, int level, int? itemCollisionType = null) =>
        _swordCollision = itemCollisionType ?? SwordCollision.Type(state, level);
    private bool Eligible(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && _data.ActiveCollisions[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private bool Hit(int collision, Rect2 bounds)
    {
        if (!Eligible(collision, bounds)) return false;
        int effect = _data.Effects[collision].Value;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect1c: case CollisionEffect.Effect20:
                Entity.PublishCollision(collision); return true;
            case CollisionEffect.Effect1f:
                Entity.InvincibilityCounter = -28; Entity.PublishCollision(collision);
                sound(SoundId.SndClink2); return true;
            default: throw new NotSupportedException($"vireProjectile.s: PART$3a mode$76 collision${collision:x2}, effect${effect:x2}.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(_swordCollision, bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, bounds);
    public bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns) =>
        bomb.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, bomb.CollisionZ, bomb.CollisionZRadius) &&
        Hit(ItemCollisionType.Bomb, bomb.CollisionBounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) &&
        Hit(ItemCollisionType.L1Boomerang, item.CollisionBounds)
            ? new(true, _data.Effects[ItemCollisionType.L1Boomerang].Value != CollisionEffect.None) : default;
    public SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("PART$3a requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(collision, bounds)) return default;
        int effect = _data.Effects[collision].Value;
        return effect == CollisionEffect.None ? new(true, SeedHitResult.None, false) :
            new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
                effect == CollisionEffect.Effect20);
    }
    public void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.PendingCollision || Entity.InvincibilityCounter != 0 ||
            !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && player.CanAcceptShieldCollision &&
            Hit(shield, player.ShieldCollisionBounds)) return;
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, (0x100 - _data.PartData[3].Value) / 2,
                RingDamageSource.Generic, 34, 15)) Entity.PublishCollision(ItemCollisionType.Link);
    }
}
