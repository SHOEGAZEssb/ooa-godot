using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GiantGhiniChildRoomEntity
    : CombatEnemyRoomEntityAdapter<GiantGhiniChild>, IFixedRoomEntity,
        IPlayerRestriction, IAfterPlayerUpdateRoomEntity
{
    protected override int GaleCollisionMode => GaleSeedCollisionDatabase.Shared.EnemyMode(Entity.Record.Id);
    public GiantGhiniChildRoomEntity(
        GiantGhiniChild child,
        Action<int> soundRequested)
        : base(
            child, child.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                new EnemyCombatComponent(
                    () => child.IsDead,
                    () => child.CollisionBounds,
                    child.TakeSwordHit,
                    child.TakeBurnHit,
                    child.HandleLinkContact,
                    () => child.IsDead
                        ? new EnemyDeathPuffSpawn(
                            child.Position,
                            EnemyId: child.Record.Id)
                        : null,
                    (sourcePosition, strength) =>
                    {
                        child.ApplySwordKnockback(sourcePosition, strength);
                        soundRequested(SoundId.SndDamageEnemy);
                    }),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    child.State == ChildState.Fading
                        ? RoomEnemyOutcome.SilentDeletion(
                            decrementsRoomCount: true)
                        : RoomEnemyOutcome.EnemyDie(
                            killableEnemyIndex: 0)), collisionZ: () => child.ZHigh)
    { }

    public bool DisablesSword => Entity.DisablesItems;
    public bool AlternatesMovementWithSwordRestriction => false;
    public bool DisablesItems => Entity.DisablesItems;
    public bool DisablesMovement => Entity.SlowsLink;
    public void AfterPlayerUpdate() => Entity.ClearRestrictions();

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(
            frame.Player, frame.AnyButtonJustPressed, frame.Counter, spawns);
}
