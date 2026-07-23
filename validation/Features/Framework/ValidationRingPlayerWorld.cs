using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;
internal sealed class ValidationRingPlayerWorld : IPlayerWorld
{
    public bool IsTransitioning => false;
    public bool DialogueOpen => false;
    public bool SwordDisabled => false;
    public bool ItemUsageDisabled => false;
    public bool MovementDisabled => false;
    public bool RingTransformationsAllowed { get; set; } = true;
    public bool RidingObject => false;
    public int SwordHitCalls { get; private set; }
    public int LastSwordDamage { get; private set; }
    public int ExpertTileHitCalls { get; private set; }
    public int SwordBeamCalls { get; private set; }
    public int LastSwordBeamDirection { get; private set; } = -1;
    public List<int> Sounds { get; } = new();

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        SwordHitCalls++;
        LastSwordDamage = player.SwordDamage;
        return false;
    }

    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) => false;
    public bool ApplyExpertsRingTileHit(Player player, int direction)
    {
        ExpertTileHitCalls++;
        return true;
    }

    public bool TryCreateSwordBeam(Player player, int direction)
    {
        SwordBeamCalls++;
        LastSwordBeamDirection = direction;
        return true;
    }

    public void PlaySound(int soundId) => Sounds.Add(soundId);
    public bool TryInteract(Player player) => false;
    public bool TrySecondaryInteract(Player player) => false;
    public bool TryUseBracelet(Player player, bool primaryButton) => false;
    public bool UpdateBracelet(Player player, Vector2 movementInput, bool primaryHeld, bool secondaryHeld, bool itemButtonJustPressed) => false;
    public void InterruptBracelet(Player player, bool discard)
    {
    }

    public int TryUseSeedSatchel(Player player) => 0;
    public bool DigWithShovel(Vector2 point, Vector2I direction) => false;
    public bool Collides(Vector2 playerPosition) => false;
    public Vector2 ResolveMovement(Vector2 playerPosition, Vector2 movement, bool allowWallSlide) => movement;
    public bool IsPushingAgainstWall(Vector2 playerPosition, Vector2I facing, Vector2 movementInput) => false;
    public void UpdatePushableBlocks(Vector2 playerPosition, Vector2I facing, Vector2 movementInput)
    {
    }

    public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition) => default;
    public Vector2 GetTerrainPush(Vector2 playerPosition) => Vector2.Zero;
    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement) => false;
    public void SpawnDrowningSplash(Vector2 position, HazardType hazard)
    {
    }

    public bool CheckTileWarp(Player player) => false;
    public void CheckRoomExit(Player player)
    {
    }
}
