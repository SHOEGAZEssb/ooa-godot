using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArmosRoomEntity
    : CombatEnemyRoomEntityAdapter<ArmosCharacter>, IFixedRoomEntity,
        IItemCollisionHittableRoomEntity, ILinkSwordStateAwareRoomEntity,
        ISwordAttackerKnockbackRoomEntity
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _collisionEffects =
        EnemyBehaviorTables.Shared.ArmosCollisionEffects;
    private readonly ArmoredSwordAttackerKnockbackProfile _attackerKnockback =
        EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
    private SwordActionState _swordState;
    private int _swordLevel;

    internal ArmosRoomEntity(ArmosCharacter armos)
        : base(
            armos,
            armos.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => armos.IsDead,
                    () => armos.CollisionBounds,
                    armos.TakeDamageWithoutKnockback,
                    _ => false,
                    armos.OverlapsLink,
                    () => armos.Position,
                    armos.Record.DamageQuarters,
                    () => armos.IsDead && !armos.DiedInHazard
                        ? new EnemyDeathPuffSpawn(armos.Position, EnemyId: 0x1d)
                        : null),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: RoomEnemyOutcome.EnemyDieUncounted))
    {
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();

    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    {
        _swordState = state;
        _swordLevel = swordLevel;
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        int collisionType = _swordState switch
        {
            SwordActionState.Spin => 0x08,
            SwordActionState.Held or SwordActionState.Charged => 0x09,
            _ => Math.Clamp(0x03 + _swordLevel, 0x04, 0x06)
        };
        int expectedEffect = _swordState == SwordActionState.Spin
            ? 0x16
            : 0x15;
        RequireCollisionEffect(collisionType, expectedEffect);
        if (!Entity.CollisionEnabled ||
            !hitbox.Intersects(Entity.CollisionBounds) ||
            !Entity.TakeArmoredHit())
        {
            return false;
        }
        spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(
            Entity.Position, sourcePosition)));
        return true;
    }

    public bool TryGetSwordAttackerKnockback(
        EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response)
    {
        int frames = strength switch
        {
            EnemyKnockbackStrength.Low => _attackerKnockback.LowFrames,
            EnemyKnockbackStrength.Normal => _attackerKnockback.NormalFrames,
            EnemyKnockbackStrength.High => _attackerKnockback.HighFrames,
            _ => 0
        };
        response = new SwordAttackerKnockback(Entity.Position, frames);
        return frames != 0;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        return collision switch
        {
            RoomEntityItemCollision.Bomb => ApplyDamage(
                (int)collision, 0x0b, hitbox, sourcePosition, damage, spawns),
            RoomEntityItemCollision.ThrownObject => ApplyHarmless(
                collision, 0x1c, hitbox),
            RoomEntityItemCollision.SwordBeam => ApplyHarmless(
                collision, 0x20, hitbox),
            RoomEntityItemCollision.ExpertPunch => ApplyHarmless(
                collision, 0x00, hitbox),
            _ => false
        };
    }

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled ||
            !hitbox.Intersects(Entity.CollisionBounds))
        {
            return SeedHitResult.None;
        }
        switch (seedItem)
        {
            case 0x20:
                RequireCollisionEffect(0x1b, 0x20);
                return Entity.InvincibilityCounter == 0
                    ? SeedHitResult.Activate
                    : SeedHitResult.None;
            case 0x21:
                return ApplyDamage(
                    0x1c,
                    0x0b,
                    hitbox,
                    sourcePosition,
                    damage: 2,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case 0x24:
                return ApplyDamage(
                    0x1a,
                    0x35,
                    hitbox,
                    sourcePosition,
                    damage: 0x7f,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            default:
                return SeedHitResult.None;
        }
    }

    private bool ApplyDamage(
        int collisionType,
        int expectedEffect,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        RequireCollisionEffect(collisionType, expectedEffect);
        return CombatDescriptor.Combat.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            EnemyKnockbackStrength.None,
            spawns,
            deathPuffDecrementsRoomCount: true);
    }

    private bool ApplyHarmless(
        RoomEntityItemCollision collision,
        int expectedEffect,
        Rect2 hitbox)
    {
        RequireCollisionEffect((int)collision, expectedEffect);
        return Entity.CollisionEnabled &&
            Entity.InvincibilityCounter == 0 &&
            hitbox.Intersects(Entity.CollisionBounds) &&
            collision == RoomEntityItemCollision.SwordBeam;
    }

    private void RequireCollisionEffect(int collisionType, int expectedEffect)
    {
        EnemyBehaviorValue effect = _collisionEffects[collisionType];
        if (effect.Value != expectedEffect)
        {
            throw new InvalidOperationException(
                $"{effect.Source} maps ENEMYCOLLISION_ACTIVE_RED_ARMOS $1e " +
                $"item collision ${collisionType:x2} to effect " +
                $"${effect.Value:x2}; runtime support requires " +
                $"${expectedEffect:x2}.");
        }
    }
}
