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
    Action? acceptedSwordHit = null)
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
        Action? acceptedSwordHit = null)
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
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Intersects(hitbox))
            return false;
        bool struck = takeSwordHit(sourcePosition, damage);
        if (struck)
            acceptedSwordHit?.Invoke();
        if (struck && createDeathPuff() is { } deathPuff)
            spawns.Add(deathPuff);
        return struck;
    }

    public void ApplyBurnHit(
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (isDead() || !takeBurnHit(damage))
            return;
        if (createDeathPuff() is { } deathPuff)
            spawns.Add(deathPuff);
    }
}
