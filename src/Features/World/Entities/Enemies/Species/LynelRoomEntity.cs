using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class LynelRoomEntity(LynelCharacter enemy, EnemyCombatSourceDescriptor source,
    RoomEntityManager owner)
    : CombatEnemyRoomEntityAdapter<LynelCharacter>(enemy, enemy.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
            enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
            owner.OnSoundRequested, EnemySwordResponse.Knockback), collisionZ: () => enemy.ZFixed >> 8),
        IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, ILinkSwordStateAwareRoomEntity, IItemCollisionHittableRoomEntity,
        IExpertPunchHittableRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    private readonly EnemyBehaviorTables _data = EnemyBehaviorTables.Shared;
    private int _swordCollision = ItemCollisionType.L1Sword;
    protected override bool Stunned => Entity.StunCounter != 0;
    public override void HandleLinkContact(Player player)
    {
        if (Entity.NativeHitPending) return;
        float invincibility = player.InvincibilityFrames;
        base.HandleLinkContact(player);
        // collisionEffect02 still writes ENEMYDMG_1c: a body hit consumes
        // the Lynel's next status dispatch without changing its health.
        if (invincibility == 0 && player.InvincibilityFrames > 0) Entity.DeferNativeHitStatus();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        PrepareForScreenTransition(null, spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InitializationPending) Entity.UpdateFrame(player?.EnemyContactPosition ?? Vector2.Zero, null, 0, () => false);
        return ScreenTransitionPresentation.Visible;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.EnemyContactPosition, frame.ScentSeedTarget, frame.Counter,
            () => owner.TryCreatePart(new LynelBeamSpawn(Entity.Position, Entity.Angle, Entity.ZFixed >> 8)));
        if (Entity.BurnKilled && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }
    public void SetLinkSwordState(SwordActionState state, int level, int? itemCollisionType = null) =>
        _swordCollision = itemCollisionType ?? SwordCollision.Type(state, level);
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => Hit(_swordCollision, bounds, origin, damage, spawns);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.BiggoronSword, bounds, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, bounds, origin, damage, spawns);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        Hit(ItemCollisionType.ExpertPunch, bounds, origin, damage, spawns);
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    private bool Hit(int collision, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter != 0 ||
            _data.LynelActiveCollisions[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        int effect = _data.LynelEffects[collision].Value;
        if (effect == CollisionEffect.None) return true;
        if (effect == CollisionEffect.Effect1c) { Entity.DeferNativeHitStatus(); return true; }
        if (effect == CollisionEffect.SwordNoKnockback)
        {
            if (!Entity.TakeDeferredNoKnockbackHit(origin, damage)) return false;
            Entity.DeferNativeHitStatus(); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); return true;
        }
        var strength = effect switch
        {
            CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
            CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
            _ => throw new NotSupportedException($"lynel.s ENEMY$0d: collision${collision:x2}, effect${effect:x2}.")
        };
        bool hit = CombatDescriptor.Combat.ApplyDamageAfterCollision(origin, damage, strength, spawns, CombatDescriptor.CountsAsEnemy);
        if (hit) Entity.DeferNativeHitStatus();
        return hit;
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.Effect1c) return false;
        Entity.DeferNativeHitStatus(); return true;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || !Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter != 0 ||
            _data.LynelActiveCollisions[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return default;
        int effect = _data.LynelEffects[collision].Value;
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: Entity.DeferNativeHitStatus(); break;
            case CollisionEffect.Effect35: Entity.BeginMysteryHit(); break;
            case CollisionEffect.Burn:
                Entity.BeginEmberHit(-(sbyte)seed.Damage);
                if (owner.PartSlotAvailable) spawns.Add(new BurningEnemySpawn(this));
                break;
            case CollisionEffect.SwordNoKnockback: Hit(collision, bounds, origin, -(sbyte)seed.Damage, spawns); break;
            case CollisionEffect.PegasusSeed: Entity.BeginPegasusHit(); CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); break;
            case CollisionEffect.GaleSeed: Entity.ClearStun(); BeginNativeGale(origin, () => owner.Random.Next().Value); break;
            default: throw new NotSupportedException($"ENEMY$0d seed collision${collision:x2}, effect${effect:x2}.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            effect != CollisionEffect.SwordNoKnockback);
    }
    public int BurnTargetId => Entity.Record.Id;
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public override void ReleaseBurn() => Entity.ReleaseBurn();
}
