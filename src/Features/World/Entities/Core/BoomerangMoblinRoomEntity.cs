using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BoomerangMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<BoomerangMoblinCharacter>, IFixedRoomEntity
{
    public BoomerangMoblinRoomEntity(
        BoomerangMoblinCharacter moblin,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            moblin, moblin.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                moblin,
                moblin.Record.DamageQuarters,
                moblin.TakeSwordHit,
                moblin.TakeBurnHit,
                moblin.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int angle = Entity.UpdateFrame(
            frame.Player.Position, frame.ScentSeedTarget);
        if (angle >= 0)
            spawns.Add(new MoblinBoomerangSpawn(Entity, Entity.Position, angle));
    }
}

internal sealed record EnemyDeathPuffSpawn(
    Vector2 Position,
    bool HighKnockback = false,
    int EnemyId = -1,
    bool DecrementsRoomCount = false) : RoomEntitySpawn;

internal sealed record MoblinBoomerangSpawn(
    BoomerangMoblinCharacter Owner,
    Vector2 Position,
    int Angle) : RoomEntitySpawn(UpdateThisFrame: true);
