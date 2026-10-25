using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

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
    public override void HandleLinkContact(Player player) => HandleNativeLinkContact(player, Entity.Record.DamageQuarters);
    protected override void OnNativeLinkHit() => Entity.MarkLinkHit();
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        !GaleCaught && !Entity.NativeHitPending && base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    private bool Eligible(int collision, Rect2 bounds) =>
        NativeCollisionEligible(collision, bounds, _data.ActiveCollisions, Entity.NativeHitPending);
    protected override void BeginSeedStatus(bool ember, int damage) => Entity.BeginSeedStatus(ember, damage);
    protected override bool SeedStatusAcceptsStrongKnockback => true;
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyNativeItemCollision(collision, bounds, origin, damage, spawns, _data.ActiveCollisions, _data.Effects);
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collision, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible(collision, bounds)) return default;
        return ApplyStatusSeedCollision(_data.Effects[collision].Value, collision,
            bounds, origin, seed, spawns, _freePart, _random);
    }
    protected override void CatchStatusGale(Vector2 origin, Func<byte> random)
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
        CatchStatusGale(bounds.GetCenter(), random); return true;
    }
    public int BurnTargetId => Entity.Record.Id;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
}

internal sealed record WizzrobeProjectileSpawn(Vector2 Position, int Angle, int ZHigh) : RoomEntitySpawn;
