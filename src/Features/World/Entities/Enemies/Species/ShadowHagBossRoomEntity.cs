using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ShadowHagBossRoomEntity
    : CombatEnemyRoomEntityAdapter<ShadowHagBoss>, IFixedRoomEntity,
        IPlayerRestriction, IPlayerForcedMovement,
        IScreenTransitionPreloadRoomEntity
{
    private readonly BossEntryMovement _entryMovement;
    private bool _initialized;

    internal ShadowHagBossRoomEntity(
        ShadowHagBoss boss,
        Vector2I entryDirection)
        : base(
            boss,
            boss.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => boss.IsDead,
                    () => boss.CollisionBounds,
                    boss.TakeSwordHit,
                    boss.TakeBurnHit,
                    boss.OverlapsLink,
                    () => boss.Position,
                    boss.Record.DamageQuarters,
                    () => null),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.BossTeardown(0)))
    {
        _entryMovement = new BossEntryMovement(entryDirection);
    }

    public bool DisablesSword => Entity.IntroActive || Entity.Defeated;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, frame.Counter, spawns);
        if (_initialized)
            return;
        _initialized = true;
        _entryMovement.Arm();
    }

    public void UpdatePlayerForcedMovement(Player player) =>
        _entryMovement.Update(player);

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Hidden;
    }

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = sourcePosition;
        _ = spawns;
        if (seedItem is < 0x20 or > 0x24 || !Entity.Vulnerable ||
            !Entity.CollisionBounds.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        // Collision mode $4b routes only Ember ($1b) and Scent ($1c) through
        // collisionEffect21 / ENEMYDMG_$30. Mystery, Pegasus, and Gale use
        // collisionEffect20 / ENEMYDMG_$44, which consumes their collision
        // without reducing Shadow Hag's health.
        if ((seedItem is 0x20 or 0x21) &&
            !Entity.TakeSeedHit(damage: 2))
            return SeedHitResult.None;
        return seedItem is 0x21 or 0x24
            ? SeedHitResult.Activate
            : SeedHitResult.Consume;
    }
}
