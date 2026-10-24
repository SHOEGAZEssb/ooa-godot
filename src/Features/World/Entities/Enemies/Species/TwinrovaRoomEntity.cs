using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TwinrovaRoomEntity : CombatEnemyRoomEntityAdapter<TwinrovaCharacter>, IFixedRoomEntity,
    INativeEnemySlotRoomEntity, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget, IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity
{
    internal TwinrovaCharacter Actor => Entity;
    internal int Slot { get; private set; }
    private int _sword = 4;
    public bool MeleeReportsContact { get; private set; }
    public override int DimitriCollisionType => 3;
    public override int DimitriCollisionMode => 0x0a;
    internal TwinrovaRoomEntity(TwinrovaCharacter actor, bool counted = true) : base(actor, actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(() => actor.IsDead, () => actor.CollisionBounds,
            (_, _) => false, _ => false, actor.OverlapsLink, () => actor.Position, actor.Data.Actor(3).DamageQuarters, () => null), counted, 0,
            () => RoomEnemyOutcome.SilentDeletion(false), actor.World.Entities.OnSoundRequested), collisionZ: () => actor.ZFixed >> 8) { }
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) { Slot = slot; Actor.Slot = slot; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Actor.UpdateFrame(frame);
    public void SetLinkSwordState(SwordActionState state, int level, int? collision = null) => _sword = collision ?? SwordCollision.Type(state, level);
    private bool Hit(int item, Rect2 bounds)
    {
        MeleeReportsContact = false;
        if (!Actor.CollisionEnabled || Actor.NativeHitPending || Actor.InvincibilityCounter != 0 || !Actor.Data.Active(false, 3, item) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Actor.CollisionBounds, bounds)) return false;
        int effect = Actor.Data.Effect(0x0a, item); MeleeReportsContact = effect != 0;
        if (effect == 0) return true;
        if (effect is not (0x1c or 0x20)) throw new NotSupportedException($"twinrova.s: ENEMY $03 mode$0a item${item:x2} effect${effect:x2}.");
        Actor.DeferNativeHitStatus(); return true;
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision item, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)item, bounds);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(11, bounds);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(7, bounds);
    public override bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns) => bomb.CollisionEnabled &&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, bomb.CollisionZ, bomb.CollisionZRadius) && Hit(0x18, bomb.CollisionBounds);
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 link)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) || !Hit(13, hook.CollisionBounds)) return false;
        hook.NotifyObjectCollision(); return true;
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) && Hit(0x17, item.CollisionBounds) ? new(true, MeleeReportsContact) : default;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !Actor.CollisionEnabled || Actor.NativeHitPending || Actor.InvincibilityCounter != 0 ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7) || !RoomEntityManager.ObjectCollisionXYOverlaps(Actor.CollisionBounds, block.CollisionBounds)) return false;
        if (Actor.Data.Effect(0x0a, 0x15) != 0x2d) throw new NotSupportedException("twinrova.s: Somaria collision requires effect$2d.");
        block.Flags |= 0x20; return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int item, ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("ENEMY $03 requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(item, bounds)) return default;
        int effect = Actor.Data.Effect(0x0a, item);
        return effect == 0 ? new(true, SeedHitResult.None, false) : new(true,
            seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect == 0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Actor.CollisionEnabled || Actor.NativeHitPending || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.CanAcceptShieldCollision && Actor.Data.Active(false, 3, player.Inventory.ShieldLevel) &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Actor.CollisionBounds, player.ShieldCollisionBounds))
        {
            int effect = Actor.Data.Effect(0x0a, player.Inventory.ShieldLevel);
            if (effect == 0) return;
            if (effect != 6) throw new NotSupportedException($"twinrova.s: shield effect${effect:x2}.");
            player.ApplyShieldCollisionRecoil(Actor.Position, 15, 19); Actor.DeferNativeHitStatus();
            Actor.World.Entities.OnSoundRequested(SoundId.SndBombLand); return;
        }
        if (player.OverlapsEnemyCollision(Actor.CollisionBounds, CollisionZ) && player.ApplyEnemyContactDamage(Actor.Position, Actor.Data.Actor(3).DamageQuarters,
            RingDamageSource.Generic, 34, 15)) Actor.DeferNativeHitStatus();
    }
}
