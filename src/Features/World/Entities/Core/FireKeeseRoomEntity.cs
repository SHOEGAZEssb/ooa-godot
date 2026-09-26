using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FireKeeseRoomEntity : CombatEnemyRoomEntityAdapter<FireKeeseCharacter>, IFixedRoomEntity, ISeedCollisionTarget,
    IScreenTransitionPreloadRoomEntity
{
    private readonly Func<bool> _canSpawnPart;
    private readonly Func<byte> _random;
    internal FireKeeseRoomEntity(FireKeeseCharacter keese, EnemyCombatSourceDescriptor source, Action<int> sound, Func<bool> canSpawnPart, Func<byte> random)
        : base(keese, keese.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, keese, keese.Record.DamageQuarters,
                keese.TakeSwordHit, _ => false, keese.ApplySwordKnockback, sound, EnemySwordResponse.Knockback),
            collisionZ: () => keese.ZFixed >> 8)
    { _canSpawnPart = canSpawnPart; _random = random; }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwordHit(linkPosition, hook.HitDamage)) return false;
        Entity.ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position, frame.Counter) && _canSpawnPart())
            spawns.Add(new KeeseFireSpawn(OracleObjectMath.ToPixelPosition(Entity.Position), Entity.ZFixed >> 8));
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // bank0._updateEnemiesIfStateIsZero still dispatches enemyCode39 while
        // scrolling. Resolve zh=-$1c, animation $01 and both state-0 RNG calls
        // before drawing the incoming bat, then freeze state $0b until entry.
        if (Entity.State == 0) Entity.UpdateFrame(Vector2.Zero);
        return ScreenTransitionPresentation.Visible;
    }
    public override void HandleLinkContact(Player player)
    {
        if (GaleCaught || Entity.HasPendingHit || !Entity.CollisionEnabled ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        if (player.IsUsingShield && CombatDescriptor.Source!.Value.ShieldBumpResponse(player.Inventory.ShieldLevel) is not null &&
            CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            base.HandleLinkContact(player);
            return;
        }
        if (Entity.StunCounter == 0 && player.OverlapsEnemyCollision(Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position, Entity.DamageQuarters))
            Entity.NotifyLinkCollision();
    }
    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY_FIRE_KEESE $39 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || Entity.HasPendingHit || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            EnemyBehaviorTables.Shared.FireKeeseActiveCollisions[collisionType].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox)) return default;
        int effect = EnemyBehaviorTables.Shared.FireKeeseCollisionEffects[collisionType].Value;
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect1c: Entity.NotifyOtherCollision(); break;
            case CollisionEffect.SwordLowKnockback:
                Entity.TakeSwordHit(origin, -(sbyte)seed.Damage);
                Entity.ApplySwordKnockback(origin, EnemyKnockbackStrength.Low);
                CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); break;
            case CollisionEffect.PegasusSeed:
                Entity.BeginPegasusHit();
                CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); break;
            case CollisionEffect.GaleSeed:
                Entity.ClearSeedStun(); BeginNativeGale(origin, _random); break;
            default: throw new NotSupportedException($"ENEMY_FIRE_KEESE $39 seed collision${collisionType:x2}, effect${effect:x2} is unsupported.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect is CollisionEffect.PegasusSeed or CollisionEffect.GaleSeed);
    }
}

internal sealed record KeeseFireSpawn(Vector2 Position, int ZHigh) : RoomEntitySpawn;
