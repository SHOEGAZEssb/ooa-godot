using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GiantGhiniChildRoomEntity
    : CombatEnemyRoomEntityAdapter<GiantGhiniChild>, IFixedRoomEntity,
        IPlayerRestriction
{
    public GiantGhiniChildRoomEntity(GiantGhiniChild child)
        : base(
            child, child.SetTransitionDrawOffset,
            new EnemyCombatComponent(
                () => child.IsDead,
                () => child.CollisionBounds,
                child.TakeSwordHit,
                child.TakeBurnHit,
                child.HandleLinkContact,
                () => child.IsDead
                    ? new EnemyDeathPuffSpawn(child.Position, EnemyId: child.Record.Id)
                    : null,
                child.ApplySwordKnockback),
            countsAsEnemy: true,
            killableEnemyIndex: 0)
    { }

    public bool DisablesSword => false;
    public bool DisablesItems => Entity.DisablesItems;
    public bool DisablesMovement => Entity.SlowsLink;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(
            frame.Player, frame.AnyButtonJustPressed, frame.Counter, spawns);
}
