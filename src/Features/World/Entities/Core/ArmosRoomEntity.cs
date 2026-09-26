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

    public override int DimitriCollisionMode => Entity.ActiveCollisionMode;
    public override int DimitriCollisionType => 0x1d;

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
                        ? new EnemyDeathPuffSpawn(armos.Position, EnemyId: EnemyId.Armos)
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
        int collisionType = SwordCollision.Type(_swordState, _swordLevel);
        int expectedEffect = _swordState == SwordActionState.Spin
            ? CollisionEffect.Effect16
            : CollisionEffect.Effect15;
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
                (int)collision, CollisionEffect.SwordNoKnockback, hitbox, sourcePosition, damage, spawns),
            RoomEntityItemCollision.ThrownObject => ApplyHarmless(
                collision, CollisionEffect.Effect1c, hitbox),
            RoomEntityItemCollision.SwordBeam => ApplyHarmless(
                collision, CollisionEffect.Effect20, hitbox),
            RoomEntityItemCollision.ExpertPunch => ApplyHarmless(
                collision, CollisionEffect.None, hitbox),
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
            case ItemId.EmberSeed:
                RequireCollisionEffect(ItemCollisionType.EmberSeed, CollisionEffect.Effect20);
                return Entity.InvincibilityCounter == 0
                    ? SeedHitResult.Activate
                    : SeedHitResult.None;
            case ItemId.ScentSeed:
                return ApplyDamage(
                    ItemCollisionType.ScentSeed,
                    CollisionEffect.SwordNoKnockback,
                    hitbox,
                    sourcePosition,
                    damage: 2,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case ItemId.MysterySeed:
                return ApplyDamage(
                    ItemCollisionType.MysterySeed,
                    CollisionEffect.Effect35,
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
