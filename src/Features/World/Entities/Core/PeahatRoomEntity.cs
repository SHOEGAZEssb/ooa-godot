using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PeahatRoomEntity
    : CombatEnemyRoomEntityAdapter<PeahatCharacter>, IFixedRoomEntity
{
    internal PeahatRoomEntity(
        PeahatCharacter peahat,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            peahat,
            peahat.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                peahat,
                peahat.Record.DamageQuarters,
                peahat.TakeSwordHit,
                peahat.TakeBurnHit,
                peahat.ApplySwordNoKnockback,
                soundRequested,
                EnemySwordResponse.NoKnockback),
            collisionZ: () => peahat.ZHigh)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
