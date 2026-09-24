using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArrowMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<ArrowMoblinCharacter>, IFixedRoomEntity, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override bool Stunned => Entity.StunCounter != 0;
    internal ArrowMoblinRoomEntity(
        ArrowMoblinCharacter moblin,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            moblin,
            moblin.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                moblin,
                moblin.Record.DamageQuarters,
                moblin.TakeSwordHit,
                moblin.TakeBurnHit,
                moblin.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback), collisionZ: () => moblin.ZFixed >> 8)
    { }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(
            frame.Player.Position, frame.ScentSeedTarget, frame.Counter);
        if (arrowAngle >= 0)
        {
            spawns.Add(new EnemyArrowSpawn(
                Entity.Position, arrowAngle));
        }
    }
}
