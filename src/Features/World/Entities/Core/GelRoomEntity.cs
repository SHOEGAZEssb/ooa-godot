using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GelRoomEntity
    : CombatEnemyRoomEntityAdapter<GelCharacter>, IFixedRoomEntity, IPlayerRestriction
{
    public GelRoomEntity(
        GelCharacter gel,
        Action<int> soundRequested,
        bool countsAsEnemy = true,
        int killableEnemyIndex = 0)
        : base(
            gel, gel.SetTransitionDrawOffset, CreateCombat(gel),
            countsAsEnemy, killableEnemyIndex,
            finished: () => EnemyHazardSounds.PlayHoleSound(
                gel.DeathHazard, soundRequested),
            collisionZ: () => gel.ZFixed >> 8)
    { }

    public bool DisablesSword => Entity.IsAttached;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);

    private static EnemyCombatComponent CreateCombat(GelCharacter gel) =>
        new(
            () => gel.IsDead,
            () => gel.CollisionBounds,
            (_, damage) => gel.TakeSwordHit(damage),
            gel.TakeSwordHit,
            player =>
            {
                if (gel.OverlapsLink(player.Position))
                    gel.AttachToLink(player.Position);
            },
            () => gel.IsDead && !gel.DiedInHazard
                ? new EnemyDeathPuffSpawn(gel.Position, EnemyId: gel.Definition.Id)
                : null);
}
