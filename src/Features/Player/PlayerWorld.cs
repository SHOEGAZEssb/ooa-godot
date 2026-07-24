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
    private readonly OverworldKeyholeController _keyholes;
    private readonly TerrainController _terrain;
    private readonly CombatController _combat;
    private readonly RoomEntityManager _entities;
    private readonly BraceletController _bracelet;
    private readonly ShovelController _shovel;
    private readonly SeedSatchelController _seedSatchel;
    private readonly RoomEventController _roomEvents;
    private readonly InventoryState _inventory;
    private readonly OracleSoundEngine _sound;
    private readonly Func<bool> _collisionsDisabled;

    public int FrameCounter => _entities.FrameCounter;
    public bool IsTransitioning => _transitions.IsTransitioning;
    public bool DialogueOpen => _interactions.DialogueOpen;
    public bool SwordDisabled => _roomEvents.Active || _entities.PlayerSwordDisabled;
    public bool ItemUsageDisabled => _entities.PlayerItemUsageDisabled;
    public bool MovementDisabled => _roomEvents.Active || _entities.PlayerMovementDisabled;
    public bool RidingObject => _entities.PlayerRidingObject;
    public bool SideScrolling =>
        (_terrain.CurrentTilesetFlags & 0x20) != 0;
    public SideScrollPlayerParameters SideScrollParameters =>
        _terrain.SideScrollParameters;
    public bool RingTransformationsAllowed =>
        !_roomEvents.Active &&
        (_terrain.CurrentTilesetFlags & 0x60) == 0 &&
        !_entities.PlayerRingTransformationsDisabled;

    public PlayerWorld(
        RoomTransitionController transitions,
        InteractionController interactions,
        RoomCollision collision,
        PushBlockController pushBlocks,
        DungeonKeyDoorController keyDoors,
        OverworldKeyholeController keyholes,
        TerrainController terrain,
        CombatController combat,
        RoomEntityManager entities,
        BraceletController bracelet,
        ShovelController shovel,
        SeedSatchelController seedSatchel,
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
        _keyholes = keyholes;
        _terrain = terrain;
        _combat = combat;
        _entities = entities;
        _bracelet = bracelet;
        _shovel = shovel;
        _seedSatchel = seedSatchel;
        _roomEvents = roomEvents;
        _inventory = inventory;
        _sound = sound;
        _collisionsDisabled = collisionsDisabled;
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox) => _combat.ApplySwordHit(player, hitbox);
    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) =>
        _combat.ApplySwordTileHit(player, direction, swordPoke);
    public bool ApplyExpertsRingTileHit(Player player, int direction) =>
        _combat.ApplyExpertsRingTileHit(player, direction);
    public bool TryCreateSwordBeam(Player player, int direction) =>
        _entities.TrySpawnSwordBeam(player.Position, direction);
    public void PlaySound(int soundId) => _sound.PlaySound(soundId);
    public bool TryInteract(Player player) => _interactions.TryInteract(player);
    public bool TrySecondaryInteract(Player player) =>
        _roomEvents.TryInteractPlayer(player);
    public bool TryUseBracelet(Player player, bool primaryButton) =>
        _bracelet.TryUse(player, primaryButton);
    public bool UpdateBracelet(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld,
        bool itemButtonJustPressed) =>
        _bracelet.Update(
            player,
            movementInput,
            primaryHeld,
            secondaryHeld,
            itemButtonJustPressed);
    public void InterruptBracelet(Player player, bool discard) =>
        _bracelet.Interrupt(player, discard);
    public int TryUseSeedSatchel(Player player) => _seedSatchel.TryUse(player);
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
            _inventory.BraceletLevel);
        _keyDoors.UpdatePushAttempt(position, facing, resolvedInput);
        _keyholes.UpdatePushAttempt(position, facing, resolvedInput);
    }
    public ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    public SideScrollTerrainState GetSideScrollTerrain(Vector2 position) =>
        _terrain.GetSideScrollTerrain(position);
    public int GetAdjacentWallsBitset(Vector2 position) =>
        _collision.AdjacentWallsBitset(position);
    public Vector2 GetTerrainPush(Vector2 position) =>
        RidingObject ? Vector2.Zero : _terrain.GetTerrainPush(position);
    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    public bool ApplyLandedTileHit(Vector2 position) =>
        _combat.ApplyLandedTileHit(position);
    public void BeginLedgeScreenTransition(Player player) =>
        _transitions.BeginLedgeScroll(player);
    public void ResumeLedgeHopAfterScroll(Player player) =>
        _terrain.ResumeLedgeHopAfterScroll(player);
    public void SpawnDrowningSplash(Vector2 position, HazardType hazard) =>
        _terrain.SpawnSplash(position, hazard);
    public bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    public void CheckRoomExit(Player player) => _transitions.CheckRoomExit(player);
}
