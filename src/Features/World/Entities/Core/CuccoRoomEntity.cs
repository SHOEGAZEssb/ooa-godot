using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CuccoRoomEntity(CuccoCharacter cucco)
    : RoomEntityAdapter<CuccoCharacter>(
        cucco, cucco.SetTransitionDrawOffset),
        IFixedRoomEntity, IBraceletInteractableRoomEntity,
        ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        ILinkContactEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IGaleSeedTarget, IRoomEnemyOutcomeSource
{
    private readonly GaleSeedEnemyMotion _gale = new(cucco);
    private bool _outcomeTaken;
    public bool GaleCaught => _gale.Active;
    public bool TryCatchGale(Rect2 hitbox, int seedZ, System.Func<byte> random)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !hitbox.Intersects(Entity.CollisionBounds) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.Z, seedZ, 7)) return false;
        if (Entity.IsGiant)
            return Entity.TakeHit(damage: 1);
        // ENABLE_US_BUGFIXES keeps collision $9e in the ordinary state-$05
        // dispatch instead of sending the caught Cucco through cucco_attacked.
        _gale.Begin(hitbox.GetCenter(), Entity.Z, random);
        return true;
    }
    public void UpdateGale(int cameraY) => _gale.Update(cameraY);
    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        outcome = RoomEnemyOutcome.SilentDeletion(true);
        if (!Finished || !GaleCaught || _outcomeTaken) return false;
        _outcomeTaken = true;
        return true;
    }
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
        if (Entity.IsGiant && player.EnemyContactHeightOverlaps(0) && Entity.OverlapsLink(player.EnemyContactPosition))
        {
            player.ApplyEnemyContactDamage(
                Entity.Position, Entity.Record.DamageQuarters);
        }
    }
}
