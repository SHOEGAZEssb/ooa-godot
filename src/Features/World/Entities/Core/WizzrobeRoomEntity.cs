using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WizzrobeRoomEntity : CombatEnemyRoomEntityAdapter<WizzrobeCharacter>, IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity, INativeEnemySlotRoomEntity, INativeEnemyCounter1RoomEntity,
    IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget, IGaleSeedTarget
{
    private readonly WizzrobeBehaviorProfile _data = EnemyBehaviorTables.Shared.Wizzrobe;
    private readonly Func<byte> _random;
    private readonly Func<bool> _freePart;
    private bool _galeReservationPending;
    protected override bool Stunned => Entity.StunCounter != 0;
    public bool MeleeReportsContact => true;
    public int Counter1 { get => Entity.Counter1; set => Entity.Counter1 = value; }
    public bool RetainsCounter1AfterDeletion => false;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.BindEnemySlot(slot);
    internal WizzrobeRoomEntity(WizzrobeCharacter enemy, EnemyCombatSourceDescriptor source,
        Action<int> sound, Func<byte> random, Func<bool> freePart)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback, sound, EnemySwordResponse.Knockback),
            collisionZ: () => enemy.ZFixed >> 8)
    { _random = random; _freePart = freePart; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.ScentSeedTarget ?? frame.Player.Position, frame.Counter, out var position) && _freePart())
            spawns.Add(new WizzrobeProjectileSpawn(position, Entity.Angle, Entity.ZFixed >> 8));
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.PrepareForScreenTransition(); return ScreenTransitionPresentation.Hidden; }
    public override void HandleLinkContact(Player player)
    {
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int before = Entity.InvincibilityCounter;
            base.HandleLinkContact(player);
            if (before != Entity.InvincibilityCounter) Entity.DeferNativeHitStatus();
            return;
        }
        if (!Stunned && player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters)) Entity.MarkLinkHit();
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        !GaleCaught && !Entity.NativeHitPending && base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    private bool Eligible(int collision, Rect2 bounds) => !GaleCaught && !Entity.NativeHitPending && Entity.CollisionEnabled &&
        Entity.InvincibilityCounter == 0 && _data.ActiveCollisions[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible((int)collision, bounds)) return false;
        int effect = _data.Effects[(int)collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20) return true;
        var strength = effect switch
        {
            CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
            CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
            CollisionEffect.SwordHighKnockback => EnemyKnockbackStrength.High,
            _ => throw new NotSupportedException($"wizzrobe.s: ENEMY$40:${Entity.Record.SubId:x2} collision${(int)collision:x2}, effect${effect:x2} requires its native response.")
        };
        return ApplySwordHit(bounds, origin, damage, strength, spawns);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY$40 requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collision, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible(collision, bounds)) return default;
        int effect = _data.Effects[collision].Value;
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: break;
            case CollisionEffect.SwordLowKnockback:
            case CollisionEffect.Sword:
            case CollisionEffect.SwordHighKnockback:
                ApplyItemCollision((RoomEntityItemCollision)collision, bounds, origin, -unchecked((sbyte)seed.Damage), spawns);
                break;
            case CollisionEffect.Burn:
                Entity.BeginSeedStatus(ember: true, -unchecked((sbyte)seed.Damage));
                if (_freePart()) spawns.Add(new BurningEnemySpawn(this));
                break;
            case CollisionEffect.PegasusSeed:
                Entity.BeginSeedStatus(ember: false, 0); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
                break;
            case CollisionEffect.GaleSeed:
                CatchGale(origin, _random); break;
            default: throw new NotSupportedException($"wizzrobe.s: ENEMY$40:${Entity.Record.SubId:x2} seed collision${collision:x2}, effect${effect:x2}.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            effect is CollisionEffect.Effect20 or CollisionEffect.GaleSeed);
    }
    private void CatchGale(Vector2 origin, Func<byte> random)
    { Entity.EnterGaleState(); _galeReservationPending = true; BeginNativeGale(origin, random); }
    void IGaleSeedTarget.UpdateGale(int cameraY)
    {
        if (_galeReservationPending)
        {
            _galeReservationPending = false;
            if (Entity.Record.SubId == 1) Entity.RemoveReservation();
        }
        UpdateGale(cameraY);
    }
    bool IGaleSeedTarget.TryCatchGale(Rect2 bounds, int seedZ, Func<byte> random)
    {
        if (!Eligible(ItemCollisionType.GaleSeed, bounds) || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, seedZ, 7)) return false;
        CatchGale(bounds.GetCenter(), random); return true;
    }
    public bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public int BurnTargetId => Entity.Record.Id;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public void ReleaseBurn() => Entity.InvincibilityCounter = 0;
}

internal sealed record WizzrobeProjectileSpawn(Vector2 Position, int Angle, int ZHigh) : RoomEntitySpawn;
