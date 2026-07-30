using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ZolRoomEntity
    : CombatEnemyRoomEntityAdapter<ZolCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public ZolRoomEntity(
        ZolCharacter zol,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            zol,
            zol.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                zol,
                zol.Record.DamageQuarters,
                (_, damage) => zol.TakeSwordHit(damage),
                zol.TakeBurnHit,
                zol.ApplySwordNoKnockback,
                soundRequested,
                EnemySwordResponse.NoKnockback,
                completedOutcome: () => zol.DiedInHazard
                    ? RoomEnemyOutcome.HazardDeletion(
                        combatSource.CountsAsEnemy)
                    : zol.Record.SubId == 1
                        ? RoomEnemyOutcome.ReplacementDeletion(
                            combatSource.CountsAsEnemy)
                        : RoomEnemyOutcome.EnemyDie(
                            combatSource.KillableEnemyIndex)),
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

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        // enemyCode34 state 0 is completed by ZolCharacter.Initialize.
        // Subid $00 intentionally remains hidden in state $08 until Link is
        // within the strict $28 Manhattan-distance wake check.
        Entity.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
}

internal sealed record KillEnemyPuffSpawn(Vector2 Position) : RoomEntitySpawn;

internal sealed record GelSpawn(
    Vector2 Position,
    string Name = "Gel",
    int KillableEnemyIndex = 0)
    : RoomEntitySpawn;
