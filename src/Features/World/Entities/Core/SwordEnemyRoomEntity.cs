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
    private SwordActionState _swordState;
    private int _swordLevel = 1;

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

    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    { _swordState = state; _swordLevel = swordLevel; }

    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        ApplyDamageCollision(SwordCollision.Type(_swordState, _swordLevel),
            hitbox, sourcePosition, damage, spawns);

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyDamageCollision((int)collision, hitbox, sourcePosition, damage, spawns);

    private bool ApplyDamageCollision(int collision, Rect2 hitbox, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        int effect = EnemyBehaviorTables.Shared.SwordEnemyCollisionEffect(Entity.CollisionMode, collision);
        if (effect == CollisionEffect.None) return false;
        EnemyKnockbackStrength strength = effect switch
        {
            CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
            CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
            CollisionEffect.SwordHighKnockback => EnemyKnockbackStrength.High,
            _ => throw new NotSupportedException($"Sword enemy ${Entity.Record.Id:x2}:${Entity.Record.SubId:x2}, collision ${collision:x2}: effect ${effect:x2}.")
        };
        return base.ApplySwordHit(hitbox, origin, damage, strength, spawns);
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Sword enemies require Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            EnemyBehaviorTables.Shared.SwordEnemyActiveCollisions[collisionType].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox)) return default;
        int effect = EnemyBehaviorTables.Shared.SwordEnemyCollisionEffect(Entity.CollisionMode, collisionType);
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: break; // Consume an ineffective seed, including fire against Darknuts.
            case CollisionEffect.SwordLowKnockback:
                ApplyDamageCollision(collisionType, hitbox, origin, -(sbyte)seed.Damage, spawns);
                break;
            case CollisionEffect.PegasusSeed:
                Entity.BeginPegasusHit();
                CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
                break;
            case CollisionEffect.GaleSeed:
                Entity.ClearSeedStun();
                BeginNativeGale(origin, _random);
                break;
            case CollisionEffect.Effect34:
                Entity.BeginEmberHit();
                if (_freePartSlot()) spawns.Add(new BurningEnemySpawn(this));
                break;
            default: throw new NotSupportedException($"Sword enemy ${Entity.Record.Id:x2}:${Entity.Record.SubId:x2}, seed collision ${collisionType:x2}: effect ${effect:x2}.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != CollisionEffect.SwordLowKnockback);
    }

    public override void HandleLinkContact(Player player)
    {
        if (Entity.StunCounter != 0 && !(player.IsUsingShield &&
            CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))) return;
        base.HandleLinkContact(player);
    }
    public bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public int BurnTargetId => Entity.Record.Id;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public void ReleaseBurn() => Entity.ReleaseBurn();
}
