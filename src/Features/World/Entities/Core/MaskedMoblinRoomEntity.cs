using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MaskedMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<MaskedMoblinCharacter>, IFixedRoomEntity
{
    public MaskedMoblinRoomEntity(
        MaskedMoblinCharacter moblin,
        EnemyCombatSourceDescriptor combatSource)
        : base(
            moblin,
            moblin.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                moblin,
                moblin.Record.DamageQuarters,
                moblin.TakeSwordHit,
                damage => moblin.TakeSwordHit(Vector2.Zero, damage),
                moblin.ApplySwordKnockback,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(frame.Player.Position);
        if (arrowAngle >= 0)
            spawns.Add(new EnemyArrowSpawn(Entity.Position, arrowAngle));
    }
}

internal sealed record EnemyArrowSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
