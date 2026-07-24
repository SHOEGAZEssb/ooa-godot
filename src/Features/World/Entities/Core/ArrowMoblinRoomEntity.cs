using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArrowMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<ArrowMoblinCharacter>, IFixedRoomEntity
{
    internal ArrowMoblinRoomEntity(
        ArrowMoblinCharacter moblin,
        int killableEnemyIndex)
        : base(
            moblin,
            moblin.SetTransitionDrawOffset,
            EnemyCombatComponent.WithContactDamage(
                () => moblin.IsDead,
                () => moblin.CollisionBounds,
                moblin.TakeSwordHit,
                moblin.TakeBurnHit,
                moblin.OverlapsLink,
                () => moblin.Position,
                moblin.Record.DamageQuarters,
                () => moblin.IsDead && !moblin.DiedInHazard
                    ? new EnemyDeathPuffSpawn(
                        moblin.Position, EnemyId: moblin.Record.Id)
                    : null,
                moblin.ApplySwordKnockback),
            countsAsEnemy: true,
            killableEnemyIndex)
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
