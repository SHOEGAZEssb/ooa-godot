
namespace Oracles;

internal sealed class KeeseRoomEntity
    : CombatEnemyRoomEntityAdapter<KeeseCharacter>, IFixedRoomEntity, ISomariaBlockCollisionRoomEntity,
        IBoomerangCollisionRoomEntity, INativeEnemyCounter2RoomEntity, IScreenTransitionPreloadRoomEntity
{
    public int Counter2 { get => Entity.Counter2; set => Entity.Counter2 = value; }
    public KeeseRoomEntity(
        KeeseCharacter keese,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            keese,
            keese.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                keese,
                keese.Record.DamageQuarters,
                (_, damage) => keese.TakeSwordHit(damage),
                keese.TakeSwordHit,
                keese.ApplySwordKnockback,
                soundRequested,
                deathPuffZHigh: () => keese.SpriteHeight),collisionZ:() => keese.SpriteHeight)
    { }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeStateZero();
        return ScreenTransitionPresentation.Visible;
    }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public override void HandleLinkContact(Player player) => HandleNativeLinkContact(player,Entity.Record.DamageQuarters);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position,frame.Counter,frame.LinkTarget ?? frame.Player.EnemyContactPosition);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
}
