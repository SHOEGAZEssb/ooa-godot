using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LeeverRoomEntity
    : CombatEnemyRoomEntityAdapter<LeeverCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    internal LeeverRoomEntity(
        LeeverCharacter leever,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            leever,
            leever.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                leever,
                leever.Record.DamageQuarters,
                leever.TakeSwordHit,
                leever.TakeBurnHit,
                leever.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(
            frame.Player.Position,
            frame.Player.FacingVector,
            frame.Counter);

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Hidden;
    }
}
