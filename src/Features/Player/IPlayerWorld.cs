using Godot;
using System;

namespace oracleofages;

public interface IPlayerWorld
{
    int FrameCounter { get; }
    bool IsTransitioning { get; }
    bool DeathUpdatesSuspendedByWarp => false;
    bool PassesNpcs => false;
    bool InteractionMenusDisabled => false;
    bool ScreenScrolling { get; }
    bool DialogueOpen { get; }
    bool NativeTextActive => DialogueOpen;
    bool SwordDisabled { get; }
    bool ItemUsageDisabled { get; }
    bool MovementDisabled { get; }
    bool PlayerUpdatesFrozen => false;
    bool RingTransformationsAllowed { get; }
    bool RidingObject { get; }
    int RaisedFloorOffset
    {
        get => 0;
        set
        {
            if (value != 0)
                throw new NotSupportedException("Raised-floor Link requires the authoritative wLinkRaisedFloorOffset owner.");
        }
    }
    bool GaleWarpDisabled => false;
    bool NativeWarpsDisabled => false;
    void SetNativeWarpsDisabled(bool disabled) =>
        throw new NotSupportedException("LINK_STATE_GRABBED $0d requires the authoritative wWarpsDisabled owner.");
    bool PlayerContactDisabled => false;
    Vector2? MountedCompanionPosition => null;
    Vector2? MountedRaftPosition => null;
    bool BombParentActive => false;
    bool BraceletParentActive => false;
    bool SideScrolling { get; }
    bool Underwater => false;
    SideScrollPlayerParameters SideScrollParameters { get; }
    bool ApplySwordHit(Player player, Rect2 hitbox);
    bool ApplySwordTileHit(Player player, int direction, bool swordPoke);
    bool ApplyExpertsRingTileHit(Player player, int direction);
    bool TryCreateSwordBeam(Player player, int direction);
    void PlaySound(int soundId);
    void UpdateElectricShockPresentation(int counter) { }
    bool TryInteract(Player player);
    bool TrySecondaryInteract(Player player);
    bool TryUseBomb(Player player) => false;
    bool UpdateBomb(
        Player player,
        Vector2 movementInput,
        bool itemButtonJustPressed) => false;
    void InterruptBomb(Player player, bool discard) { }
    bool TryUseBracelet(Player player, bool primaryButton);
    bool UpdateBracelet(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld,
        bool itemButtonJustPressed);
    void AdvanceBraceletProjectile();
    void InterruptBracelet(Player player, bool discard);
    int TryUseSeedSatchel(Player player);
    PegasusSeedState? Pegasus => null;
    bool SeedShooterActive => false;
    int SeedShooterAngle => 0;
    bool TryBeginSeedShooter(
        Player player, bool primaryButton, Vector2 movementInput) => false;
    bool UpdateSeedShooter(
        Player player, Vector2 movementInput,
        bool primaryHeld, bool secondaryHeld,
        bool directionJustPressed) => false;
    void InterruptSeedShooter() { }
    bool SwitchHookActive => false;
    bool BoomerangParentActive => false;
    int BoomerangParentSlot => 0;
    int BoomerangParentGraphic => 0;
    bool TryBeginBoomerang(Player player, int parentSlot) => false;
    void UpdateBoomerangParent() { }
    void ClearBoomerangParent() { }
    bool SomariaActive => false;
    int SomariaAnimationMode => 0;
    int SomariaAnimationFrame => 0;
    void BeginSomaria(Player player,bool underwater) { }
    void UpdateSomariaParent() { }
    void CancelSomaria() { }
    bool SwitchHookExchangeActive => false;
    bool TryBeginSwitchHook(Player player, Vector2 input) => false;
    void UpdateSwitchHookParent(Player player) { }
    void InterruptSwitchHook(bool discard) { }
    void ClearItemParents(Player player) { }
    int BeginHarp(Player player) => 0;
    int BeginFlute(Player player) => 0;
    void AdvanceHarp(Player player, int actionUpdate) { }
    void CompleteHarp(Player player, int song) { }
    void CancelHarp() { }
    bool DigWithShovel(Vector2 point, Vector2I direction);
    bool Collides(Vector2 playerPosition);
    Vector2 ResolveMovement(Vector2 playerPosition, Vector2 movement, bool allowWallSlide);
    Vector2 ResolveNativeMovement(Vector2 position, int speed, int angle, bool allowWallSlide) =>
        (angle & 0x80) != 0 ? Vector2.Zero :
        ResolveMovement(position, OracleObjectMovement.Shared.Delta(speed, angle), allowWallSlide);
    bool IsPushingAgainstWall(
        Vector2 playerPosition,
        Vector2I facing,
        Vector2 movementInput);
    void UpdatePushableBlocks(
        Vector2 playerPosition,
        Vector2I facing,
        Vector2 movementInput);
    ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition);
    SideScrollTerrainState GetSideScrollTerrain(Vector2 playerPosition);
    int GetAdjacentWallsBitset(Vector2 playerPosition);
    bool SideScrollTileBlocksPoint(Vector2 point) => false;
    Vector2 GetTerrainPush(Vector2 playerPosition);
    bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement);
    bool ApplyLandedTileHit(Vector2 playerPosition);
    void BeginLedgeScreenTransition(Player player);
    void ResumeLedgeHopAfterScroll(Player player);
    void SpawnDrowningSplash(Vector2 position, HazardType hazard);
    void SpawnSideScrollBubble(Vector2 position);
    void BeginFallDownHoleWarp(Player player, int packedPosition) =>
        throw new NotSupportedException(
            "This player world does not support dungeon warphole descents.");
    void DeactivateWarpAtPlayerPosition(Player player);
    bool CheckTileWarp(Player player);
    void CheckRoomExit(Player player);
}
