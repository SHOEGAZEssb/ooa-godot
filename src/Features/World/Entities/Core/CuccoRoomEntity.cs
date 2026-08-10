using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CuccoRoomEntity(CuccoCharacter cucco)
    : RoomEntityAdapter<CuccoCharacter>(
        cucco, cucco.SetTransitionDrawOffset),
        IFixedRoomEntity, IBraceletInteractableRoomEntity,
        ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        ILinkContactEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => !Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns);

    public bool TryUseBracelet(Player player, Vector2I releaseDirection) =>
        Entity.TryUseBracelet(player, releaseDirection);

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = sourcePosition;
        _ = damage;
        _ = knockbackStrength;
        _ = spawns;
        return Entity.CollisionEnabled &&
            hitbox.Intersects(Entity.CollisionBounds) &&
            Entity.TakeHit();
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = sourcePosition;
        _ = spawns;
        if (!Entity.CollisionEnabled ||
            !hitbox.Intersects(Entity.CollisionBounds))
        {
            return SeedHitResult.None;
        }

        if (seedItem == OwlStatueDatabase.MysterySeedItem && !Entity.IsGiant)
        {
            Entity.BeginMysterySeedTransformation(spawns);
            return SeedHitResult.Consume;
        }
        if (seedItem == 0x21)
            return Entity.TakeHit()
                ? SeedHitResult.Activate
                : SeedHitResult.None;
        return Entity.TakeHit() ? SeedHitResult.Consume : SeedHitResult.None;
    }

    public void HandleLinkContact(Player player)
    {
        if (Entity.IsGiant && Entity.OverlapsLink(player.Position))
        {
            player.ApplyEnemyContactDamage(
                Entity.Position, Entity.Record.DamageQuarters);
        }
    }
}
