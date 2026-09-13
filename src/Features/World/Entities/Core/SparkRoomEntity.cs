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

    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        if (effect != 0x1c) return false;
        // Spark's JUST_HIT handler ignores $8d and continues normal movement.
        hook.NotifyObjectCollision();
        return true;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
