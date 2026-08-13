using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FlyingTileRoomEntity
    : CombatEnemyRoomEntityAdapter<FlyingTileCharacter>, IFixedRoomEntity,
        IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _collisionEffects =
        EnemyBehaviorTables.Shared.FlyingTileCollisionEffects;
    internal FlyingTileRoomEntity(
        FlyingTileCharacter tile,
        bool countsAsEnemy)
        : base(
            tile,
            tile.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => tile.IsDead,
                    () => tile.CollisionBounds,
                    (_, _) => tile.QueueCollisionBreak(),
                    _ => false,
                    tile.OverlapsLink,
                    () => tile.Position,
                    tile.Record.DamageQuarters,
                    () => null),
                countsAsEnemy,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.SilentDeletion(countsAsEnemy)),
            collisionZ: () => tile.ZHigh)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    public override void HandleLinkContact(Player player)
    {
        if (player.IsUsingShield &&
            CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            int effect = player.Inventory.ShieldLevel == 1 ? 0x07 : 0x06;
            RequireCollisionEffect(
                Math.Clamp(player.Inventory.ShieldLevel, 1, 3), effect);
            if (player.CanAcceptShieldCollision &&
                Entity.QueueCollisionBreak())
            {
                player.ApplyShieldCollisionRecoil(
                    Entity.Position,
                    effect == 0x07 ? 0x16 : 0x0f,
                    effect == 0x07 ? 0x19 : 0x13);
            }
            return;
        }
        CombatDescriptor.Combat.HandleLinkContact(player);
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        int collisionType = knockbackStrength switch
        {
            EnemyKnockbackStrength.Low => 0x04,
            EnemyKnockbackStrength.Normal => 0x05,
            EnemyKnockbackStrength.High => 0x08,
            _ => throw new ArgumentOutOfRangeException(
                nameof(knockbackStrength), knockbackStrength,
                "Flying Tile received an unknown sword collision strength.")
        };
        return ApplyBreakingCollision(collisionType, hitbox);
    }

    public bool ApplyExpertPunch(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        ApplyBreakingCollision(
            (int)RoomEntityItemCollision.ExpertPunch, hitbox);

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) => collision switch
    {
        RoomEntityItemCollision.ExpertPunch =>
            ApplyExpertPunch(hitbox, sourcePosition, damage, spawns),
        RoomEntityItemCollision.ThrownObject or RoomEntityItemCollision.Bomb =>
            ApplyBreakingCollision((int)collision, hitbox),
        RoomEntityItemCollision.SwordBeam =>
            ApplyStatusCollision((int)collision, hitbox),
        _ => false
    };

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        int collisionType = seedItem switch
        {
            0x24 => 0x1a,
            0x20 => 0x1b,
            0x21 => 0x1c,
            0x22 => 0x1d,
            0x23 => 0x1e,
            _ => -1
        };
        return collisionType >= 0 &&
            ApplyStatusCollision(collisionType, hitbox)
                ? SeedHitResult.Activate
                : SeedHitResult.None;
    }

    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.TakeDebrisRequest())
            spawns.Add(new RockDebrisSpawn(Entity.Position));
        base.OnFinished(spawns);
    }

    private bool ApplyBreakingCollision(int collisionType, Rect2 hitbox)
    {
        RequireCollisionEffect(collisionType, 0x1c);
        return hitbox.Intersects(Entity.CollisionBounds) &&
            Entity.QueueCollisionBreak();
    }

    private bool ApplyStatusCollision(int collisionType, Rect2 hitbox)
    {
        RequireCollisionEffect(collisionType, 0x20);
        return hitbox.Intersects(Entity.CollisionBounds) &&
            Entity.QueueCollisionBreak();
    }

    private void RequireCollisionEffect(int collisionType, int expectedEffect)
    {
        EnemyBehaviorValue effect = _collisionEffects[collisionType];
        if (effect.Value != expectedEffect)
        {
            throw new InvalidOperationException(
                $"{effect.Source} maps ENEMY_FLYING_TILE $3c item " +
                $"collision ${collisionType:x2} to effect ${effect.Value:x2}; " +
                $"runtime support requires ${expectedEffect:x2}.");
        }
    }
}
