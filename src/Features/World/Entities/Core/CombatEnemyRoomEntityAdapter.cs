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
    Func<bool>? marksEnemyKilled = null,
    Action? finished = null,
    Func<int>? collisionZ = null)
    : RoomEntityAdapter<T>(entity, setTransitionDrawOffset),
        ILinkContactEntity, ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        ISeedBurnTarget, IRoomEntityLifetime,
        IRoomEnemyCounterEntity, IRoomKillTrackedEnemy,
        IObjectCollisionHeightRoomEntity
    where T : Node2D
{
    private bool _seedBurning;

    public bool Finished => combat.Finished;
    public bool CountsAsEnemy => countsAsEnemy && !combat.Finished;
    public bool IsSeedBurning => _seedBurning;
    public Vector2 SeedBurnPosition => Entity.Position;
    public int KillableEnemyIndex => killableEnemyIndex;
    // enemyDie and enemyDie_uncounted both advance the lifetime/special-ring
    // counters. Only the separate recent-defeat reservation requires a
    // nonzero wKillableEnemyIndex.
    public bool MarksEnemyKilled => marksEnemyKilled?.Invoke() ?? true;
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
        ICollection<RoomEntitySpawn> spawns) =>
        !_seedBurning && combat.ApplySwordHit(hitbox, sourcePosition, damage, spawns);
    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns) =>
        ApplySeedHit(hitbox);

    private SeedHitResult ApplySeedHit(Rect2 hitbox)
    {
        if (_seedBurning || !combat.Intersects(hitbox))
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
        combat.ApplyBurnHit(2, spawns);
    }
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) => finished?.Invoke();
}

internal enum SeedHitResult
{
    None,
    Ignite,
    Consume
}

internal abstract record RoomEntitySpawn(bool UpdateThisFrame = false);
