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
        if (effect != 0x08 || !Entity.TakeSwordHit(linkPosition, hook.HitDamage)) return false;
        Entity.ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy);
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
        if (seedItem == 0x24) throw new InvalidOperationException("ENEMY_FIRE_KEESE $39 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & 0x7f, spawns).Effect;
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
            case 0: return new(true, SeedHitResult.None, false);
            case 0x1c: Entity.NotifyOtherCollision(); break;
            case 0x08:
                Entity.TakeSwordHit(origin, -(sbyte)seed.Damage);
                Entity.ApplySwordKnockback(origin, EnemyKnockbackStrength.Low);
                CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); break;
            case 0x28:
                Entity.BeginPegasusHit();
                CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); break;
            case 0x29:
                Entity.ClearSeedStun(); BeginNativeGale(origin, _random); break;
            default: throw new NotSupportedException($"ENEMY_FIRE_KEESE $39 seed collision${collisionType:x2}, effect${effect:x2} is unsupported.");
        }
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect is 0x28 or 0x29);
    }
}

internal sealed record KeeseFireSpawn(Vector2 Position, int ZHigh) : RoomEntitySpawn;
