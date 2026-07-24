using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class StalfosRoomEntity
    : CombatEnemyRoomEntityAdapter<StalfosCharacter>, IFixedRoomEntity
{
    public StalfosRoomEntity(
        StalfosCharacter stalfos,
        int killableEnemyIndex = 0)
        : base(
            stalfos, stalfos.SetTransitionDrawOffset, CreateCombat(stalfos),
            (stalfos.Record.Flags & 0x02) == 0, killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    private static EnemyCombatComponent CreateCombat(StalfosCharacter stalfos) =>
        EnemyCombatComponent.WithContactDamage(
            () => stalfos.IsDead,
            () => stalfos.CollisionBounds,
            stalfos.TakeSwordHit,
            damage => stalfos.TakeSwordHit(Vector2.Zero, damage),
            stalfos.OverlapsLink,
            () => stalfos.Position,
            stalfos.Record.DamageQuarters,
            () => stalfos.IsDead && !stalfos.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    stalfos.Position, EnemyId: stalfos.Record.Id)
                : null,
            stalfos.ApplySwordKnockback);
}
