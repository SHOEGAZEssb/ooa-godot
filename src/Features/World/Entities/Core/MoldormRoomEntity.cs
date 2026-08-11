using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MoldormRoomEntity
    : CombatEnemyRoomEntityAdapter<MoldormCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, IItemCollisionHittableRoomEntity,
        IExpertPunchHittableRoomEntity
{
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
        RequireCollisionEffect(0x0b, 0x0a);
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
            case 0x20: // ITEM_EMBER_SEED -> COLLISIONEFFECT_20.
                RequireCollisionEffect(0x1b, 0x20);
                return SeedHitResult.Activate;
            case 0x21: // ITEM_SCENT_SEED -> COLLISIONEFFECT_08.
                RequireCollisionEffect(0x1c, 0x08);
                return base.ApplySwordHit(
                    hitbox,
                    sourcePosition,
                    damage: 2,
                    EnemyKnockbackStrength.Low,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case 0x24: // ITEM_MYSTERY_SEED -> COLLISIONEFFECT_20.
                RequireCollisionEffect(0x1a, 0x20);
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
