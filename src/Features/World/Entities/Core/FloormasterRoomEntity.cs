using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class FloormasterRoomEntity : CombatEnemyRoomEntityAdapter<FloormasterCharacter>, IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity, INativeEnemySlotRoomEntity, IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity,
    ISeedCollisionTarget, IBurningEnemyTarget, ISomariaBlockCollisionRoomEntity
{
    private readonly FloormasterBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Floormaster;
    private readonly Func<byte> _random;
    private readonly Func<bool> _freePart;
    internal int Slot { get; private set; } = -1;
    internal Func<int, IRoomEntity?> ResolveSlot { get; private set; } = null!;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) { Slot = slot; ResolveSlot = resolve; }
    public bool MeleeReportsContact => true;
    protected override bool Stunned => Entity.StunCounter != 0;

    internal FloormasterRoomEntity(FloormasterCharacter hand, EnemyCombatSourceDescriptor source,
        Action<int> sound, Func<byte> random, Func<bool> freePart)
        : base(hand, hand.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, hand, 0, hand.TakeSwordHit, hand.TakeBurnHit,
                hand.ApplySwordKnockback, sound, EnemySwordResponse.Knockback,
                deathPuffAllowed: () => hand.Record.SubId != 0 && !hand.RetreatCompleted,
                completedOutcome: () => hand.Record.SubId == 0 ?
                    RoomEnemyOutcome.SilentDefeat(source.KillableEnemyIndex, source.CountsAsEnemy) :
                    hand.RetreatCompleted ? RoomEnemyOutcome.SilentDeletion(false) : RoomEnemyOutcome.EnemyDieUncounted()),
            collisionZ: () => hand.ZFixed >> 8)
    { _random = random; _freePart = freePart; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player, frame.Counter);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.PrepareForScreenTransition(); return ScreenTransitionPresentation.Hidden; }
    public override void HandleLinkContact(Player player)
    {
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled || Entity.StunCounter != 0 ||
            !player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ)) return;
        // collisionEffect37 accepts normal Link and disables this hand's
        // collision immediately; Link consumes state$0c on the next update.
        if (player.RequestWallmasterGrab()) Entity.MarkCapture();
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        !Entity.NativeHitPending && base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, bounds, origin, damage, spawns);
    private bool Hit(int collision, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!NativeCollisionEligible(collision, bounds, _behavior.ActiveCollisions, Entity.NativeHitPending)) return false;
        int effect = _behavior.Effects[collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20) return true;
        var strength = CollisionKnockback(collision, effect);
        return base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collision, ICollection<RoomEntitySpawn> spawns)
    {
        if (!NativeCollisionEligible(collision, bounds, _behavior.ActiveCollisions, Entity.NativeHitPending)) return default;
        return ApplyStatusSeedCollision(_behavior.Effects[collision].Value, collision,
            bounds, origin, seed, spawns, _freePart, _random);
    }
    protected override void BeginSeedStatus(bool ember, int damage)
    {
        if (ember) Entity.BeginEmberHit();
        else Entity.BeginPegasusHit();
    }
    protected override void CatchStatusGale(Vector2 origin, Func<byte> random)
    { Entity.EnterGaleState(); BeginNativeGale(origin, random); }
    protected override bool SeedStatusDisablesCollision(int effect) => effect != CollisionEffect.SwordLowKnockback;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public int BurnTargetId => Entity.Record.Id;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
}
