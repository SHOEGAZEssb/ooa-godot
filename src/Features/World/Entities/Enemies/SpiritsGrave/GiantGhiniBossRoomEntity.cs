using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GiantGhiniBossRoomEntity
    : CombatEnemyRoomEntityAdapter<GiantGhiniBoss>, IFixedRoomEntity,
        IPlayerRestriction, IPlayerForcedMovement
{
    private readonly BossEntryMovement _entryMovement;
    private bool _initialized;

    public GiantGhiniBossRoomEntity(GiantGhiniBoss boss, Vector2I entryDirection)
        : base(
            boss, boss.SetTransitionDrawOffset,
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
            killableEnemyIndex: 0)
    {
        _entryMovement = new BossEntryMovement(entryDirection);
    }

    public bool DisablesSword => false;
    public bool DisablesItems => Entity.State is GiantGhiniBossBossState.IntroWait or
        GiantGhiniBossBossState.IntroFlicker;
    public bool DisablesMovement => DisablesItems;
    public bool DisablesMenus => DisablesItems;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
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
