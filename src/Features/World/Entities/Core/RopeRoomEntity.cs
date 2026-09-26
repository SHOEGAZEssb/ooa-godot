using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RopeRoomEntity
    : CombatEnemyRoomEntityAdapter<RopeCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public RopeRoomEntity(
        RopeCharacter rope,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            rope, rope.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                rope,
                rope.Record.DamageQuarters,
                rope.TakeSwordHit,
                rope.TakeBurnHit,
                rope.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback),
            collisionZ: () => rope.ZFixed >> 8)
    { }

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != CollisionEffect.SwordLowKnockback || !Entity.TakeSwitchHookHit(linkPosition, hook.HitDamage)) return false;
        // enemyStandardUpdate initializes var3e=$01. Effect08 ORs that
        // into item.var2a; it does not clear the item's collision-enable bit.
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.ScentSeedTarget,
            OracleObjectMovement.Shared.RelativeAngle(Vector2.Zero, frame.Player.FacingVector) / 8);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();
}
