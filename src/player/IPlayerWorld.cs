using Godot;

namespace oracleofages;

public interface IPlayerWorld
{
    bool IsTransitioning { get; }
    bool DialogueOpen { get; }
    bool SwordDisabled { get; }
    bool ItemUsageDisabled { get; }
    bool MovementDisabled { get; }
    bool RingTransformationsAllowed { get; }
    bool RidingObject { get; }
    bool ApplySwordHit(Player player, Rect2 hitbox);
    bool ApplySwordTileHit(Player player, int direction, bool swordPoke);
    bool ApplyExpertsRingTileHit(Player player, int direction);
    bool TryCreateSwordBeam(Player player, int direction);
    void PlaySound(int soundId);
    bool TryInteract(Player player);
    bool TrySecondaryInteract(Player player);
    bool TryUseBracelet(Player player, bool primaryButton);
    bool UpdateBracelet(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld,
        bool itemButtonJustPressed);
    void InterruptBracelet(Player player, bool discard);
    int TryUseSeedSatchel(Player player);
    bool DigWithShovel(Vector2 point, Vector2I direction);
    bool Collides(Vector2 playerPosition);
    Vector2 ResolveMovement(Vector2 playerPosition, Vector2 movement, bool allowWallSlide);
    bool IsPushingAgainstWall(
        Vector2 playerPosition,
        Vector2I facing,
        Vector2 movementInput);
    void UpdatePushableBlocks(
        Vector2 playerPosition,
        Vector2I facing,
        Vector2 movementInput);
    ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition);
    Vector2 GetTerrainPush(Vector2 playerPosition);
    bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement);
    void SpawnDrowningSplash(Vector2 position, OracleRoomData.HazardType hazard);
    bool CheckTileWarp(Player player);
    void CheckRoomExit(Player player);
}
