using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SpikedBeetleRoomEntity
    : CombatEnemyRoomEntityAdapter<SpikedBeetleCharacter>,
        IFixedRoomEntity, IShovelHittableRoomEntity,
        IItemCollisionHittableRoomEntity,
        ISwordAttackerKnockbackRoomEntity
{
    private readonly Action<int> _soundRequested;

    public SpikedBeetleRoomEntity(
        SpikedBeetleCharacter beetle,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            beetle,
            beetle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                beetle,
                beetle.Record.DamageQuarters,
                beetle.TakeSwordHit,
                beetle.TakeBurnHit,
                beetle.ApplyAcceptedSwordResponse,
                soundRequested,
                EnemySwordResponse.Armored,
                acceptedHitSound: 0),
            collisionZ: () => beetle.ZHigh)
    {
        _soundRequested = soundRequested;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    public override void HandleLinkContact(Player player)
    {
        if (Entity.ZHigh < -7)
            return;

        if (player.IsUsingShield &&
            CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            if (Entity.TryApplyFlipHit(
                player.ShieldCollisionBounds,
                player.ShieldCollisionBounds.GetCenter(),
                player.Inventory.ShieldLevel))
            {
                _soundRequested(OracleSoundEngine.SndBombLand);
            }
            return;
        }

        base.HandleLinkContact(player);
    }

    public bool ApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition)
    {
        if (Entity.ZHigh < -7 ||
            !Entity.TryApplyFlipHit(
                hitbox,
                sourcePosition,
                shieldLevel: 2))
        {
            return false;
        }

        _soundRequested(OracleSoundEngine.SndBombLand);
        return true;
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool vulnerable = Entity.FlippedCollision;
        bool hit = base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns);
        if (hit)
        {
            if (vulnerable)
            {
                _soundRequested(OracleSoundEngine.SndDamageEnemy);
            }
            else
            {
                // COLLISIONEFFECT_$15-$17 first allocates INTERAC_CLINK at
                // the enemy/item midpoint, then LINKDMG_$10/$14/$18 requests
                // SND_BOMB_LAND while writing recoil to the sword item.
                spawns.Add(new EnemyClinkSpawn(
                    CollisionMidpoint(
                        Entity.Position,
                        hitbox.GetCenter())));
                _soundRequested(OracleSoundEngine.SndBombLand);
            }
        }
        return hit;
    }

    public bool TryGetSwordAttackerKnockback(
        EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response)
    {
        int frames = Entity.FlippedCollision
            ? 0
            : Entity.ArmoredAttackerKnockbackFrames(strength);
        response = new SwordAttackerKnockback(Entity.Position, frames);
        return frames != 0;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InvincibilityCounter != 0 ||
            !CombatDescriptor.Combat.Intersects(hitbox))
        {
            return false;
        }

        if (!Entity.FlippedCollision)
        {
            // Normal mode maps bombs to effect $00 and thrown objects to
            // effect $1c. Effect $20 consumes a sword beam without health,
            // invincibility, recoil, or a clink.
            return collision == RoomEntityItemCollision.SwordBeam;
        }

        return ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            collision == RoomEntityItemCollision.Bomb
                ? EnemyKnockbackStrength.High
                : EnemyKnockbackStrength.Normal,
            spawns);
    }

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled ||
            Entity.InvincibilityCounter != 0 ||
            !CombatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        if (seedItem == 0x24)
            return SeedHitResult.Activate;
        if (seedItem == 0x20)
        {
            return Entity.FlippedCollision
                ? base.ApplySeedHit(
                    hitbox, sourcePosition, seedItem, spawns)
                : SeedHitResult.Consume;
        }
        return SeedHitResult.None;
    }
}
