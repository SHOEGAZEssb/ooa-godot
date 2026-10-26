using Godot;

namespace Oracles;

internal sealed class SeasonsOpeningWorld(SeasonsOpeningSession session) : IPlayerWorld
{
    public int FrameCounter => session.FrameCounter;
    public bool IsTransitioning => session.Scrolling;
    public bool ScreenScrolling => session.Scrolling;
    public bool DialogueOpen => session.Dialogue.IsOpen;
    public bool SwordDisabled => false;
    public bool ItemUsageDisabled => true;
    public bool InShop => false;
    public bool MovementDisabled => session.DisabledObjects != 0;
    public byte NativeObjectDisableMask => session.DisabledObjects;
    public bool PlayerUpdatesFrozen => session.Scrolling || session.Fading;
    public bool LinkDisabled => (session.DisabledObjects & 0x81) != 0;
    public bool NativePaletteChanging => session.Fading;
    public bool RingTransformationsAllowed => false;
    public bool RidingObject => false;
    public bool SideScrolling => false;
    public SideScrollPlayerParameters SideScrollParameters => default;
    public int TilePushingDirection { get; set; }
    public bool ApplySwordHit(Player player, Rect2 hitbox) => false;
    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) => false;
    public bool ApplyExpertsRingTileHit(Player player, int direction) => false;
    public bool TryCreateSwordBeam(Player player, int direction) => false;
    public void PlaySound(int soundId) => session.Sound.PlaySound(soundId);
    public bool TryInteract(Player player) => session.TryTalk();
    public bool TrySecondaryInteract(Player player) => false;
    public bool TryUseBracelet(Player player, bool primaryButton) => false;
    public bool UpdateBracelet(Player player, Vector2 movementInput, bool primaryHeld, bool secondaryHeld, bool itemButtonJustPressed) => false;
    public void InterruptBracelet(Player player, bool discard) { }
    public int TryUseSeedSatchel(Player player) => 0;
    public bool DigWithShovel(Vector2 point, Vector2I direction) => false;
    public bool Collides(Vector2 position) => session.Collision.Collides(position);
    public Vector2 ResolveMovement(Vector2 position, Vector2 movement, bool allowWallSlide) => session.Collision.ResolveMovement(position, movement, allowWallSlide);
    public Vector2 ResolveNativeMovement(Vector2 position, int speed, int angle, bool allowWallSlide, int? adjacentWalls = null) =>
        session.Collision.ResolveNativeMovement(position, speed, angle, allowWallSlide, adjacentWalls);
    public bool IsPushingAgainstWall(Vector2 position, Vector2I facing, Vector2 input) => session.Collision.IsPushingAgainstWall(position, facing, input);
    public bool UpdatePushableBlocks(Vector2 position, Vector2I facing, Vector2 input) => false;
    public ActiveTerrainInfo GetActiveTerrain(Vector2 position)
    {
        Vector2 sample = position + new Vector2(0, 5);
        int x = Mathf.FloorToInt(sample.X / 16), y = Mathf.FloorToInt(sample.Y / 16);
        return new(session.Room.GetTerrainInfo(sample), sample, new(x * 16 + 8, y * 16 + 8), (y << 4) | x);
    }
    public SideScrollTerrainState GetSideScrollTerrain(Vector2 position) => default;
    public int GetAdjacentWallsBitset(Vector2 position) => GetActiveTerrain(position).Terrain.Type == TerrainType.Stump
        ? 0xff : session.Collision.AdjacentWallsBitset(position);
    public void UpdateLinkOnChest(Vector2 position, bool airborne) => session.Collision.UpdateLinkOnChest(position, airborne);
    public void UpdateActiveLinkTile(Vector2 position) => session.Collision.UpdateActiveLinkTile(position);
    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement) => false;
    public bool ApplyLandedTileHit(Vector2 position) => false;
    public void BeginLedgeScreenTransition(Player player) { }
    public void ResumeLedgeHopAfterScroll(Player player) { }
    public void SpawnDrowningSplash(Vector2 position, HazardType hazard) { }
    public void SpawnSideScrollBubble(Vector2 position) { }
    public void DeactivateWarpAtPlayerPosition(Player player) { }
    public bool CheckTileWarp(Player player) => false;
    public void CheckRoomExit(Player player) => session.CheckRoomExit();
}
