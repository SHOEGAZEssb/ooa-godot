using System.IO;

namespace Oracles;
internal sealed class ValidationRingPlayerWorld : IPlayerWorld
{
    public int FrameCounter { get; set; }
    public bool IsTransitioning { get; set; }
    public bool ScreenScrolling { get; set; }
    public bool DialogueOpen => false;
    public bool NativeTextActive { get; set; }
    public bool SwordDisabled => false;
    public bool ItemUsageDisabled => false;
    public bool InShop { get; set; }
    public bool MovementDisabled { get; set; }
    public bool RingTransformationsAllowed { get; set; } = true;
    public bool RidingObject { get; set; }
    public bool SideScrolling { get; set; }
    public SideScrollPlayerParameters SideScrollParameters { get; set; } =
        new SideScrollPlayerDatabase(AgesGameContent.AssetRoot).Parameters;
    public SideScrollTerrainState SideScrollTerrain { get; set; }
    public int AdjacentWallsBitset { get; set; }
    public bool BlockMovement { get; set; }
    public bool BlockHorizontalMovement { get; set; }
    public bool BlockVerticalMovement { get; set; }
    public int TilePushingDirection { get; set; } = 0xff;
    public int SwordHitCalls { get; private set; }
    public bool AcceptSwordHits { get; set; }
    public int LastSwordDamage { get; private set; }
    public int ExpertTileHitCalls { get; private set; }
    public int SwordBeamCalls { get; private set; }
    public int LastSwordBeamDirection { get; private set; } = -1;
    public List<int> Sounds { get; } = new();
    public List<Vector2> SwimBubbles { get; } = new();
    public void SpawnSideScrollBubble(Vector2 position) => SwimBubbles.Add(position);
    public List<(Vector2 Position, HazardType Hazard)> DrowningSplashes { get; } =
        new();

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        SwordHitCalls++;
        LastSwordDamage = player.SwordDamage;
        return AcceptSwordHits;
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
    public Vector2 ResolveMovement(
        Vector2 playerPosition,
        Vector2 movement,
        bool allowWallSlide)
    {
        if (BlockMovement)
            return Vector2.Zero;
        return new Vector2(
            BlockHorizontalMovement ? 0.0f : movement.X,
            BlockVerticalMovement ? 0.0f : movement.Y);
    }
    public bool UpdatePushableBlocks(Vector2 playerPosition, Vector2I facing, Vector2 movementInput)
    {
        return false;
    }

    public ActiveTerrainInfo ActiveTerrain { get; set; }
    internal OracleRuntimeState? TileMemory { get; set; }
    public byte UpdateActiveLinkTile(Vector2 position, bool airborne)
    {
        if (TileMemory is not { } memory) return 0;
        byte previous = memory.ReadField("wActiveTilePos");
        memory.SetField("wIsTileSlippery",0);
        if (airborne)
        {
            memory.SetField("wActiveTileType",0); memory.SetField("wStandingOnTileCounter",0);
            return previous;
        }
        int count = previous == ActiveTerrain.PackedPosition && memory.ReadField("wActiveTileIndex") == ActiveTerrain.Terrain.Tile
            ? memory.ReadField("wStandingOnTileCounter") : 0;
        memory.SetField("wStandingOnTileCounter",unchecked((byte)(count + 1)));
        memory.SetField("wLastActiveTileType",memory.ReadField("wActiveTileType"));
        memory.SetField("wActiveTilePos",(byte)ActiveTerrain.PackedPosition);
        memory.SetField("wActiveTileIndex",ActiveTerrain.Terrain.Tile);
        memory.SetField("wActiveTileType",(byte)ActiveTerrain.Terrain.Type);
        return previous;
    }
    public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition) =>
        ActiveTerrain;
    public SideScrollTerrainState GetSideScrollTerrain(Vector2 playerPosition) =>
        SideScrollTerrain;
    public SideScrollActiveTiles SideScrollActiveTiles => new(
        SideScrollTerrain.ActiveTile, SideScrollTerrain.ActiveType, SideScrollTerrain.BelowType);
    public int GetAdjacentWallsBitset(Vector2 playerPosition) =>
        AdjacentWallsBitset;
    public bool TryStartTerrainJump(Player player, Vector2 from, Vector2 attemptedMovement, int objectAngle) => false;
    public bool ApplyLandedTileHit(Vector2 playerPosition) => false;
    public void BeginLedgeScreenTransition(Player player)
    {
    }
    public void ResumeLedgeHopAfterScroll(Player player)
    {
    }
    public void SpawnDrowningSplash(Vector2 position, HazardType hazard)
    {
        DrowningSplashes.Add((position, hazard));
    }

    public void DeactivateWarpAtPlayerPosition(Player player)
    {
    }
    public bool CheckTileWarp(Player player) => false;
    public void CheckRoomExit(Player player)
    {
    }
}
