using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WhispRoomEntity
    : CombatEnemyRoomEntityAdapter<WhispCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity, ISomariaBlockCollisionRoomEntity, ISeedCollisionTarget,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity, IExpertPunchHittableRoomEntity,
        IPostObjectItemCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    // bank0._updateEnemiesIfStateIsZero still dispatches whisp state0 during
    // palette fades, text and object freezes. State8 waits for normal updates.
    public bool UpdatesDuringDialogue => !Entity.Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Entity.Initialized;

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
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Whisp requires Mystery Seed's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
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
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: break;
            case CollisionEffect.GaleSeed:
                // Unlike enemies whose JUST_HIT handler returns, Whisp falls
                // through to its state5 handler on the next enemy update.
                TryCatchGale(hitbox, 0, _random);
                break;
            case CollisionEffect.Effect35: Entity.ApplyUnrandomizedMysteryHit(); break;
            default: throw new NotSupportedException($"whisp.s $19 seed collision ${collisionType:x2}: effect ${effect:x2} is not represented.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != CollisionEffect.Effect35);
    }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, false, spawns);

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1c) return false;
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
