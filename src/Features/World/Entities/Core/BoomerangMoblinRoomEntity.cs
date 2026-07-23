using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BoomerangMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<BoomerangMoblinCharacter>, IFixedRoomEntity
{
    public BoomerangMoblinRoomEntity(
        BoomerangMoblinCharacter moblin,
        int killableEnemyIndex)
        : base(
            moblin, moblin.SetTransitionDrawOffset,
            EnemyCombatComponent.WithContactDamage(
                () => moblin.IsDead,
                () => moblin.CollisionBounds,
                moblin.TakeSwordHit,
                moblin.TakeBurnHit,
                moblin.OverlapsLink,
                () => moblin.Position,
                moblin.Record.DamageQuarters,
                () => moblin.IsDead
                    ? new EnemyDeathPuffSpawn(moblin.Position, EnemyId: moblin.Record.Id)
                    : null),
            countsAsEnemy: true,
            killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int angle = Entity.UpdateFrame(frame.Player.Position);
        if (angle >= 0)
            spawns.Add(new MoblinBoomerangSpawn(Entity, Entity.Position, angle));
    }
}
