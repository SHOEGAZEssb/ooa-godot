using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SparkRoomEntity
    : CombatEnemyRoomEntityAdapter<SparkCharacter>, IFixedRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    internal SparkRoomEntity(
        SparkCharacter spark,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            spark,
            spark.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                spark,
                spark.Record.DamageQuarters,
                spark.TakeSwordHit,
                spark.TakeBurnHit,
                (_, _) => { },
                soundRequested,
                EnemySwordResponse.NoKnockback,
                acceptedHitSound: 0))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();
}
