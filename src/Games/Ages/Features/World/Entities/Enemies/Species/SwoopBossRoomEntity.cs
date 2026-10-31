using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SwoopBossRoomEntity
    : CombatEnemyRoomEntityAdapter<SwoopBoss>, IFixedRoomEntity,
        IPlayerRestriction, IScreenTransitionPreloadRoomEntity
{
    protected override int GaleCollisionMode => ObjectCollisionDatabase.For(AgesGameContent.AssetRoot).EnemyMode(Entity.Record.Id);

    internal SwoopBossRoomEntity(SwoopBoss boss)
        : base(
            boss,
            boss.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => boss.IsDead,
                    () => boss.CollisionBounds,
                    boss.TakeSwordHit,
                    boss.TakeBurnHit,
                    boss.OverlapsLinkAtCollisionHeight,
                    () => boss.Position,
                    boss.Record.DamageQuarters,
                    () => null),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.BossTeardown(
                        killableEnemyIndex: 0)),
            collisionZ: () => boss.ZFixed >> 8)
    {
    }

    // wDisabledObjects remains set through Swoop's one-time entrance and is
    // cleared when state 8's first flying-up handoff reaches state $0a. Later
    // calls to swoop_beginFlyingUp do not write it again.
    public bool DisablesSword => Entity.IntroActive;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, spawns);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition(spawns);
        return ScreenTransitionPresentation.Hidden;
    }

}
