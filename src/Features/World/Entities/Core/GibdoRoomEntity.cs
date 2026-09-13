using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GibdoRoomEntity : CombatEnemyRoomEntityAdapter<GibdoCharacter>, IFixedRoomEntity,
    IRoomEnemyReplacementSource, IBurningEnemyTarget, ISeedCollisionTarget
{
    private readonly RoomObjectRecord _source;
    private readonly Func<bool> _canSpawnPart;
    private readonly Func<byte> _random;
    private bool _replacementPending;
    internal GibdoRoomEntity(GibdoCharacter gibdo, RoomObjectRecord source,
        EnemyCombatSourceDescriptor combat, Action<int> sound, Func<bool> canSpawnPart, Func<byte> random)
        : base(gibdo, gibdo.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(combat, gibdo, gibdo.Record.DamageQuarters,
                gibdo.TakeSwordHit, _ => false, gibdo.ApplySwordNoKnockback, sound,
                EnemySwordResponse.NoKnockback), collisionZ: () => gibdo.ZFixed >> 8)
    { _source = source; _canSpawnPart = canSpawnPart; _random = random; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _replacementPending = Entity.UpdateFrame(frame.Counter);
        if (Entity.IsDead && !Entity.DiedInHazard && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }

    public bool TryTakeReplacement(out RoomEnemyReplacement replacement)
    {
        replacement = default;
        if (!_replacementPending) return false;
        _replacementPending = false;
        Entity.Replaced = true;
        var behavior = EnemyBehaviorTables.Shared.Gibdo;
        replacement = new(_source with {
            Id = behavior[5].Value, SubId = behavior[6].Value,
            X = (int)Entity.Position.X, Y = (int)Entity.Position.Y,
            SourceOverride = _source.Source + " via gibdo.s:@stateA/enemyReplaceWithID"
        }, KillableEnemyIndex, Entity.ZFixed >> 8);
        return true;
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == 0x24) throw new InvalidOperationException("ENEMY_GIBDO $12 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(hitbox, origin, seed, seed.Collision & 0x7f, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox, Vector2 origin, SeedRecord seed,
        int collisionType, ICollection<RoomEntitySpawn> spawns)
    {
        if (GaleCaught || Entity.HasPendingHit || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            EnemyBehaviorTables.Shared.GibdoActiveCollisions[collisionType].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hitbox)) return default;
        int effect = EnemyBehaviorTables.Shared.GibdoCollisionEffects[collisionType].Value;
        switch (effect)
        {
            case 0: return new(true, SeedHitResult.None, false);
            case 0x0b:
                Entity.BeginScentHit(origin, -(sbyte)seed.Damage);
                CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); break;
            case 0x27:
                Entity.BeginEmberHit();
                if (_canSpawnPart()) spawns.Add(new BurningEnemySpawn(this));
                break;
            case 0x28:
                Entity.BeginPegasusHit();
                CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy); break;
            case 0x29:
                Entity.ClearSeedStun(); BeginNativeGale(origin, _random); break;
            default: throw new NotSupportedException($"ENEMY_GIBDO $12 seed collision${collisionType:x2}, effect${effect:x2} is unsupported.");
        }
        return new(true, seed.SeedItem == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, effect != 0x0b);
    }
    public override void HandleLinkContact(Player player)
    {
        if (Entity.HasPendingHit || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        bool shield = player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds);
        if (Entity.StunCounter != 0 && !shield) return;
        base.HandleLinkContact(player);
    }
    public bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead && !Entity.Replaced;
    public int BurnTargetId => Entity.Record.Id;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public int BurnZFixed { get => Entity.ZFixed; set => Entity.ZFixed = value; }
    public void ReleaseBurn() => Entity.InvincibilityCounter = 0;
}
