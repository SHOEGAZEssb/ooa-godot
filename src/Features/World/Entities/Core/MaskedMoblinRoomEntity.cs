using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MaskedMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<MaskedMoblinCharacter>, IFixedRoomEntity
{
    public MaskedMoblinRoomEntity(
        MaskedMoblinCharacter moblin,
        Action<int> soundRequested)
        : base(
            moblin, moblin.SetTransitionDrawOffset, CreateCombat(moblin),
            countsAsEnemy: true, killableEnemyIndex: 0,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                moblin.DeathHazard, soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(frame.Player.Position);
        if (arrowAngle >= 0)
            spawns.Add(new EnemyArrowSpawn(Entity.Position, arrowAngle));
    }

    private static EnemyCombatComponent CreateCombat(MaskedMoblinCharacter moblin) =>
        EnemyCombatComponent.WithContactDamage(
            () => moblin.IsDead,
            () => moblin.CollisionBounds,
            moblin.TakeSwordHit,
            damage => moblin.TakeSwordHit(Vector2.Zero, damage),
            moblin.OverlapsLink,
            () => moblin.Position,
            moblin.Record.DamageQuarters,
            () => moblin.IsDead && !moblin.DiedInHazard
                ? new EnemyDeathPuffSpawn(
                    moblin.Position, EnemyId: moblin.Record.Id)
                : null);
}

internal sealed record EnemyArrowSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
