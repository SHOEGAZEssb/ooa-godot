using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class OctorokRoomEntity
    : CombatEnemyRoomEntityAdapter<OctorokCharacter>, IFixedRoomEntity,
      IBoomerangCollisionRoomEntity, ISomariaBlockCollisionRoomEntity,
      IScreenTransitionPreloadRoomEntity
{
    public OctorokRoomEntity(
        OctorokCharacter octorok,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            octorok,
            octorok.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                octorok,
                octorok.Record.DamageQuarters,
                octorok.TakeSwordHit,
                octorok.TakeBurnHit,
                octorok.ApplySwordKnockback,
                soundRequested))
    { }

    protected override bool Stunned => Entity.StunCounter != 0;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(
                frame.LinkTarget ?? frame.Player.EnemyContactPosition, frame.ScentSeedTarget, frame.Counter))
            // ecom_spawnProjectile copies the parent's high XY bytes only.
            spawns.Add(new OctorokRockSpawn(new(OracleObjectPosition.HighByte(Entity.Position.X),
                OracleObjectPosition.HighByte(Entity.Position.Y)), Entity.Angle));
    }

}

internal sealed record OctorokRockSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
