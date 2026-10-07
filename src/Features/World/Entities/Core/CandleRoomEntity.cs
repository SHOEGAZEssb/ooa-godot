using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CandleRoomEntity : CombatEnemyRoomEntityAdapter<CandleCharacter>, IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity, INativeEnemySlotRoomEntity, IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity,
    ISeedCollisionTarget, ISomariaBlockCollisionRoomEntity
{
    private readonly CandleBehaviorProfile _data = EnemyBehaviorTables.Shared.Candle;
    internal int Slot { get; private set; }
    internal Func<int, IRoomEntity?> ResolveSlot { get; private set; } = null!;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) { Slot = slot; ResolveSlot = resolve; }
    public bool MeleeReportsContact => true;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    internal CandleRoomEntity(CandleCharacter candle, EnemyCombatSourceDescriptor source, Action<int> sound)
        : base(candle, candle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, candle, candle.Record.DamageQuarters,
                candle.TakeSwordHit, candle.TakeBurnHit, (_, _) => { }, sound, EnemySwordResponse.Bump,
                deathPuffAllowed: () => false, completedOutcome: () => candle.ExplosionCompleted
                    ? RoomEnemyOutcome.SilentDefeat(source.KillableEnemyIndex, source.CountsAsEnemy)
                    : RoomEnemyOutcome.HazardDeletion(source.CountsAsEnemy), acceptedHitSound: 0)) { }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.PrepareForScreenTransition(); return ScreenTransitionPresentation.Visible; }
    private bool Eligible(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.NativeHitPending &&
        Entity.InvincibilityCounter == 0 && _data.ActiveCollisions[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private int Effect(int collision) => (Entity.State == 14 ? _data.ExplosionEffects : _data.Effects)[collision].Value;
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        Hit(ItemCollisionType.L1Sword, bounds, origin);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        Hit(ItemCollisionType.BiggoronSword, bounds, origin);
    private bool Hit(int collision, Rect2 bounds, Vector2 origin)
    {
        if (!Eligible(collision, bounds)) return false;
        switch (Effect(collision))
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect20:
                // Non-Mystery seeds also publish JUST_HIT through
                // collisionEffect20's ENEMYDMG_$44 fallthrough. Candle's
                // handler consumes Ember's $9b on the next eligible update.
                Entity.NotifySeedConsumed(collision, origin); return true;
            case CollisionEffect.Effect1c: Entity.NotifyCollision(collision); return true;
            case CollisionEffect.BumpLowKnockback: case CollisionEffect.ShieldBump:
                if (!Entity.Bump(origin)) return false;
                if (Effect(collision) == CollisionEffect.ShieldBump) CombatDescriptor.RequestSound(SoundId.SndBombLand);
                return true;
            default: throw new NotSupportedException($"candle.s: ENEMY$55 mode${Entity.CollisionMode:x2} collision${collision:x2} effect${Effect(collision):x2}.");
        }
    }
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, bounds, origin);
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(0, item.ZHigh, 7) ||
            !Hit(ItemCollisionType.L1Boomerang, item.CollisionBounds, item.Position)) return default;
        return new(true, Effect(ItemCollisionType.L1Boomerang) != CollisionEffect.None);
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.BumpLowKnockback) return false;
        if (!Entity.Bump(linkPosition)) return false;
        hook.NotifyObjectCollision(); return true;
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || !player.EnemyContactHeightOverlaps(0)) return;
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters)) Entity.NotifyCollision(ItemCollisionType.Link);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Candle $55 requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(collision, bounds, origin)) return default;
        return Effect(collision) == CollisionEffect.None ? new(true, SeedHitResult.None, false) :
            new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
}
