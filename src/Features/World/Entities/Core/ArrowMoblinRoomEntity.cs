using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArrowMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<ArrowMoblinCharacter>, IFixedRoomEntity
{
    internal ArrowMoblinRoomEntity(
        ArrowMoblinCharacter moblin,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            moblin,
            moblin.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                moblin,
                moblin.Record.DamageQuarters,
                moblin.TakeSwordHit,
                moblin.TakeBurnHit,
                moblin.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(frame.Player.Position);
        if (arrowAngle >= 0)
        {
            spawns.Add(new EnemyArrowSpawn(
                Entity.Position, arrowAngle));
        }
    }
}
