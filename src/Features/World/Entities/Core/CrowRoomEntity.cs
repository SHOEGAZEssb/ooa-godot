using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CrowRoomEntity
    : CombatEnemyRoomEntityAdapter<CrowCharacter>, IFixedRoomEntity
{
    public CrowRoomEntity(CrowCharacter crow, int killableEnemyIndex = 0)
        : base(
            crow, crow.SetTransitionDrawOffset, CreateCombat(crow),
            (crow.Record.Flags & 0x02) == 0, killableEnemyIndex,
            () => !crow.DeletedOutOfBounds,
            collisionZ: () => crow.Z)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    private static EnemyCombatComponent CreateCombat(CrowCharacter crow) =>
        EnemyCombatComponent.WithContactDamage(
            () => crow.IsDead,
            () => crow.CollisionBounds,
            (_, damage) => crow.TakeSwordHit(damage),
            damage => crow.TakeSwordHit(damage),
            crow.OverlapsLink,
            () => crow.Position,
            crow.Record.DamageQuarters,
            () => crow.IsDead && !crow.DeletedOutOfBounds
                ? new EnemyDeathPuffSpawn(
                    crow.Position + Vector2.Down * crow.Z,
                    EnemyId: crow.Record.Id)
                : null,
            crow.ApplySwordKnockback);
}
