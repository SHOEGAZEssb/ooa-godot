using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

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
                if (overlapsLink(player.Position))
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
        EnemySwordResponse swordResponse,
        Func<Vector2>? deathPuffPosition = null,
        Func<bool>? deathPuffAllowed = null,
        Func<RoomEnemyOutcome>? completedOutcome = null,
        int acceptedHitSound = OracleSoundEngine.SndDamageEnemy)
    {
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
                        EnemyId: source.Id)
                    : null,
            (sourcePosition, strength) =>
            {
                acceptedSwordHit(sourcePosition, strength);
                if (acceptedHitSound != 0)
                    soundRequested(acceptedHitSound);
            });
        return FromSource(
            source, combat, swordResponse, completedOutcome, soundRequested);
    }

    internal static EnemyCombatDescriptor FromSource(
        EnemyCombatSourceDescriptor source,
        EnemyCombatComponent combat,
        EnemySwordResponse swordResponse,
        Func<RoomEnemyOutcome>? completedOutcome = null,
        Action<int>? soundRequested = null)
    {
        source.ValidateSwordResponse(swordResponse);
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
        Func<RoomEnemyOutcome>? completedOutcome = null) =>
        new(
            combat,
            countsAsEnemy,
            killableEnemyIndex,
            completedOutcome,
            source: null,
            soundRequested: null);

    internal void RequestSound(int sound) =>
        _soundRequested?.Invoke(sound);

    internal RoomEnemyOutcome CompletedOutcome(EnemyCharacter enemy) =>
        _completedOutcome?.Invoke() ??
        (enemy.DiedInHazard
            ? RoomEnemyOutcome.HazardDeletion(CountsAsEnemy)
            : RoomEnemyOutcome.EnemyDie(KillableEnemyIndex));
}

internal readonly record struct EnemyCombatSourceDescriptor(
    int Id,
    int SubId,
    int CollisionMode,
    int ObjectFlags,
    int KillableEnemyIndex,
    EnemyHandlerKind Handler,
    string Source,
    int ShieldLevel1Effect = 0,
    int ShieldLevel2Effect = 0,
    int ShieldLevel3Effect = 0,
    string ShieldSource = "unavailable")
{
    internal bool CountsAsEnemy => (ObjectFlags & 0x02) == 0;
    internal bool CollisionInitiallyEnabled => (CollisionMode & 0x80) != 0;

    internal EnemyShieldBumpResponse? ShieldBumpResponse(int shieldLevel)
    {
        int effect = shieldLevel switch
        {
            1 => ShieldLevel1Effect,
            2 => ShieldLevel2Effect,
            3 => ShieldLevel3Effect,
            _ => 0
        };
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
            _ => null
        };
    }

    internal void ValidateSwordResponse(EnemySwordResponse response)
    {
        int collisionMode = CollisionMode & 0x7f;
        EnemySwordResponse expected =
            (Handler, collisionMode) switch
        {
            (EnemyHandlerKind.Thwomp, 0x28) =>
                EnemySwordResponse.Armored,
            (_, 0x10 or 0x11 or 0x14 or 0x1a or 0x1f or 0x25 or 0x31 or 0x3a or 0x7d) =>
                EnemySwordResponse.Knockback,
            (_, 0x18) => EnemySwordResponse.Armored,
            (_, 0x17 or 0x1c or 0x28 or 0x29 or 0x33 or 0x58 or 0x6e) =>
                EnemySwordResponse.NoKnockback,
            (_, 0x36 or 0x7e) => EnemySwordResponse.Knockback,
            (_, 0x21 or 0x38) => EnemySwordResponse.Bump,
            _ => throw new InvalidOperationException(
                $"{Source} resolves {Handler} ${Id:x2}:${SubId:x2} to " +
                $"unsupported enemy collision mode ${CollisionMode:x2}.")
        };
        if (response != expected)
        {
            throw new InvalidOperationException(
                $"{Source} resolves {Handler} ${Id:x2}:${SubId:x2} through " +
                $"enemy collision mode ${CollisionMode:x2}, which requires " +
                $"{expected} instead of {response}.");
        }
    }
}

internal readonly record struct EnemyShieldBumpResponse(
    EnemyKnockbackStrength EnemyStrength,
    int LinkInvincibilityFrames,
    int LinkKnockbackFrames);

internal enum EnemySwordResponse
{
    Knockback,
    Armored,
    NoKnockback,
    Bump
}
