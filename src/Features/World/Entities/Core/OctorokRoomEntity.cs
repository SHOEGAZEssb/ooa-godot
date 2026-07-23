using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OctorokRoomEntity
    : CombatEnemyRoomEntityAdapter<OctorokCharacter>, IFixedRoomEntity
{
    public OctorokRoomEntity(
        OctorokCharacter octorok,
        Action<int> soundRequested,
        int killableEnemyIndex = 0)
        : base(
            octorok, octorok.SetTransitionDrawOffset, CreateCombat(octorok),
            (octorok.Record.Flags & 0x02) == 0, killableEnemyIndex,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                octorok.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position))
            spawns.Add(new OctorokRockSpawn(Entity.Position, Entity.Angle));
    }

    private static EnemyCombatComponent CreateCombat(OctorokCharacter octorok) =>
        EnemyCombatComponent.WithContactDamage(
            () => octorok.IsDead,
            () => octorok.CollisionBounds,
            octorok.TakeSwordHit,
            octorok.TakeBurnHit,
            octorok.OverlapsLink,
            () => octorok.Position,
            octorok.Record.DamageQuarters,
            () => octorok.IsDead && !octorok.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    octorok.Position, EnemyId: octorok.Record.Id)
                : null);
}

internal sealed record OctorokRockSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
