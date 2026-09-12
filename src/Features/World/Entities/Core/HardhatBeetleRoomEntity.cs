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
        Action<int> soundRequested,
        Action? fellInHole = null)
        : base(
            beetle,
            beetle.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                beetle,
                beetle.Record.Id == 0x5f ? 0 : beetle.Record.DamageQuarters,
                beetle.TakeBumpHit,
                beetle.TakeBurnHit,
                beetle.ApplySwordBump,
                soundRequested,
                EnemySwordResponse.Bump,
                acceptedHitSound: 0))
    { _fellInHole = fellInHole; }

    private readonly Action? _fellInHole;
    public override void HandleLinkContact(Player player)
    {
        if (Entity.Record.Id != 0x5f || player.IsUsingShield && CombatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        { base.HandleLinkContact(player); return; }
        // Collision $42/$00 selects collisionEffect06: LINKDMG_14 and
        // ENEMYDMG_1c. Only Link recoils, with no health subtraction.
        if (!Entity.IsDead && Entity.CollisionEnabled && Entity.InvincibilityCounter == 0 &&
            player.OverlapsEnemyCollision(Entity.CollisionBounds) && player.TryApplyHarmlessContactRecoil(Entity.Position))
            CombatDescriptor.RequestSound(OracleSoundEngine.SndBombLand);
    }
    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.DeathHazard == HazardType.Hole) _fellInHole?.Invoke();
        base.OnFinished(spawns);
    }

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
