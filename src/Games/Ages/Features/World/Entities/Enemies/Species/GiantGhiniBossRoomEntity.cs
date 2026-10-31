using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class GiantGhiniBossRoomEntity
    : CombatEnemyRoomEntityAdapter<GiantGhiniBoss>, IFixedRoomEntity,
        IPlayerRestriction, IScreenTransitionPreloadRoomEntity
{
    protected override int GaleCollisionMode => ObjectCollisionDatabase.For(AgesGameContent.AssetRoot).EnemyMode(Entity.Record.Id);

    public GiantGhiniBossRoomEntity(GiantGhiniBoss boss)
        : base(
            boss, boss.SetTransitionDrawOffset,
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
    }

    public bool DisablesSword => false;
    public bool DisablesItems => Entity.State is GiantGhiniBossBossState.IntroWait or
        GiantGhiniBossBossState.IntroFlicker;
    public bool DisablesMovement => DisablesItems;
    public bool DisablesMenus => DisablesItems;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, spawns);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition(spawns);
        return ScreenTransitionPresentation.Visible;
    }

}
