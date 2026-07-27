using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CrowRoomEntity
    : CombatEnemyRoomEntityAdapter<CrowCharacter>, IFixedRoomEntity
{
    public CrowRoomEntity(
        CrowCharacter crow,
        EnemyCombatSourceDescriptor combatSource)
        : base(
            crow,
            crow.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                crow,
                crow.Record.DamageQuarters,
                (_, damage) => crow.TakeSwordHit(damage),
                damage => crow.TakeSwordHit(damage),
                crow.ApplySwordKnockback,
                EnemySwordResponse.Knockback,
                deathPuffPosition: () =>
                    crow.Position + Vector2.Down * crow.Z,
                deathPuffAllowed: () => !crow.DeletedOutOfBounds,
                completedOutcome: () => crow.DeletedOutOfBounds
                    ? RoomEnemyOutcome.SilentDeletion(
                        decrementsRoomCount: false)
                    : RoomEnemyOutcome.EnemyDie(
                        combatSource.KillableEnemyIndex)),
            collisionZ: () => crow.Z)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

}
