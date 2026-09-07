using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BuzzBlobRoomEntity : CombatEnemyRoomEntityAdapter<BuzzBlobCharacter>,
    IFixedRoomEntity, IItemCollisionHittableRoomEntity, IPlayerInteractable,
    IScreenTransitionPreloadRoomEntity
{
    private readonly Action<int, string, Vector2> _showText;
    private bool _shockLink;
    private bool _talkPending;
    internal BuzzBlobRoomEntity(BuzzBlobCharacter enemy,
        EnemyCombatSourceDescriptor source, Action<int> soundRequested,
        Action<int, string, Vector2> showText)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy,
                enemy.Record.DamageQuarters, enemy.TakeSwordHit, enemy.TakeBurnHit,
                enemy.ApplySwordNoKnockback, soundRequested, EnemySwordResponse.ElectricShock))
    {
        _showText = showText;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_talkPending)
        {
            _talkPending = false;
            _showText(Entity.ChooseText(), "Cukeman", Entity.Position);
        }
        if (_shockLink)
        {
            _shockLink = false;
            frame.Player.ApplyElectricShock(Entity.Position);
        }
        Entity.UpdateFrame(frame.ScentSeedTarget);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }

    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition,
        int damage, EnemyKnockbackStrength knockbackStrength, ICollection<RoomEntitySpawn> spawns)
    {
        if (SeedBurning || !hitbox.Intersects(Entity.CollisionBounds) || !Entity.BeginShock())
            return false;
        _shockLink = true;
        return true;
    }

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        // ENEMYCOLLISION_BUZZBLOB $1b: beams/punches shock ($36),
        // thrown objects and bombs apply damage without recoil ($0b).
        if (collision is RoomEntityItemCollision.SwordBeam or RoomEntityItemCollision.ExpertPunch)
            return ApplySwordHit(hitbox, sourcePosition, damage, EnemyKnockbackStrength.None, spawns);
        if (collision is not (RoomEntityItemCollision.ThrownObject or RoomEntityItemCollision.Bomb))
            throw new ArgumentOutOfRangeException(nameof(collision));
        return base.ApplySwordHit(hitbox, sourcePosition, damage, EnemyKnockbackStrength.Low, spawns);
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition,
        int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (SeedBurning || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !hitbox.Intersects(Entity.CollisionBounds)) return SeedHitResult.None;
        if (seedItem == 0x24)
        {
            Entity.BecomeCukeman();
            return SeedHitResult.Activate;
        }
        return base.ApplySeedHit(hitbox, sourcePosition, seedItem, spawns);
    }

    public bool TryInteract(Player player)
    {
        if (!Entity.IsCukeman || Entity.IsDead) return false;
        Vector2 point = player.Position.Floor() + (Vector2)player.FacingVector * 10;
        Vector2 difference = point - Entity.Position;
        if (Mathf.Abs(difference.X) >= Entity.Record.RadiusX ||
            Mathf.Abs(difference.Y) >= Entity.Record.RadiusY) return false;
        _talkPending = true;
        player.ApplyObjectInteractionGrace();
        return true;
    }
}
