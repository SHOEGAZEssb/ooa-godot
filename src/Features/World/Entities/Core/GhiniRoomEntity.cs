using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

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
                (sourcePosition, strength) =>
                {
                    ghini.ApplySwordKnockback(sourcePosition, strength);
                    soundRequested(OracleSoundEngine.SndDamageEnemy);
                },
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
