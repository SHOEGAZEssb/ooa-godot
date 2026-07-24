using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ZolRoomEntity
    : CombatEnemyRoomEntityAdapter<ZolCharacter>, IFixedRoomEntity
{
    public ZolRoomEntity(
        ZolCharacter zol,
        int killableEnemyIndex = 0)
        : base(
            zol, zol.SetTransitionDrawOffset, CreateCombat(zol),
            (zol.Record.Flags & 0x02) == 0, killableEnemyIndex,
            () => zol.Record.SubId != 1 || zol.DiedInHazard,
            collisionZ: () => zol.ZFixed >> 8)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        switch (Entity.UpdateFrame(frame.Player.Position))
        {
            case UpdateEvent.BeginSplit:
                spawns.Add(new KillEnemyPuffSpawn(Entity.Position));
                break;
            case UpdateEvent.SpawnGels:
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Right * 4.0f,
                    "SplitGelRight", KillableEnemyIndex));
                spawns.Add(new GelSpawn(
                    Entity.Position + Vector2.Left * 4.0f,
                    "SplitGelLeft", KillableEnemyIndex));
                break;
        }
    }

    private static EnemyCombatComponent CreateCombat(ZolCharacter zol) =>
        EnemyCombatComponent.WithContactDamage(
            () => zol.IsDead,
            () => zol.CollisionBounds,
            (_, damage) => zol.TakeSwordHit(damage),
            zol.TakeBurnHit,
            zol.OverlapsLink,
            () => zol.Position,
            zol.Record.DamageQuarters,
            () => zol.IsDead && !zol.DiedInHazard
                ? new EnemyDeathPuffSpawn(zol.Position, EnemyId: zol.Record.Id)
                : null,
            zol.ApplySwordNoKnockback);
}

internal sealed record KillEnemyPuffSpawn(Vector2 Position) : RoomEntitySpawn;

internal sealed record GelSpawn(
    Vector2 Position,
    string Name = "Gel",
    int KillableEnemyIndex = 0)
    : RoomEntitySpawn;
