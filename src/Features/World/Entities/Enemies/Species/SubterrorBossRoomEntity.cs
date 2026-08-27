using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SubterrorBossRoomEntity
    : CombatEnemyRoomEntityAdapter<SubterrorBoss>, IFixedRoomEntity,
        IShovelHittableRoomEntity, IPlayerRestriction, IPlayerForcedMovement
{
    private readonly BossEntryMovement _entryMovement;
    private bool _initialized;

    internal SubterrorBossRoomEntity(
        SubterrorBoss boss,
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
        Entity.UpdateFrame(frame.Player, spawns);
        if (_initialized)
            return;
        _initialized = true;
        _entryMovement.Arm();
    }

    public void UpdatePlayerForcedMovement(Player player) =>
        _entryMovement.Update(player);

    public bool ApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition) =>
        Entity.TryApplyShovelHit(hitbox, sourcePosition);
}
