
namespace Oracles;

// Content bindings supply interactions and transitions; movement and combat
// share the same room, save, collision and entity owners.
public abstract class PlayerWorld
{
    private InteractableTileDatabase? _interactableTilesSourceCache;
    private InteractableTileDatabase _interactableTiles => _interactableTilesSourceCache ??= new(_rooms.AssetRoot);
    internal InstrumentController Instruments { get; }
    public int PlayingInstrument => Instruments.PlayingInstrument;
    public int BeginHarp(Player player) => Instruments.TryStartHarp(player);
    public int BeginFlute(Player player) => Instruments.TryStartFlute(player);
    public void AdvanceHarp(Player player,int actionUpdate) => Instruments.Advance(player,actionUpdate);
    public void CompleteHarp(Player player,int song) => Instruments.Complete(player,song);
    public void CancelHarp() => Instruments.Cancel();
    protected readonly RoomSession _rooms;
    protected readonly RoomEntityManager _entities;
    protected readonly RoomCollision _collision;
    protected readonly OracleSoundEngine _sound;
    protected readonly Func<bool> _collisionsDisabled;
    protected readonly CombatController _combat;
    protected readonly TerrainController _terrain;
    public bool NativeWarpsDisabled => _entities.RuntimeState.ReadField("wWarpsDisabled")!=0;
    public void SetNativeWarpsDisabled(bool disabled) =>
        _entities.RuntimeState.SetField("wWarpsDisabled",disabled?(byte)1:(byte)0);
    public void RequestWallmasterReturn()
    {
        var profile=FloormasterDatabase.For(_rooms.AssetRoot);
        _entities.OnRoomWarpRequested(new Warp(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,-1,0,
            WarpSourceTransition.FadeOut,_rooms.ActiveGroup,_entities.RuntimeState.ReadField("wDungeonWallmasterDestRoom"),
            profile.ReturnPosition,0,profile.ReturnTransition,DirectFadeOut:true));
    }
    private readonly RoomTransitionController _roomTransitions;
    internal TerrainController Terrain => _terrain;
    internal RoomTransitionController Transitions => _roomTransitions;
    internal CombatController Combat => _combat;
    private BombController? _bombs;
    internal BombController Bombs => _bombs ??= new(
        _entities.Inventory ?? throw new InvalidOperationException("Bomb input requires live inventory."),
        _entities,_rooms,_sound.PlaySound);
    public bool BombParentActive => Bombs.Active;
    public virtual bool TryUseBomb(Player player) => Bombs.TryUse(player);
    public bool UpdateBomb(Player player, Vector2 movementInput, bool itemButtonJustPressed) =>
        Bombs.Update(player,movementInput,itemButtonJustPressed);
    public void InterruptBomb(Player player, bool discard) => Bombs.Interrupt(player,discard);
    internal BraceletController Bracelet { get; }
    public bool BraceletParentActive => Bracelet.State is not (BraceletState.Idle or BraceletState.Projectile);
    public virtual bool TryUseBracelet(Player player, bool primaryButton) => Bracelet.TryUse(player, primaryButton);
    public bool UpdateBracelet(Player player, Vector2 movementInput, bool primaryHeld, bool secondaryHeld, bool itemButtonJustPressed) =>
        Bracelet.Update(player, movementInput, primaryHeld, secondaryHeld, itemButtonJustPressed);
    public void InterruptBracelet(Player player, bool discard) => Bracelet.Interrupt(player, discard);
    internal ShovelController Shovel { get; }
    internal SeedSatchelController Satchel { get; }
    public int TryUseSeedSatchel(Player player) => Satchel.TryUse(player);
    public PegasusSeedState Pegasus => Satchel.Pegasus;
    public bool DigWithShovel(Vector2 point, Vector2I direction) => Shovel.CreateChild(point, direction);
    public bool ShovelChildActive => Shovel.ChildActive;
    protected virtual bool BombchuBlockedByGame(Player player) => false;
    public bool BombchuParentActive => _entities.BombchuParent.Active && !_entities.BombchuParent.Pending;
    public bool BombchuParentAllocated => _entities.BombchuParent.Active;
    public int BombchuParentSlot => _entities.BombchuParent.Slot;
    public int BombchuParentGraphic => _entities.BombchuParent.Graphic;
    public bool TryBeginBombchu(Player player, int parentSlot)
    {
        _entities.BombchuParent.Reserve(parentSlot);
        return true;
    }
    public bool InitializeBombchuParent(Player player)
    {
        if (!_entities.BombchuParent.Pending) return false;
        if (BombchuBlockedByGame(player) || player.TopDownSwimming || player.SideScrollSwimming ||
            (_entities.Inventory ?? throw new InvalidOperationException("ITEM$0d requires live inventory.")).Bombchus == 0)
        { _entities.BombchuParent.Clear(); return false; }
        _entities.BombchuParent.Begin(_entities.BombchuParent.Slot, player.MinecartRideActive || player.CompanionRideActive, false);
        player.NotifyParentItemAnimationStarted(TreasureId.Bombchus, _entities.BombchuParent.Slot);
        if (!_entities.TryCreateBombchu(player)) _entities.BombchuParent.Clear();
        return true;
    }
    public void UpdateBombchuParent() => _entities.BombchuParent.Update();
    public void ClearBombchuParent() => _entities.BombchuParent.Clear();
    protected virtual bool BoomerangBlockedByGame(Player player) => false;
    public bool BoomerangParentActive => _entities.BoomerangParent.Active;
    public int BoomerangParentSlot => _entities.BoomerangParent.Slot;
    public int BoomerangParentGraphic => _entities.BoomerangParent.Graphic;
    public bool TryBeginBoomerang(Player player, int parentSlot, bool primaryButton)
    {
        if (BoomerangBlockedByGame(player) || player.TopDownSwimming || player.SideScrollSwimming) return false;
        int angle = player.LinkMovementAngle;
        if ((angle & 0x80) != 0) angle = CarriedObjectMotion.DirectionIndex(player.FacingVector) * 8;
        var data = BoomerangDatabase.For(_rooms.AssetRoot);
        int subid = data.SupportsLevel2 && player.Inventory.BoomerangLevel == 2 ? 1 : 0;
        var child = _entities.TryCreateBoomerang(player.PrecisePosition, angle, player.ObjectZHigh, subid, parentSlot,
            subid == 0 ? null : point => _combat.ApplyItemTileHit(point, BoomerangDatabase.For(_rooms.AssetRoot, subid).BreakSource));
        if (child is null) return false;
        _entities.BoomerangParent.Begin(parentSlot, player.MinecartRideActive || player.CompanionRideActive, player.RaftRideActive, child, primaryButton);
        player.NotifyParentItemAnimationStarted(TreasureId.Boomerang, parentSlot);
        return true;
    }
    public bool UpdateBoomerangParent(Player player) => _entities.BoomerangParent.UpdateControl(player);
    public void ClearBoomerangParent() => _entities.BoomerangParent.Clear();
    public virtual void ClearItemParents(Player player)
    {
        Bombs.Interrupt(player, discard: false);
        Bracelet.ClearParent(player);
        ClearBombchuParent();
        ClearBoomerangParent();
    }

    protected PlayerWorld(RoomSession rooms, RoomTransitionController transitions, RoomEntityManager entities,
        RoomCollision collision, OracleSoundEngine sound, Func<bool> collisionsDisabled, CombatController combat,
        GameplaySceneGraph scene, InstrumentController? instruments = null)
    {
        if (scene.AssetRoot != rooms.AssetRoot || rooms.AssetRoot != entities.AssetRoot ||
            !ReferenceEquals(rooms.RuntimeState, entities.RuntimeState) ||
            !ReferenceEquals(rooms.RuntimeState, collision.RuntimeState) || !combat.UsesOwners(rooms,entities) ||
            !transitions.UsesOwners(rooms,entities))
            throw new ArgumentException("Player world, room, collision and entities require one selected pack and live runtime owner.");
        _rooms = rooms;
        _entities = entities;
        _collision = collision;
        _sound = sound;
        _collisionsDisabled = collisionsDisabled;
        _combat = combat;
        _roomTransitions = transitions;
        Instruments = instruments ?? new(rooms,entities,sound,(message,player)=>scene.Dialogue.ShowMessage(message,player.Position.Y));
        entities.PlayingInstrumentSource = () => Instruments.PlayingInstrument;
        var breakables = BreakableTileDatabase.For(rooms.AssetRoot);
        _terrain = new(rooms,breakables,collision.AdjacentWallsBitset);
        Bracelet = new(scene.WorldRoot, rooms, breakables, scene.RoomView,
            entities, combat, rooms.SaveData, sound.PlaySound, entities.AnimationTick, collision.HasFullWall);
        if (entities.Bracelet is not null)
            throw new InvalidOperationException("Player world requires sole ownership of the Bracelet controller.");
        entities.Bracelet = Bracelet;
        Shovel = new(rooms, breakables, scene.RoomView,
            entities, rooms.SaveData, sound.PlaySound, entities.AnimationTick);
        Satchel = new(entities.Inventory ?? throw new InvalidOperationException("Satchel input requires live inventory."),
            entities, new SeedSatchelDatabase(rooms.AssetRoot), rooms);
    }

    public int FrameCounter => _entities.FrameCounter;
    public bool SwordDisabled => _entities.PlayerSwordDisabled || _entities.RuntimeState.ReadField("wccd8") != 0;
    public bool SwordJinxed => _entities.RuntimeState.ReadField("wSwordDisabledCounter") != 0;
    public void AdvanceSwordJinxCounter()
    {
        byte counter = _entities.RuntimeState.ReadField("wSwordDisabledCounter");
        if (counter != 0) _entities.RuntimeState.SetField("wSwordDisabledCounter", (byte)(counter - 1));
    }
    public bool IsTransitioning => _roomTransitions.IsTransitioning;
    public bool ScreenScrolling => _roomTransitions.ScrollActive;
    public bool RoomExitPending => _roomTransitions.RoomExitPending;
    public bool RidingObject => _entities.PlayerRidingObject;
    public Vector2? MountedCompanionPosition => _entities.MountedCompanionPosition;
    public Vector2? MountedRaftPosition => _entities.MountedRaftPosition;
    public bool SideScrolling => (_terrain.CurrentTilesetFlags & (int)TilesetFlags.Sidescroll) != 0;
    public SideScrollPlayerParameters SideScrollParameters => _terrain.SideScrollParameters;
    public SideScrollTerrainState GetSideScrollTerrain(Vector2 position) => _terrain.GetSideScrollTerrain(position);
    public SideScrollActiveTiles SideScrollActiveTiles => new(
        _rooms.RuntimeState.ReadField("wActiveTileIndex"),
        (SideScrollTileType)_rooms.RuntimeState.ReadField("wActiveTileType"),
        (SideScrollTileType)_rooms.RuntimeState.ReadField("wLastActiveTileType"));
    public bool SideScrollTileBlocksPoint(Vector2 point) => _collision.TileBlocksPointForSidePlatform(point);
    public void SpawnSideScrollBubble(Vector2 position) => _entities.TryCreateInteraction(new SideScrollBubbleSpawn(position));
    public bool DeathUpdatesSuspendedByWarp => _roomTransitions.DeathUpdatesSuspendedByWarp;
    public ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    public bool RingTransformationsAllowed => (_terrain.CurrentTilesetFlags & 0x60) == 0 &&
        _entities.RuntimeState.ReadField("wMenuDisabled") == 0 && !_rooms.InShop;
    public bool InShop => _rooms.InShop;
    public void CreateTransformationDust(Vector2 position, int z) => _entities.TryCreateKnockbackDust(position,z);
    public int GetAdjacentWallsBitset(Vector2 position) => _collisionsDisabled() ? 0 : _terrain.GetAdjacentWallsBitset(position);
    public bool TryStartTerrainJump(Player player, Vector2 from, Vector2 movement, int objectAngle) =>
        _terrain.TryStartTerrainJump(player,from,movement,objectAngle);
    public void BeginLedgeScreenTransition(Player player) => _roomTransitions.BeginLedgeScroll(player);
    public void ResumeLedgeHopAfterScroll(Player player) => _terrain.ResumeLedgeHopAfterScroll(player);
    public void CompleteLedgeLanding(Player player)
    {
        ApplyLandedTileHit(player.PrecisePosition);
        _terrain.ApplyLedgeLandingPlacement(player);
    }
    public TerrainInfo GetTerrainAtPosition(Vector2 position) => _rooms.CurrentRoom.GetTerrainInfo(position);
    public int ActiveRoomGroup => _rooms.ActiveGroup;
    // link.s:linkState01 gates normal Link with $81, independently of other categories.
    public virtual bool MovementDisabled => (_entities.RuntimeState.ReadField("wDisabledObjects") & 0x81) != 0 ||
        _entities.PlayerMovementDisabled;
    public bool FeatherBlockedByMagnet => _entities.RuntimeState.ReadField("wMagnetGloveState") != 0;
    public void PlaySound(int soundId) => _sound.PlaySound(soundId);
    public void RestartNativeSound()
    {
        int room = _sound.NativeRoomMusic, active = _sound.NativeActiveMusic;
        _sound.RestartSound();
        _sound.SetNativeMusicSelection(room, active);
    }
    public void BeginNativeWhiteFade(bool fadeOut, int delay) => _roomTransitions.BeginNativeWhiteFade(fadeOut, delay);
    public bool ApplySwordHit(Player player, Rect2 hitbox) => _combat.ApplySwordHit(player,hitbox);
    public bool ApplySwordTileHit(Player player, int direction, bool swordPoke) => _combat.ApplySwordTileHit(player,direction,swordPoke);
    public bool ApplyExpertsRingTileHit(Player player, int direction) => _combat.ApplyExpertsRingTileHit(player,direction);
    public bool ApplyLandedTileHit(Vector2 position) => _combat.ApplyLandedTileHit(position);
    public bool TryCreateSwordBeam(Player player, int direction) => _entities.TrySpawnSwordBeam(player.Position,direction);
    public void SpawnDrowningSplash(Vector2 position, HazardType hazard)
    {
        if (hazard is not (HazardType.Water or HazardType.Lava))
            throw new ArgumentOutOfRangeException(nameof(hazard));
        _entities.TryCreateSplash(position,hazard);
    }
    public int TilePushingDirection
    {
        get => _entities.RuntimeState.ReadField("wLinkPushingDirection");
        set => _entities.RuntimeState.SetField("wLinkPushingDirection", unchecked((byte)value));
    }
    public void UpdateLinkOnChest() => _collision.UpdateLinkOnChest();

    public bool UpdatePushableBlocks(
        Vector2 position,
        Vector2I facing,
        Vector2 movementInput)
    {
        var blocks = _entities.RequiredReservedPushBlock;
        var keys = _entities.ReservedKeyDoor ?? throw new InvalidOperationException("Tile contacts require their native key-door owner.");
        var inventory = _entities.Inventory ?? throw new InvalidOperationException("Tile contacts require live inventory.");
        Vector2 resolvedInput = _collisionsDisabled()
            ? Vector2.Zero
            : movementInput;
        // interactWithTileBeforeLink consumes the preceding graphics pass's
        // wLinkPushingDirection, before this update's wall probes/movement.
        // Current wLinkAngle rejects diagonals/neutral, but need not match
        // the retained push direction until the new graphics publication.
        Vector2 tileInput = TilePushingDirection is >= 0 and < 4 &&
            InteractableTilePushGeometry.TryGetCardinalInput(resolvedInput, out _)
                ? OracleObjectMath.StrictCardinalVector(TilePushingDirection * 8) : Vector2.Zero;
        _entities.UpdatePushableEntities(position, facing, resolvedInput);
        OracleRoomData room = _rooms.CurrentRoom;
        byte tile = room.GetMetatile(position + InteractableTilePushGeometry.FrontTileOffset(facing));
        byte parameter = _interactableTiles.Parameter(room.ActiveCollisions,tile);
        bool keyholeReturned = false;
        if (parameter == InteractableTileDatabase.NoEntry)
            _rooms.TilePushCounter = 20;
        else switch (parameter & 15)
        {
            case 0:
                blocks.UpdatePushAttempt(position,facing,tileInput,inventory.BraceletLevel);
                break;
            case 1:
            case 2:
                keys.UpdatePushAttempt(position,facing,tileInput);
                break;
            case 3:
                UpdateTileInfoContact(position,tileInput,parameter,inventory);
                break;
            case 4:
            case 5:
                // Chest/sign A handling belongs to TryInteract. Without A,
                // these source handlers return without touching $cc6a.
                break;
            case 6:
            case 7:
            case 8:
                keyholeReturned = UpdateNativeTileContact((byte)(parameter & 15),position,facing,tileInput);
                break;
        }
        return keyholeReturned || _entities.TextActiveSource();
    }

    private void UpdateTileInfoContact(Vector2 position,Vector2 input,byte parameter,InventoryState inventory)
    {
        if (!InteractableTilePushGeometry.TryGetCardinalInput(input,out _) ||
            !InteractableTilePushGeometry.IsAlignedForPush(position))
        { _rooms.TilePushCounter = 20; return; }
        _rooms.TilePushCounter = unchecked((byte)(_rooms.TilePushCounter - 1));
        if (_rooms.TilePushCounter != 0) return;
        _rooms.TilePushCounter = 20;
        int textId = (parameter >> 4) switch
        {
            0 => inventory.HasTreasure(TreasureId.Bracelet) ? 0 : 0x5103,
            1 => 0x5105,
            2 => 0x5106,
            3 => 0x5108,
            4 => 0x5104,
            _ => throw new NotSupportedException($"nextToTileWithInfoText: unsupported parameter ${parameter:x2}.")
        };
        if (textId != 0) _entities.RequiredReservedPushBlock.RequestTileInfo(textId);
    }

    protected virtual bool UpdateNativeTileContact(byte handler,Vector2 position,Vector2I facing,Vector2 input) =>
        throw new NotSupportedException($"code/interactableTiles.s: native handler${handler:x2} in {_rooms.SaveData.Game} room${_rooms.ActiveGroup:x2}:${_rooms.CurrentRoom.Id:x2} has no content binding.");

    public byte UpdateActiveLinkTile(Vector2 position, bool airborne) => _collision.UpdateActiveLinkTile(position,airborne);
    public void UpdateSideScrollActiveTile(Vector2 position) => _collision.UpdateSideScrollActiveTile(position);
    public void SetLinkTerrainTile(byte position, byte tile)
    {
        if (_rooms.TrySetTile(position,tile)) _entities.OnRoomTileChanged();
    }
    public void BreakCrackedFloor(byte position, byte tile) => _entities.BreakCrackedFloor(_rooms,position,tile);
    public bool Collides(Vector2 position) => !_collisionsDisabled() && _collision.Collides(position);
    public Vector2 ResolveMovement(Vector2 position, Vector2 movement, bool allowWallSlide) =>
        _collisionsDisabled() ? movement : _collision.ResolveMovement(position, movement, allowWallSlide);
    public Vector2 ResolveNativeMovement(Vector2 position, int speed, int angle, bool allowWallSlide, int? adjacentWalls = null)
    {
        if ((angle & 0x80) != 0) return Vector2.Zero;
        if (!_collisionsDisabled()) return _collision.ResolveNativeMovement(position, speed, angle, allowWallSlide, adjacentWalls);
        var velocity = NativeObjectMovement.Velocity(_entities.RuntimeState, speed, angle);
        return new(velocity.XFixed / 256.0f, velocity.YFixed / 256.0f);
    }
    public Vector2 ApplyNativeUnblockedMovement(Vector2 position, int speed, int angle)
    {
        NativeObjectMovement.ApplySpeed(_entities.RuntimeState, ref position, speed, angle);
        return position;
    }
}
