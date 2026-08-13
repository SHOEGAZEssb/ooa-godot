using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArmMimicRoomEntity
    : CombatEnemyRoomEntityAdapter<ArmMimicCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, IItemCollisionHittableRoomEntity,
        IExpertPunchHittableRoomEntity
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _collisionEffects =
        EnemyBehaviorTables.Shared.ArmMimicCollisionEffects;

    internal ArmMimicRoomEntity(
        ArmMimicCharacter mimic,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            mimic,
            mimic.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                mimic,
                mimic.Record.DamageQuarters,
                mimic.TakeSwordHit,
                mimic.TakeBurnHit,
                mimic.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(
            frame.Player.LinkMovementAngle,
            frame.Player.FacingVector);

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
        return ApplyDamage(
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
        (int effect, EnemyKnockbackStrength strength) = collision switch
        {
            RoomEntityItemCollision.ExpertPunch =>
                (0x0a, EnemyKnockbackStrength.High),
            RoomEntityItemCollision.ThrownObject =>
                (0x09, EnemyKnockbackStrength.Normal),
            RoomEntityItemCollision.Bomb =>
                (0x0a, EnemyKnockbackStrength.High),
            RoomEntityItemCollision.SwordBeam =>
                (0x08, EnemyKnockbackStrength.Low),
            _ => throw new ArgumentOutOfRangeException(
                nameof(collision), collision,
                "Arm Mimic received an unknown direct item collision.")
        };
        RequireCollisionEffect((int)collision, effect);
        return ApplyDamage(
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
            case 0x20: // Ember Seed -> collisionEffect27 / burn.
                RequireCollisionEffect(0x1b, 0x27);
                return base.ApplySeedHit(
                    hitbox, sourcePosition, seedItem, spawns);
            case 0x21: // Scent Seed -> collisionEffect08 / low recoil.
                RequireCollisionEffect(0x1c, 0x08);
                return ApplyDamage(
                    hitbox,
                    sourcePosition,
                    damage: 2,
                    EnemyKnockbackStrength.Low,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case 0x24: // Mystery Seed -> collisionEffect35 / forced death.
                RequireCollisionEffect(0x1a, 0x35);
                return ApplyDamage(
                    hitbox,
                    sourcePosition,
                    damage: 0x7f,
                    EnemyKnockbackStrength.None,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            default:
                return SeedHitResult.None;
        }
    }

    private bool ApplyDamage(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) =>
        CombatDescriptor.Combat.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            strength,
            spawns,
            deathPuffDecrementsRoomCount:
                CombatDescriptor.CountsAsEnemy);

    private void RequireCollisionEffect(int collisionType, int expectedEffect)
    {
        EnemyBehaviorValue effect = _collisionEffects[collisionType];
        if (effect.Value != expectedEffect)
        {
            throw new InvalidOperationException(
                $"{effect.Source} maps ENEMYCOLLISION_ARM_MIMIC $39 item " +
                $"collision ${collisionType:x2} to effect ${effect.Value:x2}; " +
                $"runtime support requires ${expectedEffect:x2}.");
        }
    }
}
