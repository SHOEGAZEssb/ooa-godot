using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class HardhatBeetleRoomEntity
    : CombatEnemyRoomEntityAdapter<HardhatBeetleCharacter>,
        IFixedRoomEntity
{
    public HardhatBeetleRoomEntity(
        HardhatBeetleCharacter beetle,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            beetle,
            beetle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                beetle,
                beetle.Record.DamageQuarters,
                beetle.TakeBumpHit,
                beetle.TakeBurnHit,
                beetle.ApplySwordBump,
                soundRequested,
                EnemySwordResponse.Bump,
                acceptedHitSound: 0))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    public override SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.CollisionEnabled &&
        CombatDescriptor.Combat.Intersects(hitbox)
            ? seedItem == 0x24
                ? SeedHitResult.Activate
                : seedItem == 0x21
                    ? SeedHitResult.Activate
                : seedItem == 0x20
                    ? SeedHitResult.Consume
                    : SeedHitResult.None
            : SeedHitResult.None;
}
