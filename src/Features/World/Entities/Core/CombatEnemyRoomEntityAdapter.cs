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
        if (_seedBurning && FreezesDuringSeedBurn)
            return;

        if (player.IsUsingShield &&
            combatDescriptor.Source is { } source &&
            source.ShieldBumpResponse(player.Inventory.ShieldLevel) is
                { } response &&
            combatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            if (player.CanAcceptShieldCollision &&
                Entity.TryApplyShieldBump(
                player.ShieldCollisionBounds,
                player.ShieldCollisionBounds.GetCenter(),
                response.EnemyStrength))
            {
                player.ApplyShieldCollisionRecoil(
                    Entity.Position,
                    response.LinkInvincibilityFrames,
                    response.LinkKnockbackFrames);
                combatDescriptor.RequestSound(OracleSoundEngine.SndBombLand);
            }
            return;
        }

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
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == 0x21)
        {
            if (_seedBurning || !Entity.CollisionEnabled ||
                !combatDescriptor.Combat.Intersects(hitbox))
            {
                return SeedHitResult.None;
            }
            // ITEM_SCENT_SEED's standard collision-table entry is effect $08
            // with item damage $fe: two health units, normal knockback, then
            // the seed enters its non-attracting state-$03 effect.
            return combatDescriptor.Combat.ApplySwordHit(
                    hitbox,
                    sourcePosition,
                    2,
                    EnemyKnockbackStrength.Normal,
                    spawns,
                    deathPuffDecrementsRoomCount:
                        combatDescriptor.CountsAsEnemy)
                ? SeedHitResult.Activate
                : SeedHitResult.None;
        }
        return ApplySeedHit(hitbox, seedItem);
    }

    private SeedHitResult ApplySeedHit(Rect2 hitbox, int seedItem)
    {
        if (_seedBurning || !Entity.CollisionEnabled ||
            !combatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        if (seedItem == 0x24)
            return SeedHitResult.Activate;
        if (seedItem != 0x20)
            return SeedHitResult.None;
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

    protected static Vector2 CollisionMidpoint(
        Vector2 enemyPosition,
        Vector2 itemPosition)
    {
        int enemyY = Mathf.FloorToInt(enemyPosition.Y);
        int enemyX = Mathf.FloorToInt(enemyPosition.X);
        int itemY = Mathf.FloorToInt(itemPosition.Y);
        int itemX = Mathf.FloorToInt(itemPosition.X);
        return new Vector2(
            enemyX + ((itemX - enemyX) >> 1),
            enemyY + ((itemY - enemyY) >> 1));
    }

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
    Activate,
    Consume
}

internal abstract record RoomEntitySpawn(bool UpdateThisFrame = false);
