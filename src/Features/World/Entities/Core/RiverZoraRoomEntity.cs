using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RiverZoraRoomEntity : CombatEnemyRoomEntityAdapter<RiverZoraCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    private readonly Func<Vector2> _cameraOrigin;
    internal RiverZoraRoomEntity(RiverZoraCharacter enemy,
        EnemyCombatSourceDescriptor source, Action<int> soundRequested,
        Func<Vector2> cameraOrigin)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy,
                enemy.Record.DamageQuarters, enemy.TakeSwordHit, enemy.TakeBurnHit,
                enemy.ApplySwordNoKnockback, soundRequested, EnemySwordResponse.NoKnockback))
    {
        _cameraOrigin = cameraOrigin;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(_cameraOrigin(), spawns);

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 sourcePosition,
        int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !hitbox.Intersects(Entity.CollisionBounds)) return SeedHitResult.None;
        if (seedItem == 0x20)
        {
            // Motionless collision mode $0f uses collisionEffect34: create a
            // free flame and kill immediately, not PART_BURNING_ENEMY's stun.
            base.ApplySwordHit(hitbox, sourcePosition, 0x7f,
                EnemyKnockbackStrength.Low, spawns);
            return SeedHitResult.Activate;
        }
        return base.ApplySeedHit(hitbox, sourcePosition, seedItem, spawns);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Hidden;
    }
}
