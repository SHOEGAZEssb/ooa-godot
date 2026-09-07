using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RopeRoomEntity
    : CombatEnemyRoomEntityAdapter<RopeCharacter>, IFixedRoomEntity
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

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.ScentSeedTarget,
            OracleObjectMovement.Shared.RelativeAngle(Vector2.Zero, frame.Player.FacingVector) / 8);
}
