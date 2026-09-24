using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WhispRoomEntity
    : CombatEnemyRoomEntityAdapter<WhispCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, ISomariaBlockCollisionRoomEntity, ISeedCollisionTarget,
        IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, IExpertPunchHittableRoomEntity,
        IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override void TransformByBoomerang() => Entity.ApplyBoomerangHit();
    private int _swordCollision = 4;
    private bool _meleeReportsContact;
    public bool MeleeReportsContact => _meleeReportsContact;
    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = state == SwordActionState.Spin ? (level >= 2 ? 8 : 7)
            : state is SwordActionState.Held or SwordActionState.Charged ? 9 : Math.Clamp(level, 1, 3) + 3;
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => ApplyMelee(_swordCollision, bounds);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyMelee(0x0b, bounds);
    private bool ApplyMelee(int collision, Rect2 bounds) => ApplyIneffectiveWeaponCollision(collision, bounds,
        EnemyBehaviorTables.Shared.WhispActiveCollisions, EnemyBehaviorTables.Shared.WhispCollisionEffects,
        out _meleeReportsContact);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (collision == RoomEntityItemCollision.Boomerang)
            return ApplyBoomerangTransformCollision(bounds, EnemyBehaviorTables.Shared.WhispActiveCollisions,
                EnemyBehaviorTables.Shared.WhispCollisionEffects, Entity.ApplyBoomerangHit);
        return ApplyIneffectiveWeaponCollision((int)collision, bounds,
            EnemyBehaviorTables.Shared.WhispActiveCollisions, EnemyBehaviorTables.Shared.WhispCollisionEffects, out _);
    }
    private readonly Func<byte> _random;
    internal WhispRoomEntity(
        WhispCharacter whisp,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested, Func<byte> random)
        : base(
            whisp,
            whisp.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                whisp,
                whisp.Record.DamageQuarters,
                whisp.TakeSwordHit,
                whisp.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback,
                completedOutcome: () => whisp.TransformationCompleted
                    ? RoomEnemyOutcome.SilentDeletion(false) : RoomEnemyOutcome.EnemyDie(combatSource.KillableEnemyIndex),
                acceptedHitSound: 0))
    { _random = random; }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == 0x24) throw new InvalidOperationException("Whisp requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & 0x7f, spawns).Effect;
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            EnemyBehaviorTables.Shared.WhispActiveCollisions[collisionType].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox)) return default;
        int effect = EnemyBehaviorTables.Shared.WhispCollisionEffects[collisionType].Value;
        switch (effect)
        {
            case 0: return new(true, SeedHitResult.None, false);
            case 0x20: break;
            case 0x29:
                // Unlike enemies whose JUST_HIT handler returns, Whisp falls
                // through to its state5 handler on the next enemy update.
                TryCatchGale(hitbox, 0, _random);
                break;
            case 0x35: Entity.ApplyUnrandomizedMysteryHit(); break;
            default: throw new NotSupportedException($"whisp.s $19 seed collision ${collisionType:x2}: effect ${effect:x2} is not represented.");
        }
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != 0x35);
    }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, false, spawns);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != 0x1c) return false;
        // Whisp's JUST_HIT handler ignores $8d and continues normal movement.
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
