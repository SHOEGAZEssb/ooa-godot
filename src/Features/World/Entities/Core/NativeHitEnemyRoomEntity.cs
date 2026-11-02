
namespace Oracles;

// Native effect publication stays species-owned; item geometry and notifications do not vary.
internal abstract class NativeHitEnemyRoomEntity<T>(T entity, Action<Vector2> setTransitionDrawOffset,
    EnemyCombatDescriptor combatDescriptor, Func<int> collisionZ)
    : CombatEnemyRoomEntityAdapter<T>(entity, setTransitionDrawOffset, combatDescriptor, collisionZ: collisionZ),
        IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IExpertPunchHittableRoomEntity, ISeedCollisionTarget, IBoomerangCollisionRoomEntity
    where T : EnemyCharacter
{
    private int _sword = 4;
    public bool MeleeReportsContact { get; protected set; }
    protected abstract bool Hit(int item, Rect2 bounds, Vector2 origin, int damage);
    protected abstract int ItemEffect(int item);

    public void SetLinkSwordState(SwordActionState state, int level, int? collision = null) =>
        _sword = collision ?? SwordCollision.Type(state, level);
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds, origin, damage);
    public bool ApplyItemCollision(RoomEntityItemCollision item, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit((int)item, bounds, origin, damage);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(11, bounds, origin, damage);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(7, bounds, origin, damage);
    public override bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns) =>
        bomb.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, bomb.CollisionZ, bomb.CollisionZRadius) &&
        Hit(0x18, bomb.CollisionBounds, bomb.CollisionBounds.GetCenter(), bomb.Damage);
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 link)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) ||
            !Hit(13, hook.CollisionBounds, hook.Position, hook.HitDamage)) return false;
        hook.NotifyObjectCollision();
        return true;
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) &&
        Hit(0x17, item.CollisionBounds, item.Position, item.Damage) ? new(true, MeleeReportsContact) : default;
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int item, ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException($"ENEMY ${DimitriCollisionType:x2} requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(item, bounds, origin, -(sbyte)seed.Damage)) return default;
        int effect = ItemEffect(item);
        return effect == 0 ? new(true, SeedHitResult.None, false) : new(true,
            seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect == 0x20);
    }

    protected (int Invincibility, int Knockback) ShieldRecoil(int effect, int shield, string source) => effect switch
    {
        CollisionEffect.Effect05 => (8, 11),
        CollisionEffect.Effect06 => (15, 19),
        CollisionEffect.Effect07 => (22, 25),
        _ => throw new NotSupportedException($"{source}: shield column ${shield:x2}, mode ${DimitriCollisionMode:x2}, effect ${effect:x2}.")
    };
}
