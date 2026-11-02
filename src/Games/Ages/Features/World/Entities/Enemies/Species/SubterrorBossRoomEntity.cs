
namespace Oracles;

internal sealed class SubterrorBossRoomEntity
    : CombatEnemyRoomEntityAdapter<SubterrorBoss>, IFixedRoomEntity,
        IShovelHittableRoomEntity, IPlayerRestriction,
        IScreenTransitionPreloadRoomEntity
{
    protected override int GaleCollisionMode => ObjectCollisionDatabase.For(AgesGameContent.AssetRoot).EnemyMode(Entity.Record.Id);

    internal SubterrorBossRoomEntity(
        SubterrorBoss boss)
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
    }


    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Hidden;
    }

    public bool ApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition) =>
        Entity.TryApplyShovelHit(hitbox, sourcePosition);
}
