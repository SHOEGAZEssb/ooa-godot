using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwoopBossRoomEntity
    : CombatEnemyRoomEntityAdapter<SwoopBoss>, IFixedRoomEntity,
        IPlayerRestriction, IPlayerForcedMovement
{
    private readonly BossEntryMovement _entryMovement;
    private bool _initialized;

    internal SwoopBossRoomEntity(SwoopBoss boss, Vector2I entryDirection)
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
                    RoomEnemyOutcome.BossTeardown(
                        killableEnemyIndex: 0)))
    {
        _entryMovement = new BossEntryMovement(entryDirection);
    }

    public bool DisablesSword => Entity.State is
        SwoopState.WaitingForDoors or
        SwoopState.IntroFalling or
        SwoopState.IntroDialogue or
        SwoopState.FlyingUp;
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
}
