using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SandCrabRoomEntity
    : CombatEnemyRoomEntityAdapter<SandCrabCharacter>, IFixedRoomEntity
{
    internal SandCrabRoomEntity(
        SandCrabCharacter sandCrab,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            sandCrab,
            sandCrab.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                sandCrab,
                sandCrab.Record.DamageQuarters,
                sandCrab.TakeSwordHit,
                sandCrab.TakeBurnHit,
                sandCrab.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
