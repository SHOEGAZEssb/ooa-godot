using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RopeRoomEntity
    : CombatEnemyRoomEntityAdapter<RopeCharacter>, IFixedRoomEntity
{
    public RopeRoomEntity(
        RopeCharacter rope,
        EnemyCombatSourceDescriptor combatSource)
        : base(
            rope, rope.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                rope,
                rope.Record.DamageQuarters,
                rope.TakeSwordHit,
                rope.TakeBurnHit,
                rope.ApplySwordKnockback,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);
}
