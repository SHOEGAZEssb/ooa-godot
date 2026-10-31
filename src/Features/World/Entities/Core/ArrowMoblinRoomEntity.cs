using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class ArrowMoblinRoomEntity
    : CombatEnemyRoomEntityAdapter<ArrowMoblinCharacter>, IFixedRoomEntity, ISomariaBlockCollisionRoomEntity,
      IBoomerangCollisionRoomEntity, IScreenTransitionPreloadRoomEntity
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
                soundRequested), collisionZ: () => moblin.ZFixed >> 8)
    { }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.InitializationPending) Entity.UpdateFrame(Vector2.Zero);
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        int arrowAngle = Entity.UpdateFrame(
            frame.LinkTarget ?? frame.Player.EnemyContactPosition, frame.ScentSeedTarget, frame.Counter);
        if (arrowAngle >= 0)
        {
            spawns.Add(new EnemyArrowSpawn(
                Entity.Position, arrowAngle));
        }
    }
}
