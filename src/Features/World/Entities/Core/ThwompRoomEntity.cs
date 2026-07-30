using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ThwompRoomEntity
    : CombatEnemyRoomEntityAdapter<ThwompCharacter>,
        IFixedRoomEntity, IPlayerRideableRoomEntity
{
    private bool _linkRiding;

    bool IPlayerRideableRoomEntity.LinkRiding => _linkRiding;

    internal ThwompRoomEntity(
        ThwompCharacter thwomp,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            thwomp,
            thwomp.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                thwomp,
                thwomp.Record.DamageQuarters,
                thwomp.TakeSwordHit,
                thwomp.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback,
                acceptedHitSound: 0))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.Position);
        _linkRiding = Entity.IsLinkRiding(
            frame.Player, out float targetY);
        if (_linkRiding)
        {
            frame.Player.SetMovingPlatformCoordinateHigh(
                horizontal: false,
                Mathf.FloorToInt(targetY));
        }
    }
}
