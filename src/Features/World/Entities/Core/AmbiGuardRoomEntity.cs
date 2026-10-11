using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class AmbiGuardRoomEntity : CombatEnemyRoomEntityAdapter<AmbiGuardCharacter>, IFixedRoomEntity,
    INativeEnemySlotRoomEntity, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, ISeedCollisionTarget, IBoomerangCollisionRoomEntity,
    ISomariaBlockCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, IScreenTransitionPreloadRoomEntity
{
    private int _swordCollision = ItemCollisionType.L1Sword;
    private readonly AmbiGuardDatabase _data;
    private readonly EnemyCombatSourceDescriptor _source;
    private readonly Action<int> _sound;
    internal AmbiGuardCharacter Actor => Entity;
    internal int Slot { get; private set; } = -1;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Slot = slot;
    public bool MeleeReportsContact => true;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    internal AmbiGuardRoomEntity(AmbiGuardCharacter guard, AmbiGuardDatabase data,
        EnemyCombatSourceDescriptor source, Action<int> sound)
        : base(guard, guard.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, guard, 2, guard.TakeSwordHit, guard.TakeBurnHit,
                (origin, strength) => { if (guard.CollisionMode == 0x3d) guard.ApplySwordKnockback(origin, strength); },
                sound, EnemySwordResponse.NoKnockback, deathPuffAllowed: () => false,
                completedOutcome: () => RoomEnemyOutcome.SilentDeletion(false)), collisionZ: () => guard.ZFixed >> 8)
    { _data = data; _source = source; _sound = sound; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame, spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeStateZero();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter != 0 ||
            !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (Entity.CollisionMode == 0x3d && player.IsUsingShield &&
            (_source with { ShieldLevel1Effect = _data.Collision(0x3d, 1).Effect,
                ShieldLevel2Effect = _data.Collision(0x3d, 2).Effect,
                ShieldLevel3Effect = _data.Collision(0x3d, 3).Effect }).ShieldBumpResponse(player.Inventory.ShieldLevel) is { } shield &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (player.CanAcceptShieldCollision && Entity.TryApplyShieldBump(player.ShieldCollisionBounds,
                player.ShieldCollisionBounds.GetCenter(), shield.EnemyStrength))
            {
                Entity.RecordCollision(player.Inventory.ShieldLevel, player.ShieldCollisionBounds.GetCenter());
                player.ApplyShieldCollisionRecoil(Entity.Position, shield.LinkInvincibilityFrames, shield.LinkKnockbackFrames);
                _sound(SoundId.SndBombLand);
            }
            return;
        }
        if (!player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ)) return;
        if (Entity.CollisionMode == 0x5a) Entity.RecordCollision(0, player.Position);
        else base.HandleLinkContact(player);
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision, bounds, origin, damage, spawns);
    public void SetLinkSwordState(SwordActionState state, int swordLevel, int? itemCollisionType = null) =>
        _swordCollision = itemCollisionType ?? SwordCollision.Type(state, swordLevel);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, bounds, origin, damage, spawns);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit(7, bounds, origin, damage, spawns);
    private bool Hit(int collision, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        var response = _data.Collision(Entity.CollisionMode, collision);
        if (!response.Enabled || !Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter != 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        if (response.Effect is 0 or 0x20 or 0x2d) return true;
        if (response.Effect == 0x30) { Entity.RecordCollision(collision, origin); return true; }
        if (response.Effect == 0x1b)
        {
            Entity.RecordDeflection(collision);
            spawns.Add(new EnemyClinkSpawn(BoomerangCollisionResponse.Midpoint(Entity.Position, origin)));
            return true;
        }
        var strength = response.Effect switch
        { 8 => EnemyKnockbackStrength.Normal, 9 => EnemyKnockbackStrength.Low, 10 => EnemyKnockbackStrength.High,
            _ => throw new NotSupportedException($"ambiGuard.s: collision${collision:x2} mode${Entity.CollisionMode:x2} effect${response.Effect:x2} requires its native response.") };
        bool accepted = base.ApplySwordHit(bounds, origin, damage, strength, spawns);
        if (accepted) Entity.RecordCollision(collision, origin);
        return accepted;
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !Hit(0x17, item.CollisionBounds, item.Position, 1, spawns)) return default;
        return new(true, true, BoomerangCollisionResponse.Midpoint(Entity.Position, item.Position));
    }
    protected override bool TryApplySwitchHookEffect(int effect,SwitchHookItem hook,Vector2 link)
    {
        if (effect!=0x1b) return false;
        Entity.RecordDeflection(0x0d);
        hook.NotifyObjectCollision(BoomerangCollisionResponse.Midpoint(Entity.Position,hook.Position)); return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY_AMBI_GUARD $54 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & 31, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        var response = _data.Collision(Entity.CollisionMode, collision);
        bool accepted = Hit(collision, bounds, origin, -unchecked((sbyte)seed.Damage), spawns);
        return accepted ? new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            response.Effect is 0 or 0x1b or 0x20 or 0x2d) : default;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7)) return false;
        if (Entity.CollisionMode == 0x5a) return Hit(0x15, block.CollisionBounds, block.Position, 0, spawns);
        return ApplySomariaBlockCollision(block, 0xfc, Entity.NativeHitPending, spawns);
    }
    protected override void ApplySomariaEnemyDamage(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    { base.ApplySomariaEnemyDamage(block, spawns); Entity.RecordCollision(0x15, block.Position); }
}
