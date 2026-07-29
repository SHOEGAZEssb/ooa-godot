using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GelRoomEntity
    : CombatEnemyRoomEntityAdapter<GelCharacter>, IFixedRoomEntity, IPlayerRestriction
{
    public GelRoomEntity(
        GelCharacter gel,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            gel,
            gel.SetTransitionDrawOffset,
            EnemyCombatDescriptor.FromSource(
                combatSource,
                CreateCombat(gel, soundRequested),
                EnemySwordResponse.NoKnockback),
            collisionZ: () => gel.ZFixed >> 8)
    { }

    public bool DisablesSword => Entity.IsAttached;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);

    private static EnemyCombatComponent CreateCombat(
        GelCharacter gel,
        Action<int> soundRequested) =>
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
                : null,
            (sourcePosition, strength) =>
            {
                gel.ApplySwordNoKnockback(sourcePosition, strength);
                soundRequested(OracleSoundEngine.SndDamageEnemy);
            });
}
