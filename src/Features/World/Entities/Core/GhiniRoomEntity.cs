using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GhiniRoomEntity
    : CombatEnemyRoomEntityAdapter<GhiniCharacter>, IFixedRoomEntity
{
    public GhiniRoomEntity(
        GhiniCharacter ghini,
        int killableEnemyIndex,
        Action<int> soundRequested)
        : base(
            ghini, ghini.SetTransitionDrawOffset,
            EnemyCombatComponent.WithContactDamage(
                () => ghini.IsDead,
                () => ghini.CollisionBounds,
                ghini.TakeSwordHit,
                ghini.TakeBurnHit,
                ghini.OverlapsLink,
                () => ghini.Position,
                ghini.Record.DamageQuarters,
                () => ghini.IsDead
                    ? new EnemyDeathPuffSpawn(ghini.Position, EnemyId: ghini.Record.Id)
                    : null,
                (sourcePosition, strength) =>
                {
                    ghini.ApplySwordKnockback(sourcePosition, strength);
                    soundRequested(OracleSoundEngine.SndDamageEnemy);
                }),
            countsAsEnemy: true,
            killableEnemyIndex)
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}
