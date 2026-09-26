using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SparkRoomEntity
    : CombatEnemyRoomEntityAdapter<SparkCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, ISeedCollisionTarget, ISomariaBlockCollisionRoomEntity,
        IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, IExpertPunchHittableRoomEntity,
        IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override void TransformByBoomerang() => Entity.ApplyBoomerangHit();
    private int _swordCollision = ItemCollisionType.L1Sword;
    private bool _meleeReportsContact;
    public bool MeleeReportsContact => _meleeReportsContact;
    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = SwordCollision.Type(state, level);
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => ApplyMelee(_swordCollision, bounds);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyMelee(ItemCollisionType.ExpertPunch, bounds);
    private bool ApplyMelee(int collision, Rect2 bounds) => ApplyIneffectiveWeaponCollision(collision, bounds,
        EnemyBehaviorTables.Shared.SparkActiveCollisions, EnemyBehaviorTables.Shared.SparkCollisionEffects,
        out _meleeReportsContact);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (collision == RoomEntityItemCollision.Boomerang)
            return ApplyBoomerangTransformCollision(bounds, EnemyBehaviorTables.Shared.SparkActiveCollisions,
                EnemyBehaviorTables.Shared.SparkCollisionEffects, Entity.ApplyBoomerangHit);
        return ApplyIneffectiveWeaponCollision((int)collision, bounds,
            EnemyBehaviorTables.Shared.SparkActiveCollisions, EnemyBehaviorTables.Shared.SparkCollisionEffects, out _);
    }
    internal SparkRoomEntity(
        SparkCharacter spark,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            spark,
            spark.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                spark,
                spark.Record.DamageQuarters,
                spark.TakeSwordHit,
                spark.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback,
                completedOutcome: () => spark.TransformationCompleted
                    ? RoomEnemyOutcome.SilentDeletion(false) : RoomEnemyOutcome.EnemyDie(combatSource.KillableEnemyIndex),
                acceptedHitSound: 0))
    { }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, false, spawns);

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Spark requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            EnemyBehaviorTables.Shared.SparkActiveCollisions[collisionType].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox)) return default;
        int effect = EnemyBehaviorTables.Shared.SparkCollisionEffects[collisionType].Value;
        if (effect == CollisionEffect.None) return new(true, SeedHitResult.None, false);
        if (effect != CollisionEffect.Effect20)
            throw new NotSupportedException($"spark.s $13 seed collision ${collisionType:x2}: effect ${effect:x2} is not represented.");
        // collisionEffect20 consumes the seed without enemy damage/status.
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1c) return false;
        // Spark's JUST_HIT handler ignores $8d and continues normal movement.
        hook.NotifyObjectCollision();
        return true;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
