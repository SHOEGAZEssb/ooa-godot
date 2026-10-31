using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class GhiniRoomEntity
    : CombatEnemyRoomEntityAdapter<GhiniCharacter>, IFixedRoomEntity
{
    public GhiniRoomEntity(
        GhiniCharacter ghini,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            ghini, ghini.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                ghini,
                ghini.Record.DamageQuarters,
                ghini.TakeSwordHit,
                ghini.TakeBurnHit,
                ghini.ApplySwordKnockback,
                soundRequested))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame();
        if (Entity.IsDead && !Entity.HasCompletedKnockbackDeath && CombatDescriptor.Combat.CreateDeathPuff() is { } puff)
            spawns.Add(puff with { DecrementsRoomCount = CombatDescriptor.CountsAsEnemy });
    }
}
