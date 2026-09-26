using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MoldormRoomEntity
    : CombatEnemyRoomEntityAdapter<MoldormCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, IItemCollisionHittableRoomEntity,
        IExpertPunchHittableRoomEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, INativeEnemySlotRoomEntity, IPostObjectMeleeCollisionRoomEntity
{
    public bool MeleeReportsContact => true;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.BindEnemySlot(slot, resolve);
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    private readonly IReadOnlyList<EnemyBehaviorValue> _collisionEffects =
        EnemyBehaviorTables.Shared.MoldormCollisionEffects;

    internal MoldormRoomEntity(
        MoldormCharacter moldorm,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            moldorm,
            moldorm.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                moldorm,
                moldorm.Record.DamageQuarters,
                moldorm.TakeSwordHit,
                moldorm.TakeBurnHit,
                moldorm.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();

    public bool ApplyExpertPunch(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        RequireCollisionEffect(ItemCollisionType.ExpertPunch, CollisionEffect.SwordHighKnockback);
        return base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            EnemyKnockbackStrength.High,
            spawns);
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        (int collisionType, int effect, EnemyKnockbackStrength strength) =
            collision switch
            {
                RoomEntityItemCollision.ExpertPunch =>
                    (0x0b, 0x0a, EnemyKnockbackStrength.High),
                RoomEntityItemCollision.ThrownObject =>
                    (0x16, 0x09, EnemyKnockbackStrength.Normal),
                RoomEntityItemCollision.Bomb =>
                    (0x18, 0x0a, EnemyKnockbackStrength.High),
                RoomEntityItemCollision.SwordBeam =>
                    (0x19, 0x08, EnemyKnockbackStrength.Low),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(collision), collision,
                    "Moldorm received an unknown direct item collision.")
            };
        RequireCollisionEffect(collisionType, effect);
        return base.ApplySwordHit(
            hitbox, sourcePosition, damage, strength, spawns);
    }

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !CombatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }

        switch (seedItem)
        {
            case ItemId.EmberSeed: // ITEM_EMBER_SEED -> COLLISIONEFFECT_20.
                RequireCollisionEffect(ItemCollisionType.EmberSeed, CollisionEffect.Effect20);
                return SeedHitResult.Activate;
            case ItemId.ScentSeed: // ITEM_SCENT_SEED -> COLLISIONEFFECT_08.
                RequireCollisionEffect(ItemCollisionType.ScentSeed, CollisionEffect.SwordLowKnockback);
                return base.ApplySwordHit(
                    hitbox,
                    sourcePosition,
                    damage: 2,
                    EnemyKnockbackStrength.Low,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case ItemId.MysterySeed: // ITEM_MYSTERY_SEED -> COLLISIONEFFECT_20.
                RequireCollisionEffect(ItemCollisionType.MysterySeed, CollisionEffect.Effect20);
                return SeedHitResult.Activate;
            default:
                return SeedHitResult.None;
        }
    }

    private void RequireCollisionEffect(int collisionType, int expectedEffect)
    {
        EnemyBehaviorValue effect = _collisionEffects[collisionType];
        if (effect.Value != expectedEffect)
        {
            throw new InvalidOperationException(
                $"{effect.Source} maps ENEMYCOLLISION_MOLDORM $3a item " +
                $"collision ${collisionType:x2} to effect ${effect.Value:x2}; " +
                $"runtime support requires ${expectedEffect:x2}.");
        }
    }
}
