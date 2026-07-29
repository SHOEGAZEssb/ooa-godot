using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class CombatEnemyRoomEntityAdapter<T>(
    T entity,
    Action<Vector2> setTransitionDrawOffset,
    EnemyCombatDescriptor combatDescriptor,
    Action? finished = null,
    Func<int>? collisionZ = null)
    : RoomEntityAdapter<T>(entity, setTransitionDrawOffset),
        ILinkContactEntity, ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        ISeedBurnTarget, IRoomEntityLifetime,
        IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource,
        IObjectCollisionHeightRoomEntity
    where T : EnemyCharacter
{
    private bool _seedBurning;
    private bool _completedOutcomeTaken;

    public bool Finished => combatDescriptor.Combat.Finished;
    public bool CountsAsEnemy =>
        combatDescriptor.CountsAsEnemy &&
        !combatDescriptor.Combat.Finished;
    public bool IsSeedBurning => _seedBurning;
    public virtual bool FreezesDuringSeedBurn => true;
    public Vector2 SeedBurnPosition => Entity.Position;
    protected EnemyCombatDescriptor CombatDescriptor => combatDescriptor;
    protected bool SeedBurning => _seedBurning;
    protected int KillableEnemyIndex =>
        combatDescriptor.KillableEnemyIndex;
    public int CollisionZ => collisionZ?.Invoke() ?? 0;
    public virtual void HandleLinkContact(Player player)
    {
        if (!_seedBurning || !FreezesDuringSeedBurn)
            combatDescriptor.Combat.HandleLinkContact(player);
    }
    public virtual bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        (!_seedBurning || !FreezesDuringSeedBurn) &&
        combatDescriptor.Combat.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns,
            deathPuffDecrementsRoomCount:
                combatDescriptor.CountsAsEnemy);
    public virtual SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns) =>
        ApplySeedHit(hitbox);

    private SeedHitResult ApplySeedHit(Rect2 hitbox)
    {
        if (_seedBurning || !Entity.CollisionEnabled ||
            !combatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        _seedBurning = true;
        return SeedHitResult.Ignite;
    }
    public virtual void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_seedBurning)
            return;
        _seedBurning = false;
        combatDescriptor.Combat.ApplyBurnHit(
            2,
            spawns,
            deathPuffDecrementsRoomCount:
                combatDescriptor.CountsAsEnemy);
    }

    protected void CancelSeedBurn() =>
        _seedBurning = false;

    public virtual bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!combatDescriptor.Combat.Finished || _completedOutcomeTaken)
        {
            outcome = default;
            return false;
        }

        _completedOutcomeTaken = true;
        outcome = combatDescriptor.CompletedOutcome(Entity);
        return true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.TakeCompletedKnockbackDeath() &&
            combatDescriptor.Combat.CreateDeathPuff() is { } deathPuff)
        {
            spawns.Add(deathPuff with
            {
                DecrementsRoomCount =
                    combatDescriptor.CountsAsEnemy
            });
        }
        if (Entity.TakeHazardEffect() is { } hazardEffect)
        {
            spawns.Add(hazardEffect.Hazard switch
            {
                HazardType.Water or HazardType.Lava =>
                    new EnemySplashSpawn(
                        hazardEffect.Position, hazardEffect.Hazard),
                HazardType.Hole =>
                    new FallingDownHoleSpawn(hazardEffect.Position),
                _ => throw new InvalidOperationException(
                    "A completed enemy hazard must be water, hole, or lava.")
            });
        }
        finished?.Invoke();
    }
}

internal enum SeedHitResult
{
    None,
    Ignite,
    Consume
}

internal abstract record RoomEntitySpawn(bool UpdateThisFrame = false);
