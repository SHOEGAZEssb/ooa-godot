using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OctorokRoomEntity
    : CombatEnemyRoomEntityAdapter<OctorokCharacter>, IFixedRoomEntity
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
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position))
            spawns.Add(new OctorokRockSpawn(Entity.Position, Entity.Angle));
    }

}

internal sealed record OctorokRockSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn(UpdateThisFrame: true);
