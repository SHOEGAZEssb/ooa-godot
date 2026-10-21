using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordEnemyRoomEntity : CombatEnemyRoomEntityAdapter<SwordEnemyCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, ILinkSwordStateAwareRoomEntity,
    IItemCollisionHittableRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    private readonly Func<bool> _freePartSlot;
    private readonly Func<byte> _random;
    private int _swordCollision=ItemCollisionType.L1Sword;

    internal SwordEnemyRoomEntity(SwordEnemyCharacter enemy, EnemyCombatSourceDescriptor source,
        Action<int> soundRequested, Func<bool> freePartSlot, Func<byte> random)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
                soundRequested, EnemySwordResponse.Knockback), collisionZ: () => enemy.ZFixed >> 8)
    { _freePartSlot = freePartSlot; _random = random; }

    public override int DimitriCollisionMode => Entity.CollisionMode;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool initializing = Entity.State == SwordEnemyState.Uninitialized;
        Entity.UpdateFrame(frame.Player.EnemyContactPosition, frame.ScentSeedTarget,
            swordSlotAvailable: !initializing || _freePartSlot(), frameCounter: frame.Counter);
        if (initializing && Entity.State != SwordEnemyState.Uninitialized)
            spawns.Add(new EnemySwordSpawn(Entity, CombatDescriptor.RequestSound, () => !IsSeedBurning));
        if (Entity.BurnKilled && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        bool initializing = Entity.State == SwordEnemyState.Uninitialized;
        ScreenTransitionPresentation result = Entity.PrepareForScreenTransition(!initializing || _freePartSlot());
        if (initializing && Entity.State != SwordEnemyState.Uninitialized)
            spawns.Add(new EnemySwordSpawn(Entity, CombatDescriptor.RequestSound, () => !IsSeedBurning));
        return result;
    }

    public void SetLinkSwordState(SwordActionState state, int swordLevel,int? itemCollisionType=null)
    { _swordCollision = itemCollisionType??SwordCollision.Type(state,swordLevel); }

    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        ApplyDamageCollision(_swordCollision,
            hitbox, sourcePosition, damage, spawns);

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyDamageCollision((int)collision, hitbox, sourcePosition, damage, spawns);

    private bool ApplyDamageCollision(int collision, Rect2 hitbox, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        int effect = EnemyBehaviorTables.Shared.SwordEnemyCollisionEffect(Entity.CollisionMode, collision);
        if (effect == CollisionEffect.None) return false;
        var strength = CollisionKnockback(collision, effect);
        return base.ApplySwordHit(hitbox, origin, damage, strength, spawns);
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (!NativeCollisionEligible(collisionType, bounds, EnemyBehaviorTables.Shared.SwordEnemyActiveCollisions, false)) return default;
        return ApplyStatusSeedCollision(EnemyBehaviorTables.Shared.SwordEnemyCollisionEffect(Entity.CollisionMode, collisionType), collisionType,
            bounds, origin, seed, spawns, _freePartSlot, _random);
    }
    protected override void BeginSeedStatus(bool ember, int damage)
    {
        if (ember) Entity.BeginEmberHit();
        else Entity.BeginPegasusHit();
    }
    protected override void CatchStatusGale(Vector2 origin, Func<byte> random)
    { Entity.ClearSeedStun(); BeginNativeGale(origin, random); }
    protected override bool SeedStatusDisablesCollision(int effect) => effect != CollisionEffect.SwordLowKnockback;
    protected override int SeedStatusBurnEffect => CollisionEffect.Effect34;

    public override void HandleLinkContact(Player player)
    {
        if (Entity.StunCounter != 0 && !(player.IsUsingShield &&
            CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))) return;
        base.HandleLinkContact(player);
    }
    public int BurnTargetId => Entity.Record.Id;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public override void ReleaseBurn() => Entity.ReleaseBurn();
}
