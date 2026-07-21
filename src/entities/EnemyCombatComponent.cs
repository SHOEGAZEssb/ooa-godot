using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared room-adapter plumbing for enemy contact, sword hits, lifetime, and
/// optional death puffs. Species-specific outcomes are supplied once as policy.
/// </summary>
internal sealed class EnemyCombatComponent(
    Func<bool> isDead,
    Func<Rect2> collisionBounds,
    Func<Vector2, int, bool> takeSwordHit,
    Action<Player> handleLinkContact,
    Func<EnemyDeathPuffSpawn?> createDeathPuff)
{
    public static EnemyCombatComponent WithContactDamage(
        Func<bool> isDead,
        Func<Rect2> collisionBounds,
        Func<Vector2, int, bool> takeSwordHit,
        Func<Vector2, bool> overlapsLink,
        Func<Vector2> contactOrigin,
        int damageQuarters,
        Func<EnemyDeathPuffSpawn?> createDeathPuff)
    {
        return new EnemyCombatComponent(
            isDead,
            collisionBounds,
            takeSwordHit,
            player =>
            {
                if (overlapsLink(player.Position))
                    player.ApplyEnemyContactDamage(contactOrigin(), damageQuarters);
            },
            createDeathPuff);
    }

    public bool Finished => isDead();

    public void HandleLinkContact(Player player) => handleLinkContact(player);

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (isDead() || !hitbox.Intersects(collisionBounds()))
            return false;
        bool struck = takeSwordHit(sourcePosition, damage);
        if (struck && createDeathPuff() is { } deathPuff)
            spawns.Add(deathPuff);
        return struck;
    }
}
