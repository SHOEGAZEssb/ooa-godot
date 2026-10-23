using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TargetGhiniRoomEntity : CombatEnemyRoomEntityAdapter<TargetGhiniCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, INativeEnemyCounter1RoomEntity,
    IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget, IGaleSeedTarget
{
    private readonly TargetGhiniBehaviorProfile _data = EnemyBehaviorTables.Shared.TargetGhini;
    private readonly Func<byte> _random;
    private readonly Func<bool> _freePart;
    protected override bool Stunned => Entity.StunCounter != 0;
    public bool MeleeReportsContact => true;
    public int Counter1 { get => Entity.Counter; set => Entity.Counter = value; }
    public bool RetainsCounter1AfterDeletion => false;

    internal TargetGhiniRoomEntity(TargetGhiniCharacter enemy, EnemyCombatSourceDescriptor source,
        Action<int> sound, Func<byte> random, Func<bool> freePart)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback, sound, EnemySwordResponse.Knockback),
            collisionZ: () => enemy.ZFixed >> 8)
    { _random = random; _freePart = freePart; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Counter);
        if (Entity.IsDead && !Entity.HasCompletedKnockbackDeath && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.PrepareForScreenTransition(); return ScreenTransitionPresentation.Visible; }

    public override void HandleLinkContact(Player player) => HandleNativeLinkContact(player, Entity.Record.DamageQuarters);
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
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || Entity.NativeHitPending ||
            !Entity.TakeSwordHit(linkPosition, hook.HitDamage)) return false;
        Entity.ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        Entity.DeferNativeHitStatus(); hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collision, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible(collision, bounds)) return default;
        return ApplyStatusSeedCollision(_data.Effects[collision].Value, collision,
            bounds, origin, seed, spawns, _freePart, _random);
    }
    public int BurnTargetId => Entity.Record.Id;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public override void ReleaseBurn() => Entity.CompleteBurn();
    protected override void CatchStatusGale(Vector2 origin, Func<byte> random)
    { Entity.EnterGaleState(); BeginNativeGale(origin, random); }
    bool IGaleSeedTarget.TryCatchGale(Rect2 bounds, int seedZ, Func<byte> random)
    {
        if (!Eligible(ItemCollisionType.GaleSeed, bounds) || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, seedZ, 7)) return false;
        CatchStatusGale(bounds.GetCenter(), random); return true;
    }
}
