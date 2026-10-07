using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BubbleRoomEntity : CombatEnemyRoomEntityAdapter<BubbleCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, IPostObjectLinkContactRoomEntity,
    IPostObjectMeleeCollisionRoomEntity, IPostObjectItemCollisionRoomEntity,
    IExpertPunchHittableRoomEntity, ISeedCollisionTarget, IBoomerangCollisionRoomEntity,
    ISomariaBlockCollisionRoomEntity
{
    private readonly BubbleBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Bubble;
    public bool MeleeReportsContact => false;
    internal BubbleRoomEntity(BubbleCharacter enemy, EnemyCombatSourceDescriptor source, Action<int> sound)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, 0, enemy.TakeSwordHit,
                enemy.TakeBurnHit, (_, _) => { }, sound, EnemySwordResponse.None, acceptedHitSound: 0))
    {
        // The generic imported quarter count encodes raw $00 as 128; the
        // actual collision byte adds zero to Link's health. Keep that byte
        // distinction rather than applying a fabricated large contact hit.
        if (enemy.Record.RawDamage != 0)
            throw new NotSupportedException($"{source.Source}: ENEMY_BUBBLE $15 raw contact ${enemy.Record.RawDamage:x2} requires its source damage owner.");
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }

    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(0) ||
            !player.OverlapsEnemyCollision(Entity.CollisionBounds)) return;
        if (player.ApplyEnemyContactDamage(Entity.Position, 0, RingDamageSource.Generic,
            _behavior.ContactInvincibility, _behavior.ContactKnockback, allowZeroDamage: true))
            Entity.NotifyLinkContact();
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) => false;
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => false;
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyIneffectiveWeaponCollision((int)collision, bounds, _behavior.ActiveCollisions, _behavior.CollisionEffects, out _);
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1c) return false;
        hook.NotifyObjectCollision();
        return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Bubble $15 requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        bool hit = ApplyIneffectiveWeaponCollision(collisionType, bounds,
            _behavior.ActiveCollisions, _behavior.CollisionEffects, out _);
        if (!hit) return default;
        return _behavior.CollisionEffects[collisionType].Value == 0 ? new(true, SeedHitResult.None, false) :
            new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
}
