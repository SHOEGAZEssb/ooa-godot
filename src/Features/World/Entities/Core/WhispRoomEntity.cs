using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WhispRoomEntity
    : CombatEnemyRoomEntityAdapter<WhispCharacter>, IFixedRoomEntity
{
    internal WhispRoomEntity(
        WhispCharacter whisp,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            whisp,
            whisp.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                whisp,
                whisp.Record.DamageQuarters,
                whisp.TakeSwordHit,
                whisp.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback,
                acceptedHitSound: 0))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
