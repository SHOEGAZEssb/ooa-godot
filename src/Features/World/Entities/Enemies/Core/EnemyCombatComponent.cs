using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// Shared room-adapter plumbing for enemy contact, sword/burn hits, lifetime,
/// and optional death puffs. Species-specific outcomes are supplied once as
/// policy.
/// </summary>
internal sealed class EnemyCombatComponent(
    Func<bool> isDead,
    Func<Rect2> collisionBounds,
    Func<Vector2, int, bool> takeSwordHit,
    Func<int, bool> takeBurnHit,
    Action<Player> handleLinkContact,
    Func<EnemyDeathPuffSpawn?> createDeathPuff,
    Action<Vector2, EnemyKnockbackStrength>? acceptedSwordHit = null)
{
    public static EnemyCombatComponent WithContactDamage(
        Func<bool> isDead,
        Func<Rect2> collisionBounds,
        Func<Vector2, int, bool> takeSwordHit,
        Func<int, bool> takeBurnHit,
        Func<Vector2, bool> overlapsLink,
        Func<Vector2> contactOrigin,
        int damageQuarters,
        Func<EnemyDeathPuffSpawn?> createDeathPuff,
        Action<Vector2, EnemyKnockbackStrength>? acceptedSwordHit = null)
    {
        return new EnemyCombatComponent(
            isDead,
            collisionBounds,
            takeSwordHit,
            takeBurnHit,
            player =>
            {
                if (overlapsLink(player.EnemyContactPosition))
                    player.ApplyEnemyContactDamage(contactOrigin(), damageQuarters);
            },
            createDeathPuff,
            acceptedSwordHit);
    }

    public bool Finished => isDead();
    public bool Intersects(Rect2 hitbox) =>
        !isDead() && hitbox.Intersects(collisionBounds());

    public void HandleLinkContact(Player player) => handleLinkContact(player);

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns,
        bool deathPuffDecrementsRoomCount)
    {
        if (!Intersects(hitbox))
            return false;
        return ApplyDamageAfterCollision(sourcePosition, damage, knockbackStrength, spawns, deathPuffDecrementsRoomCount);
    }

    internal bool ApplyDamageAfterCollision(Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength knockbackStrength, ICollection<RoomEntitySpawn> spawns,
        bool deathPuffDecrementsRoomCount)
    {
        bool struck = takeSwordHit(sourcePosition, damage);
        if (struck)
            acceptedSwordHit?.Invoke(sourcePosition, knockbackStrength);
        if (struck && CreateDeathPuff() is { } deathPuff)
        {
            spawns.Add(deathPuff with
            {
                DecrementsRoomCount = deathPuffDecrementsRoomCount
            });
        }
        return struck;
    }

    public void ApplyBurnHit(
        int damage,
        ICollection<RoomEntitySpawn> spawns,
        bool deathPuffDecrementsRoomCount)
    {
        if (isDead() || !takeBurnHit(damage))
            return;
        if (CreateDeathPuff() is { } deathPuff)
        {
            spawns.Add(deathPuff with
            {
                DecrementsRoomCount = deathPuffDecrementsRoomCount
            });
        }
    }

    public EnemyDeathPuffSpawn? CreateDeathPuff() => createDeathPuff();
}

internal sealed class EnemyCombatDescriptor
{
    private readonly Func<RoomEnemyOutcome>? _completedOutcome;
    private readonly Action<int>? _soundRequested;

    private EnemyCombatDescriptor(
        EnemyCombatComponent combat,
        bool countsAsEnemy,
        int killableEnemyIndex,
        Func<RoomEnemyOutcome>? completedOutcome,
        EnemyCombatSourceDescriptor? source,
        Action<int>? soundRequested)
    {
        Combat = combat;
        CountsAsEnemy = countsAsEnemy;
        KillableEnemyIndex = killableEnemyIndex;
        _completedOutcome = completedOutcome;
        Source = source;
        _soundRequested = soundRequested;
    }

    internal EnemyCombatComponent Combat { get; }
    internal bool CountsAsEnemy { get; }
    internal int KillableEnemyIndex { get; }
    internal EnemyCombatSourceDescriptor? Source { get; }

    internal static EnemyCombatDescriptor WithContactDamage(
        EnemyCombatSourceDescriptor source,
        EnemyCharacter enemy,
        int damageQuarters,
        Func<Vector2, int, bool> takeSwordHit,
        Func<int, bool> takeBurnHit,
        Action<Vector2, EnemyKnockbackStrength> acceptedSwordHit,
        Action<int> soundRequested,
        Func<Vector2>? deathPuffPosition = null,
        Func<bool>? deathPuffAllowed = null,
        Func<RoomEnemyOutcome>? completedOutcome = null,
        int acceptedHitSound = SoundId.SndDamageEnemy,
        bool dropsItem = true,
        Func<int>? deathPuffZHigh = null)
    {
        if (source.AssetRoot != enemy.AssetRoot)
            throw new InvalidOperationException($"{source.Source}: ENEMY ${source.Id:x2}:${source.SubId:x2} combat pack '{source.AssetRoot}' does not match actor pack '{enemy.AssetRoot}'.");
        var combat = EnemyCombatComponent.WithContactDamage(
            () => enemy.IsDead,
            () => enemy.CollisionBounds,
            takeSwordHit,
            takeBurnHit,
            enemy.OverlapsLink,
            () => enemy.Position,
            damageQuarters,
            () =>
                enemy.IsDead &&
                !enemy.DiedInHazard &&
                (deathPuffAllowed?.Invoke() ?? true)
                    ? new EnemyDeathPuffSpawn(
                        deathPuffPosition?.Invoke() ?? enemy.Position,
                        HighKnockback: (enemy.KnockbackCounter & 0x80) != 0,
                        EnemyId: source.Id,
                        DropsItem: dropsItem,
                        ZHigh: deathPuffZHigh?.Invoke() ?? 0)
                    : null,
            (sourcePosition, strength) =>
            {
                acceptedSwordHit(sourcePosition, strength);
                if (acceptedHitSound != 0)
                    soundRequested(acceptedHitSound);
            });
        return FromSource(
            source, combat, completedOutcome, soundRequested);
    }

    internal static EnemyCombatDescriptor FromSource(
        EnemyCombatSourceDescriptor source,
        EnemyCombatComponent combat,
        Func<RoomEnemyOutcome>? completedOutcome = null,
        Action<int>? soundRequested = null)
    {
        source.ValidateNativeCollisionData();
        return new EnemyCombatDescriptor(
            combat,
            source.CountsAsEnemy,
            source.KillableEnemyIndex,
            completedOutcome,
            source,
            soundRequested);
    }

    internal static EnemyCombatDescriptor Special(
        EnemyCombatComponent combat,
        bool countsAsEnemy,
        int killableEnemyIndex,
        Func<RoomEnemyOutcome>? completedOutcome = null,
        Action<int>? soundRequested = null,
        EnemyCombatSourceDescriptor? source = null) =>
        new(
            combat,
            countsAsEnemy,
            killableEnemyIndex,
            completedOutcome,
            source,
            soundRequested);

    internal void RequestSound(int sound) =>
        _soundRequested?.Invoke(sound);

    internal RoomEnemyOutcome CompletedOutcome(EnemyCharacter enemy) =>
        _completedOutcome?.Invoke() ??
        (enemy.DiedInHazard
            ? RoomEnemyOutcome.HazardDeletion(CountsAsEnemy)
            : RoomEnemyOutcome.EnemyDie(KillableEnemyIndex));
}

internal readonly record struct EnemyCombatSourceDescriptor(
    string AssetRoot,
    int Id,
    int SubId,
    int CollisionMode,
    int ObjectFlags,
    int KillableEnemyIndex,
    string Source)
{
    internal bool CountsAsEnemy => (ObjectFlags & 0x02) == 0;
    internal bool CollisionInitiallyEnabled => (CollisionMode & 0x80) != 0;

    internal EnemyShieldBumpResponse? ShieldBumpResponse(int shieldLevel, int? liveMode = null, int? liveType = null)
    {
        if (shieldLevel is < 1 or > 3) return null;
        var data = ObjectCollisionDatabase.For(AssetRoot);
        if (!data.EnemyEnabled(liveType ?? Id,shieldLevel)) return null;
        int effect = data.Effect(liveMode ?? CollisionMode,shieldLevel);
        return effect switch
        {
            // collisionEffect0f applies LINKDMG_$10 / ENEMYDMG_$10.
            0x0f => new EnemyShieldBumpResponse(
                EnemyKnockbackStrength.Low,
                LinkInvincibilityFrames: 0x08,
                LinkKnockbackFrames: 0x0b),
            // collisionEffect10 applies LINKDMG_$14 / ENEMYDMG_$14.
            0x10 => new EnemyShieldBumpResponse(
                EnemyKnockbackStrength.Normal,
                LinkInvincibilityFrames: 0x0f,
                LinkKnockbackFrames: 0x13),
            // collisionEffect11 applies LINKDMG_$18 / ENEMYDMG_$18.
            0x11 => new EnemyShieldBumpResponse(
                EnemyKnockbackStrength.High,
                LinkInvincibilityFrames: 0x16,
                LinkKnockbackFrames: 0x19),
            _ => null
        };
    }

    internal void ValidateNativeCollisionData()
    {
        try { _ = ObjectCollisionDatabase.For(AssetRoot).Effect(CollisionMode,ItemCollisionType.L1Sword); }
        catch (NotSupportedException error)
        {
            throw new InvalidOperationException($"{Source}: ENEMY ${Id:x2}:${SubId:x2} has no native combat row: {error.Message}",error);
        }
    }
}

internal readonly record struct EnemyShieldBumpResponse(
    EnemyKnockbackStrength EnemyStrength,
    int LinkInvincibilityFrames,
    int LinkKnockbackFrames);
