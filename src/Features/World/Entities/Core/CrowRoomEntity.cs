using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CrowRoomEntity
    : CombatEnemyRoomEntityAdapter<CrowCharacter>, IFixedRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public CrowRoomEntity(
        CrowCharacter crow,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
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
                soundRequested,
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
        Entity.UpdateFrame(Entity.Record.SubId == 1 ? frame.LinkTarget ?? frame.Player.EnemyContactPosition : frame.Player.Position);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

}
