using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class StalfosRoomEntity
    : CombatEnemyRoomEntityAdapter<StalfosCharacter>, IFixedRoomEntity
{
    private readonly Func<bool> _canSpawnPart;
    public StalfosRoomEntity(
        StalfosCharacter stalfos,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested,
        Func<bool>? canSpawnPart = null)
        : base(
            stalfos,
            stalfos.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                stalfos,
                stalfos.Record.DamageQuarters,
                stalfos.TakeSwordHit,
                damage => stalfos.TakeSwordHit(Vector2.Zero, damage),
                stalfos.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback), collisionZ: () => stalfos.ZFixed >> 8)
    { _canSpawnPart = canSpawnPart ?? (static () => true); }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.UpdateFrame(frame.Player.Position, frame.Player.StartedItemAnimationThisUpdate, _canSpawnPart))
            spawns.Add(new StalfosBoneSpawn(OracleObjectMath.ToPixelPosition(Entity.Position), Entity.ZFixed >> 8));
    }

}

internal sealed record StalfosBoneSpawn(Vector2 Position, int ZHigh = 0) : RoomEntitySpawn;
