using Godot;
using System;

namespace oracleofages;

public sealed class PlayerWorld : IPlayerWorld
{
    private readonly RoomTransitionController _transitions;
    private readonly InteractionController _interactions;
    private readonly RoomCollision _collision;
    private readonly PushBlockController _pushBlocks;
    private readonly DungeonKeyDoorController _keyDoors;
    private readonly TerrainController _terrain;
    private readonly CombatController _combat;
    private readonly RoomEntityManager _entities;
    private readonly BraceletController _bracelet;
    private readonly ShovelController _shovel;
    private readonly RoomEventController _roomEvents;
    private readonly InventoryState _inventory;
    private readonly OracleSoundEngine _sound;
    private readonly Func<bool> _collisionsDisabled;

    public bool IsTransitioning => _transitions.IsTransitioning;
    public bool DialogueOpen => _interactions.DialogueOpen;
    public bool SwordDisabled => _roomEvents.Active || _entities.PlayerSwordDisabled;
    public bool MovementDisabled => _roomEvents.Active || _entities.PlayerMovementDisabled;

    public PlayerWorld(
        RoomTransitionController transitions,
        InteractionController interactions,
        RoomCollision collision,
        PushBlockController pushBlocks,
        DungeonKeyDoorController keyDoors,
        TerrainController terrain,
        CombatController combat,
        RoomEntityManager entities,
        BraceletController bracelet,
        ShovelController shovel,
        RoomEventController roomEvents,
        InventoryState inventory,
        OracleSoundEngine sound,
        Func<bool> collisionsDisabled)
    {
        _transitions = transitions;
        _interactions = interactions;
        _collision = collision;
        _pushBlocks = pushBlocks;
        _keyDoors = keyDoors;
        _terrain = terrain;
        _combat = combat;
        _entities = entities;
        _bracelet = bracelet;
        _shovel = shovel;
        _roomEvents = roomEvents;
        _inventory = inventory;
        _sound = sound;
        _collisionsDisabled = collisionsDisabled;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox) => _combat.ApplySwordHit(player, hitbox);
    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) =>
        _combat.ApplySwordTileHit(player, direction, swordPoke);
    public void PlaySound(int soundId) => _sound.PlaySound(soundId);
    public bool TryInteract(Player player) => _interactions.TryInteract(player);
    public bool TryUseBracelet(Player player) => _bracelet.TryUse(player);
    public bool DigWithShovel(Vector2 point, Vector2I direction) =>
        _shovel.TryDig(point, direction);
    public bool Collides(Vector2 position) =>
        !_collisionsDisabled() && _collision.Collides(position);
    public Vector2 ResolveMovement(Vector2 position, Vector2 movement, bool allowWallSlide) =>
        _collisionsDisabled()
            ? movement
            : _collision.ResolveMovement(position, movement, allowWallSlide);
    public bool IsPushingAgainstWall(
        Vector2 position,
        Vector2I facing,
        Vector2 movementInput) =>
        !_collisionsDisabled() &&
        _collision.IsPushingAgainstWall(position, facing, movementInput);
    public void UpdatePushableBlocks(
        Vector2 position,
        Vector2I facing,
        Vector2 movementInput)
    {
        Vector2 resolvedInput = _collisionsDisabled()
            ? Vector2.Zero
            : movementInput;
        _pushBlocks.UpdatePushAttempt(
            position, facing, resolvedInput,
            _inventory.HasTreasure(TreasureDatabase.TreasureBracelet));
        _keyDoors.UpdatePushAttempt(position, facing, resolvedInput);
    }
    public ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    public Vector2 GetTerrainPush(Vector2 position) => _terrain.GetTerrainPush(position);
    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    public void SpawnDrowningSplash(Vector2 position, OracleRoomData.HazardType hazard) =>
        _terrain.SpawnSplash(position, hazard);
    public bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    public void CheckRoomExit(Player player) => _transitions.CheckRoomExit(player);
}
