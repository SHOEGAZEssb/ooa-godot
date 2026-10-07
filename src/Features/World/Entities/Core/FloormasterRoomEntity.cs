using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

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
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            _behavior.ActiveCollisions[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        int effect = _behavior.Effects[collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20) return true;
        var strength = effect switch
        {
            CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
            CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
            CollisionEffect.SwordHighKnockback => EnemyKnockbackStrength.High,
            _ => throw new NotSupportedException($"floorMaster.s: ENEMY $35:${Entity.Record.SubId:x2} collision${collision:x2} effect${effect:x2} needs its native response.")
        };
        return base.ApplySwordHit(bounds, origin, damage, strength, spawns);
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY_FLOORMASTER $35 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            _behavior.ActiveCollisions[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return default;
        int effect = _behavior.Effects[collision].Value;
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: break;
            case CollisionEffect.SwordLowKnockback: Hit(collision, bounds, origin, -unchecked((sbyte)seed.Damage), spawns); break;
            case CollisionEffect.Burn:
                Entity.BeginEmberHit();
                if (_freePart()) spawns.Add(new BurningEnemySpawn(this));
                break;
            case CollisionEffect.PegasusSeed: Entity.BeginPegasusHit(); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); break;
            case CollisionEffect.GaleSeed: Entity.EnterGaleState(); BeginNativeGale(origin, _random); break;
            default: throw new NotSupportedException($"floorMaster.s: ENEMY $35 seed collision${collision:x2} effect${effect:x2} is unrepresented.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            effect != CollisionEffect.SwordLowKnockback);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public int BurnTargetId => Entity.Record.Id;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public void ReleaseBurn() => Entity.InvincibilityCounter = 0;
}
