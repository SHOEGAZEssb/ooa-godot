using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RopeRoomEntity
    : CombatEnemyRoomEntityAdapter<RopeCharacter>, IFixedRoomEntity
{
    public RopeRoomEntity(RopeCharacter rope, int killableEnemyIndex)
        : base(
            rope, rope.SetTransitionDrawOffset,
            EnemyCombatComponent.WithContactDamage(
                () => rope.IsDead,
                () => rope.CollisionBounds,
                rope.TakeSwordHit,
                rope.TakeBurnHit,
                rope.OverlapsLink,
                () => rope.Position,
                rope.Record.DamageQuarters,
                () => rope.IsDead
                    ? new EnemyDeathPuffSpawn(rope.Position, EnemyId: rope.Record.Id)
                    : null),
            countsAsEnemy: true,
            killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);
}
