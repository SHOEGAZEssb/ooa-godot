using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LikeLikeRoomEntity : CombatEnemyRoomEntityAdapter<LikeLikeCharacter>,
    IFixedRoomEntity, IPostObjectLinkContactRoomEntity, IScreenTransitionPreloadRoomEntity,
    IPostObjectItemCollisionRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override bool BoomerangHitPending => Entity.PendingHit;
    private readonly Func<bool> _freePart;
    private readonly Func<byte> _random;
    private readonly Action _shieldLost;
    private int _swordCollision = 4;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool MeleeReportsContact => true;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.PendingHit, spawns, deferNativeStatus: false);

    internal LikeLikeRoomEntity(LikeLikeCharacter enemy, EnemyCombatSourceDescriptor source, Action<int> sound,
        Func<bool> freePart, Func<byte> random, Action shieldLost)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, 0, enemy.TakeSwordHit, _ => false,
                enemy.ApplySwordKnockback, sound, EnemySwordResponse.Knockback), collisionZ: () => enemy.ZFixed >> 8)
    { _freePart = freePart; _random = random; _shieldLost = shieldLost; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, frame.AnyButtonJustPressed, frame.Counter, _shieldLost);
        if (Entity.IsDead && !Entity.DiedInHazard && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.InitializeState(); return ScreenTransitionPresentation.Visible; }

    public override void HandleLinkContact(Player player)
    {
        if (GaleCaught || !Entity.CollisionEnabled || Entity.PendingHit || Entity.StunCounter != 0 ||
            Entity.KnockbackCounter != 0 ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7) ||
            !Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Entity.CollisionBounds)) return;
        // $24's active mask excludes all shields. Effect$3a/$3d only publishes
        // status; the next enemy update performs the surrounded-wall test.
        if (player.RequestLikeLikeGrab()) Entity.MarkCapture();
    }

    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = state == SwordActionState.Spin ? (level >= 2 ? 8 : 7)
            : state is SwordActionState.Held or SwordActionState.Charged ? 9 : level + 3;
    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision, hitbox, sourcePosition, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 sourcePosition,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, hitbox, sourcePosition, damage, spawns);
    private bool Hit(int collision, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        var data = EnemyBehaviorTables.Shared.LikeLike;
        if (data.ActiveCollisions[collision].Value == 0) return false;
        int effect = data.CollisionEffects[collision].Value;
        if (effect == 0) return false;
        var strength = effect switch
        {
            8 => EnemyKnockbackStrength.Low, 9 => EnemyKnockbackStrength.Normal, 10 => EnemyKnockbackStrength.High,
            _ => throw new NotSupportedException($"ENEMY_LIKE_LIKE $24 collision ${collision:x2}: damage effect ${effect:x2} is not represented.")
        };
        return base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == 0x24) throw new InvalidOperationException("ENEMY_LIKE_LIKE $24 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & 0x7f, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        var data = EnemyBehaviorTables.Shared.LikeLike;
        if (GaleCaught || Entity.PendingHit || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            data.ActiveCollisions[collisionType].Value == 0 || !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return default;
        int effect = data.CollisionEffects[collisionType].Value;
        switch (effect)
        {
            case 0: return new(true, SeedHitResult.None, false);
            case 0x20: break;
            case 8: Hit(collisionType, bounds, origin, -(sbyte)seed.Damage, spawns); break;
            case 0x27:
                Entity.BeginEmberHit();
                if (_freePart()) spawns.Add(new BurningEnemySpawn(this));
                break;
            case 0x28: Entity.BeginPegasusHit(); CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); break;
            case 0x29: Entity.ClearSeedStun(); BeginNativeGale(origin, _random); break;
            default: throw new NotSupportedException($"ENEMY_LIKE_LIKE $24 seed collision ${collisionType:x2}: effect ${effect:x2} is not represented.");
        }
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != 8);
    }
    public bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public int BurnTargetId => 0x24;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public void ReleaseBurn() => Entity.InvincibilityCounter = 0;
}
