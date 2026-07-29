using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class StalfosRoomEntity
    : CombatEnemyRoomEntityAdapter<StalfosCharacter>, IFixedRoomEntity
{
    public StalfosRoomEntity(
        StalfosCharacter stalfos,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            stalfos,
            stalfos.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                stalfos,
                stalfos.Record.DamageQuarters,
                stalfos.TakeSwordHit,
                damage => stalfos.TakeSwordHit(Vector2.Zero, damage),
                stalfos.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

}
