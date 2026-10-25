using System;
using System.Collections.Generic;

namespace Oracles;

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
                EnemySwordResponse.Knockback), collisionZ: () => sandCrab.ZFixed >> 8)
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget);
}
