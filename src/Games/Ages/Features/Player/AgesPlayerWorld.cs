using Godot;
using System;

namespace Oracles;

public sealed class AgesPlayerWorld : PlayerWorld, IPlayerWorld
{
    private string AssetRoot => _rooms.AssetRoot;

    private readonly AgesRoomTransitionController _transitions;
    private readonly InteractionController _interactions;
    private readonly OverworldKeyholeController _keyholes;
    private readonly SeedShooterController _seedShooter;
    private readonly RoomEventController _roomEvents;
    private readonly InventoryState _inventory;

    public void UpdateElectricShockPresentation(int counter) =>
        _entities.UpdateElectricShockPresentation(counter);
    public bool RequestDeepWaterDive(Player player, int packedPosition) =>
        _transitions.RequestDeepWaterDive(player, packedPosition);
    public bool RequestUnderwaterSurface(Player player, ActiveTerrainInfo terrain) =>
        _transitions.RequestUnderwaterSurface(player, terrain);
    public bool PassesNpcs => _transitions.TimeWarpDestinationActive || _entities.PlayerPassesNpcs;
    public bool InteractionMenusDisabled => _entities.PlayerMenusDisabled || _roomEvents.MenusDisabled;
    public bool DialogueOpen => _interactions.DialogueOpen;
    public bool NativeTextActive => _entities.TextActiveSource();
    public bool ItemUsageDisabled => _entities.PlayerItemUsageDisabled;
    // Room scripts' $81 mask also freezes Link and item parents. Keep their
    // state intact while interactions (including the key sprite) keep running.
    public bool PlayerUpdatesFrozen => _entities.PlayerUpdatesFrozen || _roomEvents.FreezesNonInteractionObjects;
    public bool NativePaletteChanging => _entities.DoorPaletteFadeActive;
    public bool LinkDisabled => _roomEvents.DisablesLink;
    public override bool MovementDisabled => base.MovementDisabled || _roomEvents.Active;
    public byte NativeObjectDisableMask => _entities.RuntimeState.ReadField("wDisabledObjects");
    public int RaisedFloorOffset
    {
        get => unchecked((sbyte)_entities.RuntimeState.ReadField("wLinkRaisedFloorOffset"));
        set => _entities.RuntimeState.SetField("wLinkRaisedFloorOffset", unchecked((byte)value));
    }
    public bool GaleWarpDisabled => _entities.WarpTilesDisabled ||
        _entities.RuntimeState.ReadField("wWarpsDisabled") != 0 ||
        _entities.PlayerMenusDisabled || _roomEvents.MenusDisabled || _roomEvents.Active;
    public bool PlayerContactDisabled => _transitions.TimeWarpActive || _entities.PlayerContactDisabled;
    public bool NativeWarpsDisabled =>
        _entities.RuntimeState.ReadField("wWarpsDisabled") != 0;
    public void SetNativeWarpsDisabled(bool disabled) =>
        _entities.RuntimeState.SetField("wWarpsDisabled", disabled ? (byte)1 : (byte)0);
    public void RequestWallmasterReturn()
    {
        var profile = EnemyBehaviorTables.For(AssetRoot).Floormaster;
        _entities.OnRoomWarpRequested(new Warp(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, -1, 0,
            WarpSourceTransition.FadeOut, _rooms.ActiveGroup,
            _entities.RuntimeState.ReadField("wDungeonWallmasterDestRoom"), profile.ReturnPosition,
            0, profile.ReturnTransition, DirectFadeOut: true)); // wWarpDestTransition2=$03.
    }
    public bool SeedShooterActive => _seedShooter.ShooterActive;
    public int SeedShooterAngle => _seedShooter.ShooterAngle;
    public bool SwitchHookActive => _entities.SwitchHook?.Active == true;
    protected override bool BoomerangBlockedByGame(Player player) =>
        Underwater || SideScrolling && _inventory.HasTreasure(TreasureId.MermaidSuit) &&
        (SideScrollActiveTiles.ActiveType & SideScrollTileType.Water) != 0 || SwitchHookActive;
    protected override bool BombchuBlockedByGame(Player player) =>
        Underwater || SideScrolling && _inventory.HasTreasure(TreasureId.MermaidSuit) &&
        (SideScrollActiveTiles.ActiveType & SideScrollTileType.Water) != 0 ||
        _entities.RaftSpecialObjectPresent;
    public bool SomariaActive => _entities.Somaria?.Active==true;
    public bool BiggoronActive => _entities.Biggoron?.Active==true;
    public void BeginBiggoron(Player player)
    {
        Bombs.Interrupt(player,discard:false); Bracelet.ClearParent(player);
        _seedShooter.InterruptShooter(); _entities.SwitchHook?.ClearParent(); CancelSomaria();
        _entities.Biggoron!.Begin(player);
    }
    public void UpdateBiggoronParent(bool prohibited) => _entities.Biggoron?.UpdateParent(prohibited);
    public void CancelBiggoron() => _entities.Biggoron?.Cancel();
    public (Texture2D Texture, Vector2 Offset)? BiggoronLinkBody(Player player, bool damagePalette) =>
        _entities.Biggoron?.LinkBody(player, damagePalette);
    public int SomariaAnimationMode => _entities.Somaria?.Parent?.Mode??0;
    public int SomariaAnimationFrame => _entities.Somaria?.Parent?.Frame??0;
    public void BeginSomaria(Player player,bool underwater) => _entities.Somaria!.Begin(player,underwater);
    public void UpdateSomariaParent() => _entities.Somaria?.UpdateParent();
    public void CancelSomaria() => _entities.Somaria?.Cancel();
    public bool SwitchHookExchangeActive => _entities.SwitchHook?.ExchangeActive == true;
    public bool TryBeginSwitchHook(Player player, Vector2 input)
    {
        CancelSomaria();
        return _entities.SwitchHook?.TryBegin(player, input)==true;
    }
    public void UpdateSwitchHookParent(Player player) => _entities.SwitchHook?.UpdateParent(player);
    public void InterruptSwitchHook(bool discard) => _entities.SwitchHook?.Interrupt(discard);
    public bool Underwater =>
        (_terrain.CurrentTilesetFlags & (int)TilesetFlags.Underwater) != 0;
    public void CreateBabyTransformationPoof(Vector2 position, int z) => _entities.TryCreateBabyTransformationPoof(position, z);

    public AgesPlayerWorld(
        AgesRoomTransitionController transitions,
        InteractionController interactions,
        RoomCollision collision,
        OverworldKeyholeController keyholes,
        CombatController combat,
        RoomEntityManager entities,
        SeedShooterController seedShooter,
        HarpController harp,
        RoomEventController roomEvents,
        InventoryState inventory,
        OracleSoundEngine sound,
        Func<bool> collisionsDisabled,
        RoomSession rooms, GameplaySceneGraph scene)
        : base(rooms, transitions, entities, collision, sound, collisionsDisabled, combat, scene, harp)
    {
        _transitions = transitions;
        _interactions = interactions;
        _keyholes = keyholes;
        _seedShooter = seedShooter;
        _roomEvents = roomEvents;
        _inventory = inventory;
    }

    public bool TryInteract(Player player) => _interactions.TryInteract(player);
    public bool TrySecondaryInteract(Player player) =>
        _interactions.TrySecondaryInteract(player);
    public override bool TryUseBomb(Player player)
    {
        CancelSomaria();
        return base.TryUseBomb(player);
    }
    public override bool TryUseBracelet(Player player, bool primaryButton)
    {
        CancelSomaria();
        return base.TryUseBracelet(player, primaryButton);
    }
    public bool TryBeginSeedShooter(
        Player player, bool primaryButton, Vector2 movementInput)
    {
        if(BiggoronActive) return false;
        CancelSomaria();
        return _seedShooter.TryBeginShooter(player, primaryButton, movementInput);
    }
    public bool UpdateSeedShooter(
        Player player, Vector2 movementInput,
        bool primaryHeld, bool secondaryHeld,
        bool directionJustPressed) =>
        _seedShooter.UpdateShooter(
            player, movementInput, primaryHeld, secondaryHeld,
            directionJustPressed);
    public void InterruptSeedShooter() => _seedShooter.InterruptShooter();
    public override void ClearItemParents(Player player)
    {
        base.ClearItemParents(player);
        _seedShooter.InterruptShooter();
        _entities.SwitchHook?.ClearParent();
        _entities.Somaria?.ClearParent();
        _entities.Biggoron?.ClearParent();
    }
    protected override bool UpdateNativeTileContact(byte handler,Vector2 position,Vector2I facing,Vector2 input) =>
        _keyholes.UpdatePushAttempt(position,facing,input);
    public void BeginFallDownHoleWarp(Player player, int packedPosition) =>
        _transitions.ApplyDungeonHoleWarp(player, packedPosition);
    public void DeactivateWarpAtPlayerPosition(Player player) =>
        _transitions.DeactivateWarpAtPlayerPosition(player);
    public bool CheckTileWarp(Player player)
    {
        if (RidingObject) return false;
        if (!player.ApplicationUpdateOwned)
            return _transitions.CheckTileWarp(player);
        // cutscene01 calls func_60e9 after all objects and toggle selection.
        // Record eligibility here, then inspect the final position there.
        _transitions.RequestTileWarpCheck();
        return false;
    }
    public void CheckRoomExit(Player player)
    {
        // The application samples screenTransitionState2 once, after objects.
        // Standalone player updates retain this route for their host.
        if (!player.ApplicationUpdateOwned)
            _transitions.CheckRoomExit(player);
    }
}
