using Godot;

namespace oracleofages;

public sealed class PlayerWorld : IPlayerWorld
{
    private readonly RoomTransitionController _transitions;
    private readonly InteractionController _interactions;
    private readonly RoomCollision _collision;
    private readonly TerrainController _terrain;
    private readonly CombatController _combat;

    public bool IsTransitioning => _transitions.IsTransitioning;
    public bool DialogueOpen => _interactions.DialogueOpen;

    public PlayerWorld(
        RoomTransitionController transitions,
        InteractionController interactions,
        RoomCollision collision,
        TerrainController terrain,
        CombatController combat)
    {
        _transitions = transitions;
        _interactions = interactions;
        _collision = collision;
        _terrain = terrain;
        _combat = combat;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox) => _combat.ApplySwordHit(player, hitbox);
    public bool TryInteract(Player player) => _interactions.TryInteract(player);
    public bool Collides(Vector2 position) => _collision.Collides(position);
    public Vector2 ResolveMovement(Vector2 position, Vector2 movement, bool allowWallSlide) =>
        _collision.ResolveMovement(position, movement, allowWallSlide);
    public ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    public Vector2 GetTerrainPush(Vector2 position) => _terrain.GetTerrainPush(position);
    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    public void SpawnDrowningSplash(Vector2 position, OracleRoomData.HazardType hazard) =>
        _terrain.SpawnDrowningSplash(position, hazard);
    public bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    public void CheckRoomExit(Player player) => _transitions.CheckRoomExit(player);
}
