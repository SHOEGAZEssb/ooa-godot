using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ShadowHagBugRoomEntity
    : CombatEnemyRoomEntityAdapter<ShadowHagBug>, IFixedRoomEntity
{
    internal ShadowHagBugRoomEntity(
        ShadowHagBug bug,
        Action<int> soundRequested)
        : base(
            bug,
            bug.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => bug.IsDead,
                    () => bug.CollisionBounds,
                    bug.TakeSwordHit,
                    bug.TakeBurnHit,
                    bug.OverlapsLink,
                    () => bug.Position,
                    bug.Record.DamageQuarters,
                    () => bug.DeathPuff
                        ? new EnemyDeathPuffSpawn(
                            bug.Position,
                            EnemyId: bug.Record.Id)
                        : null,
                    (source, strength) =>
                    {
                        _ = source;
                        _ = strength;
                        soundRequested(OracleSoundEngine.SndDamageEnemy);
                    }),
                countsAsEnemy: false,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.SilentDeletion(
                        decrementsRoomCount: false)))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns);
}

internal sealed record ShadowHagBugSpawn(
    ShadowHagBoss Owner,
    Vector2 Position) : RoomEntitySpawn(UpdateThisFrame: true);
