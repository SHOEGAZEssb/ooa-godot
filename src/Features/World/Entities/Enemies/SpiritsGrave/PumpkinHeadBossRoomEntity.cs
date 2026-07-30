using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PumpkinHeadBossRoomEntity : IRoomEntity, IFixedRoomEntity,
    ILinkContactEntity, ISwordHittableRoomEntity, ISeedHittableRoomEntity,
    IRoomEntityLifetime, IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource,
    IPlayerRestriction, IBraceletInteractableRoomEntity, IPlayerForcedMovement,
    IObjectCollisionHeightRoomEntity
{
    private readonly PumpkinHeadBoss _boss;
    private readonly int _damage;
    private readonly BossEntryMovement _entryMovement;
    private bool _initialized;
    private bool _outcomeTaken;
    internal PumpkinHeadBossRoomEntity(
        PumpkinHeadBoss boss,
        int damage,
        Vector2I entryDirection)
    {
        _boss = boss;
        _damage = damage;
        _entryMovement = new BossEntryMovement(entryDirection);
    }

    public Node2D Node => _boss;
    public bool Finished => _boss.IsDead;
    public bool CountsAsEnemy => !_boss.IsDead;
    public bool DisablesSword => false;
    public bool DisablesItems => _boss.IntroActive;
    public bool DisablesMovement => _boss.IntroActive;
    public bool DisablesMenus => _boss.IntroActive;
    public int CollisionZ => _boss.CollisionZ;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _boss.UpdateFrame(frame.Player, spawns);
        if (_initialized)
            return;
        _initialized = true;
        _entryMovement.Arm();
    }
    public void UpdatePlayerForcedMovement(Player player) =>
        _entryMovement.Update(player);
    public void HandleLinkContact(Player player) => _boss.HandleLinkContact(player, _damage);
    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        _boss.ApplySwordHit(hitbox, sourcePosition, damage, spawns);
    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        seedItem == 0x24 && _boss.CollisionBounds.Intersects(hitbox)
            ? SeedHitResult.Activate
            :
        seedItem == 0x20 &&
        _boss.ApplySwordHit(hitbox, sourcePosition, 2, spawns)
            ? SeedHitResult.Consume
            : SeedHitResult.None;
    public bool TryUseBracelet(
        Player player,
        Vector2I releaseDirection) =>
        _boss.TryUseBracelet(player, releaseDirection);

    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!Finished || _outcomeTaken)
        {
            outcome = default;
            return false;
        }

        _outcomeTaken = true;
        outcome = RoomEnemyOutcome.BossTeardown(killableEnemyIndex: 0);
        return true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) =>
        _boss.SetTransitionDrawOffset(offset);
}
