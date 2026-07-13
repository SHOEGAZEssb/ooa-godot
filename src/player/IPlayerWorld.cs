using Godot;

namespace oracleofages;

public interface IPlayerWorld
{
    bool IsTransitioning { get; }
    bool DialogueOpen { get; }
    bool ApplySwordHit(Player player, Rect2 hitbox);
    bool TryInteract(Player player);
    bool Collides(Vector2 playerPosition);
    Vector2 ResolveMovement(Vector2 playerPosition, Vector2 movement, bool allowWallSlide);
    ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition);
    Vector2 GetTerrainPush(Vector2 playerPosition);
    bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement);
    void SpawnDrowningSplash(Vector2 position, OracleRoomData.HazardType hazard);
    bool CheckTileWarp(Player player);
    void CheckRoomExit(Player player);
}
