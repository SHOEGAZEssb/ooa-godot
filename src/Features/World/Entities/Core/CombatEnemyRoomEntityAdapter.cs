using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class CombatEnemyRoomEntityAdapter<T>(
    T entity,
    Action<Vector2> setTransitionDrawOffset,
    EnemyCombatComponent combat,
    bool countsAsEnemy,
    int killableEnemyIndex,
    Func<RoomEnemyOutcome>? completedOutcome = null,
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

    public bool Finished => combat.Finished;
    public bool CountsAsEnemy => countsAsEnemy && !combat.Finished;
    public bool IsSeedBurning => _seedBurning;
    public Vector2 SeedBurnPosition => Entity.Position;
    protected int KillableEnemyIndex => killableEnemyIndex;
    public int CollisionZ => collisionZ?.Invoke() ?? 0;
    public void HandleLinkContact(Player player)
    {
        if (!_seedBurning)
            combat.HandleLinkContact(player);
    }
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        !_seedBurning && combat.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns,
            deathPuffDecrementsRoomCount: countsAsEnemy);
    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns) =>
        ApplySeedHit(hitbox);

    private SeedHitResult ApplySeedHit(Rect2 hitbox)
    {
        if (_seedBurning || !Entity.CollisionEnabled ||
            !combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        _seedBurning = true;
        return SeedHitResult.Ignite;
    }
    public void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_seedBurning)
            return;
        _seedBurning = false;
        combat.ApplyBurnHit(
            2,
            spawns,
            deathPuffDecrementsRoomCount: countsAsEnemy);
    }

    public virtual bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!combat.Finished || _completedOutcomeTaken)
        {
            outcome = default;
            return false;
        }

        _completedOutcomeTaken = true;
        outcome = completedOutcome?.Invoke() ??
            (Entity.DiedInHazard
                ? RoomEnemyOutcome.HazardDeletion(countsAsEnemy)
                : RoomEnemyOutcome.EnemyDie(killableEnemyIndex));
        return true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.TakeCompletedKnockbackDeath() &&
            combat.CreateDeathPuff() is { } deathPuff)
        {
            spawns.Add(deathPuff with
            {
                DecrementsRoomCount = countsAsEnemy
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
