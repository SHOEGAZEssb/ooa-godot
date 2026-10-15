using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlueStalfosRoomEntity : CombatEnemyRoomEntityAdapter<BlueStalfosCharacter>,
    IFixedRoomEntity, INativeEnemySlotRoomEntity, IScreenTransitionPreloadRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity, IPlayerForcedMovement,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    ILinkSwordStateAwareRoomEntity, ISwordAttackerKnockbackRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IExpertPunchHittableRoomEntity, ISeedCollisionTarget, ISomariaBlockCollisionRoomEntity,
    IPostObjectLinkContactRoomEntity
{
    private readonly BlueStalfosEnvironment _world;
    private readonly bool _counted;
    private readonly BlueStalfosProfile _data = BlueStalfosProfile.Shared;
    private int _sword = ItemCollisionType.L1Sword, _attackerRecoil;
    public bool MeleeReportsContact { get; private set; }
    public override int DimitriCollisionType => EnemyId.BlueStalfos;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    public override bool CountsAsEnemy => _counted && !Entity.IsDead && !Entity.CountTransferred;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    internal BlueStalfosRoomEntity(BlueStalfosCharacter actor, BlueStalfosEnvironment world, bool counted, int killableIndex)
        : base(actor, actor.SetTransitionDrawOffset, EnemyCombatDescriptor.Special(
            EnemyCombatComponent.WithContactDamage(() => actor.IsDead, () => actor.CollisionBounds,
                (_, _) => false, _ => false, actor.OverlapsLink, () => actor.Position, actor.Record.DamageQuarters, () => null),
            counted, killableIndex, completedOutcome: () => actor.BossExploded
                ? RoomEnemyOutcome.BossTeardown(killableIndex) : RoomEnemyOutcome.SilentDeletion(false), soundRequested: world.Sound),
            collisionZ: () => actor.ZFixed >> 8) { _world = world; _counted = counted; }
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.Slot = slot;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget ?? frame.Player.EnemyContactPosition, frame.Counter);
    public void UpdatePlayerForcedMovement(Player player) { if (Entity.Record.SubId is 0 or 1 && !Finished) _world.Entry.Update(player); }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.InitializeNative(); return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (Entity.State == 0) Entity.InitializeNative(); }
    public void SetLinkSwordState(SwordActionState state, int level, int? itemCollisionType = null) => _sword = itemCollisionType ?? SwordCollision.Type(state, level);
    private bool Eligible(int item, Rect2 bounds) => Entity.CollisionEnabled && !Entity.NativeHitPending && Entity.InvincibilityCounter == 0 &&
        _data.Active(item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private bool Hit(int item, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        _attackerRecoil = 0; MeleeReportsContact = false;
        if (!Eligible(item, bounds)) return false;
        int effect = _data.Effect(Entity.CollisionMode, item); MeleeReportsContact = effect != 0;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect21:
                Entity.PublishDamage(damage, 32); _world.Sound(SoundId.SndBossDamage); return true;
            case CollisionEffect.Effect15:
            case CollisionEffect.Effect16:
                var recoil = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
                _attackerRecoil = effect == CollisionEffect.Effect15 ? recoil.LowFrames : recoil.NormalFrames;
                Entity.PublishDamage(0, -28); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, origin))); return true;
            case CollisionEffect.Effect1b:
                Entity.PublishDamage(0, -20); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, origin))); return true;
            case CollisionEffect.Effect1c:
            case CollisionEffect.Effect20: Entity.DeferNativeHitStatus(); return true;
            default: throw new NotSupportedException($"blueStalfos.s mode${Entity.CollisionMode:x2}: collision${item:x2}, effect${effect:x2}.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision item, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit((int)item, bounds, origin, damage, spawns);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.ExpertPunch, bounds, origin, damage, spawns);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.BiggoronSword, bounds, origin, damage, spawns);
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength, out SwordAttackerKnockback response)
    { response = new(Entity.Position, _attackerRecoil); bool pending = _attackerRecoil != 0; _attackerRecoil = 0; return pending; }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) ||
            !Hit(ItemCollisionType.L1Boomerang, item.CollisionBounds, item.Position, 0, spawns)) return default;
        return new(true, _data.Effect(Entity.CollisionMode, ItemCollisionType.L1Boomerang) != 0);
    }
    public override BombchuTarget BombchuTarget => new(EnemyId.BlueStalfos, Entity.Visible, Entity.Position, Entity.CollisionBounds, Entity.Health);
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) ||
            !Eligible(ItemCollisionType.SwitchHook, hook.CollisionBounds)) return false;
        int effect = _data.Effect(Entity.CollisionMode, ItemCollisionType.SwitchHook);
        if (effect == CollisionEffect.Effect1b) { Entity.PublishDamage(0, -20); hook.NotifyObjectCollision(CollisionMidpoint(Entity.Position, hook.Position)); return true; }
        if (effect == CollisionEffect.Effect21) { Entity.PublishDamage(hook.HitDamage, 32); _world.Sound(SoundId.SndBossDamage); hook.NotifyObjectCollision(); return true; }
        if (effect == CollisionEffect.Effect1c) { Entity.DeferNativeHitStatus(); hook.NotifyObjectCollision(); return true; }
        if (effect == 0) return true;
        throw new NotSupportedException($"blueStalfos.s: hook effect${effect:x2}.");
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7) || !Eligible(ItemCollisionType.SomariaBlock, block.CollisionBounds)) return false;
        int effect = _data.Effect(Entity.CollisionMode, ItemCollisionType.SomariaBlock);
        if (effect is CollisionEffect.Effect2d or CollisionEffect.Effect2f) block.Flags |= 0x20;
        if (effect == CollisionEffect.Effect2f)
        {
            Entity.PublishDamage(-block.Damage, 21);
            Entity.ApplySwordKnockback(block.Position, EnemyKnockbackStrength.Normal);
            _world.Sound(SoundId.SndDamageEnemy);
        }
        else if (effect != CollisionEffect.Effect2d && effect != 0) throw new NotSupportedException($"blueStalfos.s: Somaria effect${effect:x2}.");
        return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int item, ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("Blue Stalfos requires the live seed collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(item, bounds, origin, (256 - seed.Damage) & 255, spawns)) return default;
        int effect = _data.Effect(Entity.CollisionMode, item);
        return effect == 0 ? new(true, SeedHitResult.None, false) :
            new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && player.CanAcceptShieldCollision && _data.Active(shield) &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int effect = _data.Effect(Entity.CollisionMode, shield);
            var (inv, recoil) = effect switch {
                CollisionEffect.Effect05 => (8, 11), CollisionEffect.Effect06 => (15, 19), CollisionEffect.Effect07 => (22, 25),
                _ => throw new NotSupportedException($"blueStalfos.s: shield effect${effect:x2}.") };
            player.ApplyShieldCollisionRecoil(Entity.Position, inv, recoil); Entity.DeferNativeHitStatus(); _world.Sound(SoundId.SndBombLand); return;
        }
        if (!player.NativeObjectVulnerable || !player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ)) return;
        if (_data.Effect(Entity.CollisionMode, 0) != CollisionEffect.DamageLink) throw new NotSupportedException("Blue Stalfos requires Link effect$02.");
        if (player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters, RingDamageSource.Generic, 34, 15)) Entity.DeferNativeHitStatus();
    }
}
