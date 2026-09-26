using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Owns the active and outgoing room-entity sets. Per-type behavior is exposed
/// through small capability interfaces and constructed by RoomEntityFactory.
/// </summary>
public sealed class RoomEntityManager : IDisposable
{
    private readonly DiggingEnemyDatabase _diggingEnemies = new();
    private readonly MovingPlatformRidingState _platformRiding = new();
    private readonly List<INativeBraceletRoomEntity> _grabbableObjects = new(8);
    internal bool ReservedBraceletChildActive => _activeEntities.OfType<IBraceletChildRoomEntity>()
        .Any(child => child.ReservedBraceletChildActive);
    internal void ClearSignalsAfterPlayer()
    {
        _grabbableObjects.Clear();
        foreach (var entity in _activeEntities.OfType<IAfterPlayerUpdateRoomEntity>())
            entity.AfterPlayerUpdate();
    }
    private void PublishGrabbableObject(INativeBraceletRoomEntity entity)
    {
        if (_grabbableObjects.Count < 8) _grabbableObjects.Add(entity);
    }
    internal void UpdateHeldObjectPosition(Player player)
    {
        foreach (var entity in _activeEntities.OfType<INativeBraceletRoomEntity>())
            entity.UpdateHeldPosition(player);
    }
    internal void RequestPegasusDust(PegasusSeedState pegasus, bool signal)
    {
        var dust = _activeEntities.OfType<PegasusDustRoomEntity>().FirstOrDefault(d => !d.Finished);
        if (dust is null) { dust = new PegasusDustRoomEntity(pegasus); AddEntity(dust); }
        if (signal) dust.Signal();
    }
    internal void RequestSound(int sound) => OnSoundRequested(sound);
    internal PushBlockController? ReservedPushBlock { get; set; }
    internal DungeonKeyDoorController? ReservedKeyDoor { get; set; }
    internal bool DoorPaletteFadeActive => PaletteFadeActiveSource() ||
        _activeEntities.OfType<DarkRoomHandlerRoomEntity>().Any(handler => handler.State.FadeActive);
    private bool TryCreateSynchronizedBlock(byte position,int angle)
    {
        if (FindFreeInteractionSlot() < 0) return false;
        var actor = (ReservedPushBlock ?? throw new InvalidOperationException("INTERAC $bd has no reserved $14 owner."))
            .CreateSynchronizedController();
        actor.Name = $"SynchronizedPushBlock_{position:x2}";
        AddEntity(new SynchronizedPushBlockRoomEntity(actor,position,angle,() => _inventory?.BraceletLevel ?? 0));
        return true;
    }
    private readonly Dictionary<IRoomEntity, int> _enemySlots = new();
    private readonly DynamicItemSlotPool _dynamicItems = new();
    internal bool DynamicItemSlotAvailable => _dynamicItems.FindFree() >= 0;
    internal int DynamicItemSlotOf(IRoomEntity entity) => _dynamicItems.SlotOf(entity);
    internal bool TryAllocateSwitchHookChain(SwitchHookItem owner) =>
        _dynamicItems.TryAllocate(owner,0x0b,()=>SwitchHook?.Item==owner && owner.ChainAllocated)>=0;

    private static int DynamicItemId(IRoomEntity entity) => entity switch
    {
        EmberSeedRoomEntity { IsFlamePart:true } => -1,
        ISeedProjectileRoomEntity seed => seed.SeedItem,
        SwordBeamRoomEntity => 0x27,
        SomariaBlockRoomEntity => 0x18,
        BoomerangRoomEntity => 0x06,
        _ when entity.Node is BombEffect => 0x03,
        _ => -1
    };
    private static bool ItemSetupPending(IRoomEntity entity) => entity.Node switch
    {
        BombEffect bomb => bomb.SetupPending,
        SwordBeamEffect beam => !beam.Initialized,
        EmberSeedEffect seed => seed.State == EmberState.Initializing,
        SomariaBlock block => block.State == 0,
        BoomerangItem boomerang => boomerang.State == 0,
        _ => false
    };
    private readonly HashSet<int> _reservedEnemySlots = new();
    private readonly HashSet<int> _unownedOutgoingEnemySlots = new();
    private readonly byte[] _deletedEnemyCounter1 = new byte[16];
    private readonly HashSet<IRoomEntity> _updatedEntitiesThisFrame = new();
    private readonly HashSet<IRoomEntity> _specialObjectsUpdatedBeforePlayer = new();

    private void RegisterEnemySlot(IRoomEntity? entity, int slot)
    {
        if (slot is < 0 or >= 16 || _reservedEnemySlots.Contains(slot))
            throw new InvalidOperationException($"getFreeEnemySlot: duplicate or invalid enemy slot ${slot:x2}.");
        if (_deletedEnemyCounter1[slot] != 0)
        {
            if (entity is not INativeEnemyCounter1RoomEntity counter)
                throw new NotSupportedException($"ENEMY slot ${slot:x2} retains counter1=${_deletedEnemyCounter1[slot]:x2} " +
                    $"from fireballShooter_state9 after enemyDelete; {entity?.GetType().Name ?? "unsupported enemy"} does not represent that initial byte.");
            counter.Counter1 = _deletedEnemyCounter1[slot];
            _deletedEnemyCounter1[slot] = 0;
        }
        _reservedEnemySlots.Add(slot);
        if (entity is not null)
        {
            _enemySlots.Add(entity, slot);
            if (entity is INativeEnemySlotRoomEntity native) native.BindEnemySlot(slot, ResolveEnemySlot);
        }
    }

    private IRoomEntity? ResolveEnemySlot(int slot)
    {
        var entity = _enemySlots.FirstOrDefault(pair => pair.Value == slot).Key;
        return entity is IRoomEntityLifetime { Finished: true } ? null : entity;
    }

    private int FindFreeEnemySlot() => Enumerable.Range(0,16)
        .FirstOrDefault(slot => !_reservedEnemySlots.Contains(slot), -1);

    private bool MaplePresent() => _activeEntities.Any(
        entity => entity is MapleEncounterRoomEntity { Finished: false });

    private void SpawnDiggingEnemy(int roll, int subId, Vector2 position)
    {
        if (_runtimeState.ReadWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress) != 0)
            return;
        TryAllocateEnemy(_ => _factory.CreateDiggingEnemy(
            _diggingEnemies.Enemy(roll, subId), position, _roomForActiveEntities,
            _diggingEnemies.BeetleCounters));
    }

    // getFreeEnemySlot[_uncounted] scans the live pool. In particular, a
    // later slot freed during this pass cannot satisfy an earlier request.
    internal IRoomEntity? TryAllocateEnemy(Func<int, IRoomEntity> create)
    {
        for (int slot = 0; slot < 16; slot++)
        {
            if (_reservedEnemySlots.Contains(slot))
                continue;
            IRoomEntity entity = create(slot);
            RegisterEnemySlot(entity, slot);
            AddEntity(entity);
            return entity;
        }
        return null;
    }
    public event Action<int, OracleRoomData>? RoomEntitiesLoaded;
    public event Action<TimePortal>? TimePortalEntered;
    internal event Action<int, string>? DungeonEntranceTriggered;
    internal event Action<Warp>? RoomWarpRequested;
    internal event Action<GroundTreasurePickup, Player>? GroundTreasureCollected;
    internal event Action<
        GroundTreasurePickup,
        TreasureObjectRecord,
        Player>? GroundTreasureDialogueRequested;
    internal event Action<GashaSpotInteraction, Player>? GashaInteractionRequested;
    internal event Action<GashaSpotInteraction, Player>? GashaNutCaught;
    internal event Action<Vector2, HazardType>? ItemDropEnteredHazard;
    internal event Action<ObjectFellInHoleKind>? ObjectFellInHole;
    internal event Action<DungeonEssence, Player>? DungeonEssenceTriggered;
    internal event Action<int, string, Vector2>? RoomEntityDialogueRequested;
    internal event Action<int, string, Player>? MapleDialogueRequested;
    internal event Action<MapleItemRecord, Player>? MapleItemCollected;
    internal event Action<int, string, Vector2>? SeedTreeMessageRequested;
    internal event Action<int, string, Vector2>? OwlStatueMessageRequested;
    public event Action<int>? SoundRequested;
    internal event Action<int, byte>? NativeChannelVolumeWritten;
    public event Action<int, int>? RoomMusicRequested;
    public event Action? RoomTileChanged;
    public event Action? EnemyDefeated;
    public event Action<Vector2>? ScreenShakeChanged;
    private readonly Node _worldRoot;
    private readonly RoomEntityFactory _factory;
    private readonly OracleRandom _random;
    private readonly ItemDropDatabase _itemDrops;
    private readonly OracleSaveData? _saveData;
    private readonly InventoryState? _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly OracleRuntimeState _runtimeState;
    private readonly Func<long> _animationTick;
    private readonly BipinBlossomFamilyStateResolver _familyState;
    private readonly RecentEnemyDefeats _recentEnemyDefeats = new();
    private byte _activeTriggers;
    private readonly NpcVisibilityRuleDatabase _npcVisibility = new();
    private readonly NpcDialogueRuleDatabase _npcDialogue = new();
    private readonly NpcPositionRuleDatabase _npcPositions = new();
    private readonly List<IRoomEntity> _activeEntities = new();
    private readonly List<IRoomEntity> _outgoingEntities = new();
    private readonly List<RoomEntitySpawn> _pendingSpawns = new();
    private readonly Dictionary<IRoomEntity, int> _partSlots = new();
    // enemyCreateDeathPuff leaves wNumEnemies unreleased if PART allocation
    // fails. No live entity then owns that count; room loading clears it.
    private int _unreleasedEnemyCounts;
    private readonly Dictionary<IRoomEntity, int> _interactionSlots = new();
    // Writes to disabled interaction pages survive getFreeInteractionSlot,
    // which only sets enabled. Transfer ownership to the next live actor.
    private readonly byte[] _inactiveInteractionCounter2 = new byte[16];
    private readonly Dictionary<NpcCharacter, INpcTalkLifecycle>
        _npcTalkLifecycles = new(ReferenceEqualityComparer.Instance);
    private Warp? _pendingRoomWarp;
    private OracleRoomData _roomForActiveEntities = null!;
    private bool _screenTransitionActive;
    private double _enemyFrameAccumulator;
    private double _screenTransitionFrameAccumulator;
    private int _enemyFrameCounter;
    private int _screenShakeCounter;
    private int _horizontalScreenShakeCounter;
    private int _screenShakeMagnitude;
    private bool _linkCollisionsAndMenuDisabled;
    private bool _smogLinkAndMenuLocked;
    private readonly BossShutterSignal _bossShutterSignal = new();
    internal byte BossEntrySignal => _bossShutterSignal.Value;
    private bool _disposed;
    private Color[,]? _preShockBackgroundPalettes;
    private IReadOnlyDictionary<int, Color[]>? _activeObjectPaletteOverride;

    internal void UpdateElectricShockPresentation(int counter)
    {
        if (counter == 0x2d)
            _preShockBackgroundPalettes = _roomForActiveEntities.BackgroundPalettes.Capture();
        if (counter != 0 && (counter & 7) != 0) return;
        if (_preShockBackgroundPalettes is null) return;
        if ((counter & 8) == 0)
        {
            _roomForActiveEntities.BackgroundPalettes.Restore(_preShockBackgroundPalettes);
            _activeObjectPaletteOverride = null;
        }
        else
        {
            _roomForActiveEntities.BackgroundPalettes.Restore(ElectricShockPalette.Background);
            _activeObjectPaletteOverride = ElectricShockPalette.Objects;
            BeginScreenShake(8);
        }
        foreach (IRoomEntity entity in _activeEntities) ApplyObjectPaletteOverride(entity);
        if (counter == 0) _preShockBackgroundPalettes = null;
    }

    private void ApplyObjectPaletteOverride(IRoomEntity entity)
    {
        if (entity.Node is EnemyCharacter enemy)
            enemy.Animation.SetPaletteOverride(_activeObjectPaletteOverride);
        else if (entity.Node is ZoraFireProjectile fire)
            fire.SetPaletteOverride(_activeObjectPaletteOverride);
    }

    internal Func<bool> GameButtonJustPressedSource { get; set; } =
        ReadGameButtonJustPressed;
    internal Func<bool> GroundTreasureCollectionAllowed { get; set; } =
        static () => true;
    internal Func<Vector2, Vector2> WorldToScreen { get; set; } =
        static position => position;
    internal Func<bool> TextActiveSource { get; set; } =
        static () => false;
    internal Func<bool> PaletteFadeActiveSource { get; set; } =
        static () => false;
    internal Func<bool> NonInteractionObjectsDisabledSource { get; set; } =
        static () => false;
    // wDisabledObjects=$1e uses the same initialization/always-update
    // dispatch branches as text, without making dialogue active.
    internal Func<bool> InitializedObjectsDisabledSource { get; set; } =
        static () => false;
    internal Func<int> DisplayedHealthSource { get; set; } =
        static () => throw new InvalidOperationException("ENEMY_GREAT_FAIRY $38 requires the status-bar health owner.");
    internal Func<int> PlayingInstrumentSource { get; set; } =
        static () => 0;

    public bool ScreenTransitionActive => _screenTransitionActive;
    internal SwitchHookController? SwitchHook { get; set; }
    internal SomariaController? Somaria { get; set; }
    internal BoomerangParent BoomerangParent { get; } = new();
    internal DungeonToggleController? FloorToggle { get; set; }
    private readonly HashSet<IRoomEntity> _deferredSwitchHookContacts = [];
    private readonly Dictionary<IRoomEntity, Func<bool>> _postObjectMeleeHits = [];
    private Func<IRoomEntity, Func<bool>?>? _postObjectMeleeHitFactory;
    private readonly List<(Rect2 Bounds, int Z, int Radius, int Damage)> _postObjectThrownHits = [];
    internal void ResolvePostObjectCollisions(Player player)
    {
        if (!_screenTransitionActive && !TextActiveSource() && !RoomEntityFreezeActive())
        {
            foreach (var source in _activeEntities.OfType<IReservedBraceletCollisionRoomEntity>().ToArray())
                if (source.TryGetReservedBraceletCollision(out var collision))
                    ApplyThrownObjectHit(collision.Bounds, collision.Z, collision.Radius, collision.Damage);
            foreach (var entity in _activeEntities.OrderBy(EntityPhase)
                .ThenBy(entity => _enemySlots.TryGetValue(entity, out int slot) ? slot : _partSlots.GetValueOrDefault(entity, 16)).ToArray())
            {
                if (_postObjectMeleeHits.TryGetValue(entity, out var melee) && melee()) continue;
                if (entity is ISwitchHookHittableRoomEntity target && SwitchHook?.Item is { CollisionEnabled: true } hook &&
                    target.ApplySwitchHookHit(hook, SwitchHook.Player.Position)) continue;
                if (entity is ISeedCollisionTarget or ISomariaBlockCollisionRoomEntity or IBoomerangCollisionRoomEntity && ResolveNativeItemCollision(entity)) continue;
                if (_deferredSwitchHookContacts.Contains(entity) &&
                    !_linkCollisionsAndMenuDisabled && entity is ILinkContactEntity contact)
                {
                    if (player.AcceptsRoomEntityContact)
                    {
                        contact.HandleLinkContact(player);
                        if (contact is IPostObjectLinkContactRoomEntity effects)
                            effects.CollectContactSpawns(_pendingSpawns);
                    }
                }
            }
        }
        _deferredSwitchHookContacts.Clear();
        _postObjectMeleeHits.Clear();
        _postObjectThrownHits.Clear();
        _postObjectMeleeHitFactory = null;
        ProcessSpawns();
    }
    private bool ResolveNativeItemCollision(IRoomEntity entity)
    {
        // Reused native slots precede older scene nodes in higher slots.
        // Existing non-item projectile adapters retain their relative order.
        foreach (var item in _activeEntities.OrderBy(item =>
            _dynamicItems.SlotOf(item) is int slot && slot >= 0 ? slot : int.MaxValue))
        {
            if (item is BoomerangRoomEntity { Item.CollisionEnabled: true } boomerang && entity is IBoomerangCollisionRoomEntity boomerangTarget)
            {
                var boomerangResponse = boomerangTarget.ApplyBoomerangCollision(boomerang.Item, _pendingSpawns);
                if (boomerangResponse.Contact)
                {
                    if (entity is ItemDropRoomEntity drop)
                    {
                        int carrierSlot = _dynamicItems.SlotOf(item);
                        drop.AttachToItem(() => ReadDropCarrier(carrierSlot));
                    }
                    if (boomerangResponse.Returns) boomerang.Item.QueueCollision();
                    if (boomerangResponse.Clink is { } point) TryCreateBoomerangClink(point, 0);
                    return true;
                }
            }
            if (item is SomariaBlockRoomEntity { Block.CollisionEnabled: true } somaria &&
                entity is ISomariaBlockCollisionRoomEntity blockTarget &&
                blockTarget.ApplySomariaBlockCollision(somaria.Block, _pendingSpawns)) return true;
            if (entity is IPostObjectItemCollisionRoomEntity nativeItem)
            {
                if (item is SwordBeamRoomEntity { CollisionEnabled: true } beam &&
                    nativeItem.ApplyItemCollision(RoomEntityItemCollision.SwordBeam, beam.CollisionBounds,
                        beam.CollisionBounds.GetCenter(), beam.Damage, _pendingSpawns))
                { beam.QueueNativeCollision(); return true; }
                if (item is IBombExplosionRoomEntity { CollisionEnabled: true } bomb &&
                    ObjectCollisionZOverlaps(entity is IObjectCollisionHeightRoomEntity partHeight ? partHeight.CollisionZ : 0,
                        bomb.CollisionZ, bomb.CollisionZRadius) &&
                    nativeItem.ApplyItemCollision(RoomEntityItemCollision.Bomb, bomb.CollisionBounds,
                        bomb.CollisionBounds.GetCenter(), bomb.Damage, _pendingSpawns)) return true;
            }
            if (item is not ISeedProjectileRoomEntity seed || entity is not ISeedCollisionTarget target) continue;
            if (!seed.CollisionEnabled || entity is IObjectCollisionHeightRoomEntity height &&
                !ObjectCollisionZOverlaps(height.CollisionZ, seed.CollisionZ, 7)) continue;
            SeedCollisionResponse response = target.ApplySeedCollision(seed.CollisionBounds,
                seed.CollisionBounds.GetCenter(), seed.Record, seed.CollisionType, _pendingSpawns);
            if (!response.Contact) continue;
            if (response.Effect != SeedHitResult.None) seed.QueueNativeCollision(response);
            // enemyCheckCollisions tail-jumps to the first overlapping item's
            // handler, including effect00. No later item or Link check for
            // this enemy; the outer enemy loop still visits later slots.
            return true;
        }
        if (entity is IPostObjectItemCollisionRoomEntity thrownTarget)
            foreach (var thrown in _postObjectThrownHits)
                if (ObjectCollisionZOverlaps(entity is IObjectCollisionHeightRoomEntity h ? h.CollisionZ : 0, thrown.Z, thrown.Radius) &&
                    thrownTarget.ApplyItemCollision(RoomEntityItemCollision.ThrownObject, thrown.Bounds,
                        thrown.Bounds.GetCenter(), thrown.Damage, _pendingSpawns)) return true;
        return false;
    }
    private ItemDropCarrier? ReadDropCarrier(int slot)
    {
        object? owner = _dynamicItems.OwnerAt(slot);
        if (owner is null) return null;
        if (owner is SwitchHookItem hook) return new(0x0b, hook.ChainPosition, hook.ChainZHigh);
        if (owner is not IRoomEntity entity)
            throw new InvalidOperationException($"PART_ITEM_DROP relatedObj1 ITEM${slot:x2} has no position owner.");
        int z = entity.Node switch
        {
            BoomerangItem boomerang => boomerang.ZHigh,
            BombEffect bomb => bomb.CollisionZ,
            SomariaBlock block => block.ZHigh,
            SwordBeamEffect => 0,
            _ when entity is ISeedProjectileRoomEntity seed => seed.CollisionZ,
            _ => throw new InvalidOperationException($"PART_ITEM_DROP relatedObj1 ITEM${slot:x2} has no Z owner.")
        };
        return new(DynamicItemId(entity), ((Node2D)entity.Node).Position, z);
    }
    internal bool SwitchHookChainSlotAvailable => DynamicItemSlotAvailable;
    public OracleRuntimeState RuntimeState => _runtimeState;
    internal BipinBlossomFamilyStateResolver FamilyStateResolver =>
        _familyState;
    internal int ActiveTriggers => _activeTriggers;
    internal int FrameCounter => _enemyFrameCounter;
    internal int RandomCalls => _random.Calls;
    internal int ScreenShakeCounter => _screenShakeCounter;
    internal int HorizontalScreenShakeCounter =>
        _horizontalScreenShakeCounter;
    internal bool LinkCollisionsAndMenuDisabled =>
        _linkCollisionsAndMenuDisabled || FloorToggle?.Frozen == true;
    internal bool IsDisposed => _disposed;
    internal bool PlayerRidingObject
    {
        get
        {
            if (_platformRiding.HasRiderOrInstrument) return true;
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is IPlayerRideableRoomEntity { LinkRiding: true })
                    return true;
            }
            return false;
        }
    }
    internal IPlayerScreenTransitionRoomEntity? PlayerScreenTransitionOwner
    {
        get
        {
            IPlayerScreenTransitionRoomEntity? owner = null;
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is not IPlayerScreenTransitionRoomEntity
                    {
                        ControlsPlayerScreenTransition: true
                    } candidate)
                {
                    continue;
                }
                if (owner is not null)
                {
                    throw new InvalidOperationException(
                        "Multiple room entities own Link's screen-transition position.");
                }
                owner = candidate;
            }
            return owner;
        }
    }
    internal bool HasActiveSeed(int item, SeedLaunchKind launch) =>
        _activeEntities.OfType<EmberSeedRoomEntity>().Any(seed =>
            !seed.Finished && seed.SeedItem == item && seed.LaunchKind == launch);
    internal bool HasActiveShooterSeed =>
        _activeEntities.OfType<EmberSeedRoomEntity>().Any(seed =>
            !seed.Finished && seed.LaunchKind == SeedLaunchKind.Shooter);
    internal int ActiveBombCount
    {
        get
        {
            int count = 0;
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is BombRoomEntity { Finished: false })
                    count++;
            }
            return count;
        }
    }
    internal byte NextRandomValue() => _random.Next().Value;
    internal event Action? GaleMenuRequested;

    internal bool PushBlockPermittedByColoredCube(byte tile)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IColoredCubePuzzleStateSource source)
                return source.ColoredCubePuzzleState.PermitsPushBlock(tile);
        }
        return true;
    }

    internal RoomEntityManagerState CaptureDebugState() => new(
        _activeTriggers,
        _enemyFrameAccumulator,
        _enemyFrameCounter,
        _recentEnemyDefeats.CaptureState());

    internal void RestoreDebugStateBeforeRoomParse(
        RoomEntityManagerState state) =>
        _recentEnemyDefeats.RestoreState(state.RecentEnemyDefeats);

    internal void RestoreDebugStateAfterRoomParse(RoomEntityManagerState state)
    {
        if (!double.IsFinite(state.FrameAccumulator) ||
            state.FrameAccumulator is < 0.0 or >= 1.0 ||
            state.FrameCounter is < 0 or > 0xff)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        _activeTriggers = state.ActiveTriggers;
        _enemyFrameAccumulator = state.FrameAccumulator;
        _enemyFrameCounter = state.FrameCounter;
    }

    public bool PlayerSwordDisabled
        => HasPlayerRestriction(static restriction => restriction.DisablesSword);
    internal bool PlayerUpdatesFrozen
        => _smogLinkAndMenuLocked || HasPlayerRestriction(static restriction => restriction.FreezesPlayerUpdates);
    public bool PlayerItemUsageDisabled
        => HasPlayerRestriction(static restriction => restriction.DisablesItems);
    public bool PlayerMovementDisabled
    {
        get
        {
            if (HasPlayerRestriction(
                    static restriction => restriction.DisablesMovement))
                return true;
            return HasPlayerRestriction(static restriction => restriction.DisablesSword &&
                restriction.AlternatesMovementWithSwordRestriction) && (_enemyFrameCounter & 1) != 0;
        }
    }
    public bool PlayerMenusDisabled
    {
        get
        {
            if (_smogLinkAndMenuLocked || _linkCollisionsAndMenuDisabled || SwitchHook?.ExchangeActive == true)
                return true;
            return HasPlayerRestriction(
                static restriction => restriction.DisablesMenus);
        }
    }
    public bool PlayerRingTransformationsDisabled
        => HasPlayerRestriction(
            static restriction => restriction.DisablesRingTransformations);
    public bool ScreenTransitionsDisabled
        => HasPlayerRestriction(
            static restriction => restriction.DisablesScreenTransitions);
    // INTERAC$33 writes wMenuDisabled, which checkTileWarps also reads.
    // The separate wDisableLinkCollisionsAndMenu byte is not that gate.
    internal bool WarpTilesDisabled => MenuDisablesWarpTiles || NativeWarpTilesDisabled;
    internal bool MenuDisablesWarpTiles => _smogLinkAndMenuLocked ||
        HasPlayerRestriction(static restriction => restriction.MenuDisablesWarpTiles);
    // func_60e9 reads wDisableWarpTiles before the entered-position marker;
    // checkTileWarps reads wMenuDisabled only after that marker is updated.
    internal bool NativeWarpTilesDisabled =>
        HasPlayerRestriction(static restriction => restriction.DisablesWarpTiles);
    internal bool PlayerContactDisabled => SwitchHook?.ExchangeActive == true ||
        HasPlayerRestriction(static restriction => restriction.DisablesPlayerContact);
    internal bool PlayerPassesNpcs => HasPlayerRestriction(static restriction => restriction.PassesNpcs);
    internal Vector2? MountedCompanionPosition => _activeEntities
        .FirstOrDefault(entity => entity is IForestCompanion { LinkRiding: true })?.Node.Position;
    internal Vector2? MountedRaftPosition => _activeEntities.OfType<RaftRoomEntity>()
        .FirstOrDefault(raft => raft.LinkRiding)?.PrecisePosition;

    public RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null)
        : this(worldRoot, npcs, enemies, new ItemDropDatabase(),
            new TimePortalDatabase(), new OracleRandom(), saveData, runtimeState)
    { }

    internal RoomEntityManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        ItemDropDatabase itemDrops,
        TimePortalDatabase timePortals,
        OracleRandom random,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null,
        InventoryState? inventory = null,
        Func<long>? animationTick = null,
        TreasureDatabase? treasures = null,
        RoomSession? rooms = null)
    {
        _worldRoot = worldRoot;
        _random = random;
        _itemDrops = itemDrops;
        _saveData = saveData;
        _inventory = inventory;
        _treasures = treasures ?? new TreasureDatabase();
        _runtimeState = runtimeState ?? new OracleRuntimeState();
        _random.BindPlacementMemory(_runtimeState);
        _animationTick = animationTick ?? (() => 0);
        _familyState = new BipinBlossomFamilyStateResolver(npcs);
        _factory = new RoomEntityFactory(
            _familyState, enemies, itemDrops, timePortals, random,
            _saveData, _runtimeState, OnTimePortalEntered,
            () => PlayingInstrumentSource(),
            () => GroundTreasureCollectionAllowed(),
            OnGroundTreasureCollected, OnDungeonEntranceTriggered,
            OnRoomWarpRequested,
            OnGashaInteractionRequested, OnGashaNutCaught, inventory,
            _treasures,
            OnItemDropEnteredHazard,
            OnObjectFellInHole,
            OnSoundRequested, ApplyThrownObjectHit, CountRoomEnemies,
            () => _recentEnemyDefeats.ActiveRoomBitset,
            TriggerIsActive, () => _activeTriggers, SetTrigger,
            OnRoomTileChanged,
            OnDungeonEssenceTriggered,
            BossShuttersClosed,
            BeginScreenShake,
            DisableLinkCollisionsAndMenu,
            EnableLinkCollisionsAndMenu,
            OnRoomMusicRequested,
            OnRoomEntityDialogueRequested,
            OnMapleDialogueRequested,
            OnSeedTreeMessageRequested,
            OnOwlStatueMessageRequested,
            () => TextActiveSource(),
            OnMapleItemCollected,
            BeginHorizontalScreenShake,
            position => WorldToScreen(position), _animationTick, rooms,
            MaplePresent, SpawnDiggingEnemy, RegisterEnemySlot, FindFreeEnemySlot,
            RetainFailedPlacementCount,
            () => DisplayedHealthSource(), SetScreenShake,
            () => _screenShakeCounter != 0 || _horizontalScreenShakeCounter != 0,
            magnitude => _screenShakeMagnitude = magnitude,
            () => FindFreePartSlot() >= 0, _platformRiding,
            count => _reservedEnemySlots.Count <= 16 - count,
            () => FindFreeInteractionSlot() >= 0,
            KillMoldormRelatedParts, TryAllocateEnemy, () => _enemyFrameCounter,
            RoomEntityFreezeActive, WriteSmogInteractionCounter, TryCreatePuzzlePuff, ReleaseSmogLinkAndMenu,
            InitializeActiveSmogBossRoom, _bossShutterSignal.BeginBossEntry,
            opened => { if (opened) _bossShutterSignal.Opened(); else _bossShutterSignal.Closed(); },
            LockSmogLinkAndMenu, () => _bossShutterSignal.Value,
            phase => { TryMergeSmogClouds(phase); }, ReleaseSmogSentinelCount,
            () => ReservedPushBlock ?? throw new InvalidOperationException("INTERAC $bd requires reserved $14."),
            TryCreateSynchronizedBlock,TryCreateRockDebris,TryCreateSeedReflectorChild,TryCreateLightableTorch,TryCreateStatueEyeball,TryCreateOwlSparkle,_outgoingEntities.Contains,
            () => DoorPaletteFadeActive,
            (channel, value) => (NativeChannelVolumeWritten ?? throw new InvalidOperationException(
                "Native channel-volume writes require the sound driver owner."))(channel, value),
            TryCreateTransformationPuff, InteractionAnimationParameter, TryCreateSparkFairy, TryCreateBoomerangClink);
        if (_saveData is not null)
            _saveData.Changed += RefreshNpcState;
        _runtimeState.Changed += RefreshNpcState;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_saveData is not null)
            _saveData.Changed -= RefreshNpcState;
        _runtimeState.Changed -= RefreshNpcState;
    }

    internal IEnumerable<bool> PrepareResources() => _factory.PrepareResources();

    public List<T> Entities<T>() where T : Node2D => SelectNodes<T>(_activeEntities);
    internal IEnumerable<T> EntityAdapters<T>() where T : IRoomEntity =>
        _activeEntities.OfType<T>();
    public List<T> OutgoingEntities<T>() where T : Node2D => SelectNodes<T>(_outgoingEntities);

    public void LoadRoom(int group, OracleRoomData room)
    {
        LoadRoom(group, room, EnemyPlacementContext.Unrestricted);
    }

    internal void LoadRoom(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        Clear();
        _roomForActiveEntities = room;
        AddRoomEntities(group, room, placementContext);
    }

    /// <summary>
    /// Mirrors disableLcdAndLoadRoom followed by parseGivenObjectData: change
    /// the room backing the entity set without parsing its ordinary object list.
    /// The caller may retain time portals explicitly present in the cutscene set.
    /// </summary>
    public void LoadCutsceneRoom(int group, OracleRoomData room, bool includeTimePortals)
    {
        Clear();
        _roomForActiveEntities = room;
        if (!includeTimePortals)
            return;
        foreach (IRoomEntity portal in _factory.CreateTimePortals(group, room))
            AddEntity(portal);
    }

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset, Player? player = null)
    {
        BeginScreenTransition(
            group, room, incomingOffset, EnemyPlacementContext.Unrestricted, player);
    }

    internal void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        Vector2I scrollDirection,
        int entryPackedPosition,
        Player player)
    {
        BeginScreenTransition(
            group, room, incomingOffset,
            EnemyPlacementContext.Scrolling(
                scrollDirection, entryPackedPosition), player);
    }

    private void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        EnemyPlacementContext placementContext,
        Player? player)
    {
        ReservedKeyDoor?.BeginScreenTransition();
        ReservedPushBlock?.BeginScreenTransition();
        // updateSeedTreeRefillData runs after getNextActiveRoom only when the
        // outgoing tileset is outdoors. Warp/direct loads bypass this path.
        if ((_roomForActiveEntities.TilesetFlags & 0x01) != 0)
            _factory.UpdateSeedTreeRefillState(group, room.Id);
        ClearEntities(_outgoingEntities);
        _reservedEnemySlots.ExceptWith(_unownedOutgoingEnemySlots);
        _unownedOutgoingEnemySlots.Clear();
        _unownedOutgoingEnemySlots.UnionWith(_reservedEnemySlots.Except(_enemySlots.Values));
        _outgoingEntities.AddRange(_activeEntities);
        _activeEntities.Clear();
        _grabbableObjects.Clear();
        _postObjectMeleeHits.Clear();
        _postObjectThrownHits.Clear();
        _postObjectMeleeHitFactory = null;
        // setObjectsEnabledTo2 preserves outgoing allocations until deletion
        // or clearObjectsWithEnabled2 at scroll completion. Incoming objects
        // allocate from these same pools, including any holes already freed.
        _screenTransitionActive = true;
        _screenTransitionFrameAccumulator = 0.0;
        _roomForActiveEntities = room;
        AddRoomEntities(group, room, placementContext);
        PrepareIncomingEntitiesForScreenTransition(player);
        SetScreenTransitionOffsets(Vector2.Zero, incomingOffset);
    }

    public void SetScreenTransitionOffsets(Vector2 outgoingOffset, Vector2 incomingOffset)
    {
        if (!_screenTransitionActive)
            return;
        ReservedPushBlock?.SetScreenTransitionOffset(outgoingOffset);
        foreach (IRoomEntity entity in _outgoingEntities)
            entity.SetTransitionDrawOffset(outgoingOffset);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;
        ReservedKeyDoor?.FinishScreenTransition();
        ReservedPushBlock?.FinishScreenTransition();
        for (int index = _outgoingEntities.Count - 1; index >= 0; index--)
        {
            IRoomEntity entity = _outgoingEntities[index];
            // enabled=$03 bypasses setObjectsEnabledTo2 and survives
            // clearItemsWithEnabled2. Its raw cloud coordinates also survive.
            if (entity is not PegasusDustRoomEntity && entity is not IPlayerScreenTransitionRoomEntity
                {
                    ControlsPlayerScreenTransition: true
                })
            {
                continue;
            }
            _outgoingEntities.RemoveAt(index);
            _activeEntities.Add(entity);
        }
        ClearEntities(_outgoingEntities);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(Vector2.Zero);
        _screenTransitionActive = false;
        _reservedEnemySlots.ExceptWith(_unownedOutgoingEnemySlots);
        _unownedOutgoingEnemySlots.Clear();
        _screenTransitionFrameAccumulator = 0.0;
    }

    internal void UpdateRaftBeforePlayer(Player player)
    {
        _specialObjectsUpdatedBeforePlayer.Clear();
        if (_screenTransitionActive || TextActiveSource() || RoomEntityFreezeActive() ||
            player.ElectricShockActive ||
            HasPlayerRestriction(static restriction => restriction.DisablesCompanion))
            return;
        var frame = new RoomEntityFrame(player, _enemyFrameCounter, false, null);
        foreach (RaftRoomEntity raft in _activeEntities.OfType<RaftRoomEntity>())
        {
            if (!raft.UsesSpecialObjectSlot)
                continue;
            raft.UpdateFrame(frame, _pendingSpawns);
            _specialObjectsUpdatedBeforePlayer.Add(raft);
        }
    }

    public void Update(double delta, Player player)
        => Update(delta, player, timeWarpArrival: false);

    internal void AdvanceFrozenRoomFrame() =>
        _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;

    internal void UpdateTimeWarpSource(Player player, bool interactionsDisabled, bool enemiesDisabled)
    {
        _updatedEntitiesThisFrame.Clear();
        AdvanceFrozenRoomFrame();
        var frame = new RoomEntityFrame(player, _enemyFrameCounter, false, null);
        // Portal entry uses $81, permitting interactions; direct Harp uses
        // $5b, restricting interactions to their always-update handlers.
        // $5b leaves DISABLE_ENEMIES clear; updateEnemies additionally waits
        // for wPaletteThread_mode to clear. Both entry masks disable parts.
        for (int phase = 0; phase < 3; phase++)
        foreach (IRoomEntity entity in _activeEntities
            .Where(entity => EntityPhase(entity) == phase)
            .OrderBy(entity => _enemySlots.GetValueOrDefault(entity, 16)).ToArray())
        {
            if (_updatedEntitiesThisFrame.Contains(entity) ||
                entity is IRoomEntityLifetime { Finished: true } ||
                entity is ISeedProjectileRoomEntity ||
                phase == 0 && enemiesDisabled ||
                phase == 1 && !UpdatesDuringDialogue(entity) ||
                phase == 2 && interactionsDisabled && !UpdatesDuringDialogue(entity))
                continue;
            if (entity is IFixedRoomEntity fixedEntity)
            {
                SynchronizeEnemyFrameCounter(entity, frame.Counter);
                fixedEntity.UpdateFrame(frame, _pendingSpawns);
                _updatedEntitiesThisFrame.Add(entity);
            }
            ProcessSpawns(frame);
        }
        RemoveFinishedEntities(frame);
    }

    internal void Update(double delta, Player player, bool timeWarpArrival, bool enemiesDisabled = false)
    {
        _deferredSwitchHookContacts.Clear();
        // The original engine freezes both enabled $02 outgoing objects and
        // enabled $01 destination objects until scrolling has completed,
        // except interactions carrying the explicit always-update bit.
        if (_screenTransitionActive)
        {
            UpdateAlwaysEntitiesDuringScreenTransition(delta, player);
            return;
        }

        bool textActive = TextActiveSource() || InitializedObjectsDisabledSource();
        bool roomEntityFreezeActive = RoomEntityFreezeActive();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (textActive && !UpdatesDuringDialogue(entity) ||
                roomEntityFreezeActive && !UpdatesDuringRoomEntityFreeze(entity))
                continue;
            if (entity is IVariableRoomEntity variableEntity)
                variableEntity.Update(delta, player);
        }

        bool anyButtonJustPressed = GameButtonJustPressedSource();
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _updatedEntitiesThisFrame.Clear();
            roomEntityFreezeActive = RoomEntityFreezeActive();
            // updateSpecialObjects copies the instrument byte after Link has
            // consumed the previous update's rider. Interactions claim anew.
            if (!textActive && !roomEntityFreezeActive && !timeWarpArrival)
                _platformRiding.BeginUpdate(PlayingInstrumentSource());
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            var frame = new RoomEntityFrame(
                player, _enemyFrameCounter, anyButtonJustPressed, null);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if ((!textActive || UpdatesDuringDialogue(entity)) &&
                    !(player.ElectricShockActive && entity is IPlayerRideableRoomEntity) &&
                    (!roomEntityFreezeActive ||
                     UpdatesDuringRoomEntityFreeze(entity)) &&
                    !timeWarpArrival && entity is IPlayerForcedMovement forcedMovement)
                {
                    forcedMovement.UpdatePlayerForcedMovement(player);
                }
            }
            if (!textActive && !roomEntityFreezeActive)
            {
                ResolvePlayerProjectileCollisions();
                ResolveBossBombCatches();
                ResolveBombExplosionCollisions();
                ResolveMapleBombPulling();
            }

            // updateInteractions/updateParts still run newly allocated state
            // 0 objects while wTextIsActive is set. UpdateThisFrame spawns
            // model that creation update and therefore remain unconditional.
            ProcessSpawns(frame);

            // Legacy seed receivers still resolve before terrain movement.
            // Native ISeedCollisionTarget owners use the post-object scan;
            // itemUpdateDamageToApply consumes their pending item status on
            // the following update, before seedItemState1's terrain check.
            if (!textActive && !roomEntityFreezeActive)
                ResolveSeedCollisions(preMovement: true);
            ProcessSpawns(frame);

            // updateItems clears wScentSeedActive, updates every item slot,
            // and only then begins the enemy pass. Visit the live native slots
            // so reuse does not substitute scene insertion order for $d7-$db.
            SwitchHook?.UpdateItem(player, textActive || roomEntityFreezeActive);
            Somaria?.UpdateItem(player, textActive || roomEntityFreezeActive);
            foreach (object owner in _dynamicItems.LiveOwners())
            {
                if (owner is not IRoomEntity entity ||
                    _updatedEntitiesThisFrame.Contains(entity) ||
                    (textActive || roomEntityFreezeActive) && !ItemSetupPending(entity) ||
                    entity is not IFixedRoomEntity fixedItem)
                {
                    continue;
                }
                fixedItem.UpdateFrame(frame, _pendingSpawns);
                _updatedEntitiesThisFrame.Add(entity);
                if (entity.Node is EmberSeedEffect { GaleMenuRequested: true })
                    GaleMenuRequested?.Invoke();
                ProcessSpawns(frame);
            }
            // Existing seed flame adapters represent PARTs after conversion;
            // they no longer own an ITEM slot. Do not advance a conversion a
            // second time in the update that produced it.
            foreach (var flame in _activeEntities.OfType<EmberSeedRoomEntity>().ToArray())
                if (flame.IsFlamePart && !_updatedEntitiesThisFrame.Contains(flame) &&
                    !textActive && !roomEntityFreezeActive)
                {
                    flame.UpdateFrame(frame, _pendingSpawns);
                    _updatedEntitiesThisFrame.Add(flame);
                    ProcessSpawns(frame);
                }
            // Reserved item F ($df) follows the ordinary seed child slots.
            if (!textActive && !roomEntityFreezeActive)
                foreach (var child in _activeEntities.OfType<IBraceletChildRoomEntity>().ToArray())
                    child.UpdateBraceletChild(player);
            foreach (var dust in _activeEntities.OfType<PegasusDustRoomEntity>().ToArray())
                if (!dust.Finished && (dust.Substate == 0 || !textActive && !roomEntityFreezeActive))
                    dust.UpdateFrame(frame, _pendingSpawns);
            if (!textActive && !roomEntityFreezeActive)
                ResolveSeedCollisions(preMovement: false);
            ProcessSpawns(frame);
            SwitchHook?.UpdateHelper(textActive || roomEntityFreezeActive);
            frame = frame with
            {
                ScentSeedTarget = ActiveScentSeedTarget(),
                SwitchHookState = SwitchHook?.ExchangeState ?? 0
            };

            // updateEnemies precedes updateParts, which precedes interactions.
            // Native enemies visit live slots: a newly allocated later slot
            // runs this update; an earlier slot waits for the next pass.
            for (int phase = 0; phase < 3; phase++)
            {
                // Each native category samples wTextIsActive on entry. A
                // message opened by an enemy freezes initialized parts and
                // interactions later in this same update.
                textActive = TextActiveSource() || InitializedObjectsDisabledSource();
                // bank0.updateEnemies samples the palette thread once before
                // its slot walk, admitting only state0 while a fade is active.
                bool enemyPassDisabled = phase == 0 && (enemiesDisabled || PaletteFadeActiveSource());
                if (phase == 2)
                    ReservedKeyDoor?.Advance(1.0 / 60.0, player,
                        InitializedObjectsDisabledSource() || FloorToggle?.Frozen == true || _activeEntities.Any(entity => entity is IRoomEntityUpdateFreeze
                            { FreezesRoomEntities: true, FreezesInteractions: true }));
                if (phase == 2 && ReservedPushBlock is { Active: true } push &&
                    (!push.NativeInitialized || !textActive && !roomEntityFreezeActive))
                    push.Advance(1.0 / 60.0,player);
                foreach (IRoomEntity entity in EntitiesForUpdatePhase(phase))
                {
                    if (enemyPassDisabled && !UpdatesDuringDialogue(entity))
                        continue;
                    if (_updatedEntitiesThisFrame.Contains(entity) ||
                        _specialObjectsUpdatedBeforePlayer.Contains(entity))
                        continue;
                    if (player.ElectricShockActive && entity is IPlayerRideableRoomEntity)
                        continue;
                    if (DynamicItemId(entity) >= 0 || entity is ISeedProjectileRoomEntity or PegasusDustRoomEntity)
                        continue;
                    if (entity is IRoomEntityLifetime { Finished: true })
                    {
                        if (phase == 0 && _enemySlots.Remove(entity, out int finishedSlot))
                            _reservedEnemySlots.Remove(finishedSlot);
                        continue;
                    }
                    if (textActive && !UpdatesDuringDialogue(entity))
                        continue;
                    // DISABLE_COMPANION freezes the shared special-object slot,
                    // independently of the enemy/part/interaction update masks.
                    if (entity is IForestCompanion or MinecartRoomEntity or RaftRoomEntity &&
                        HasPlayerRestriction(static restriction => restriction.DisablesCompanion))
                        continue;
                    if (roomEntityFreezeActive &&
                        !UpdatesDuringRoomEntityFreeze(entity))
                        continue;
                    if (entity is IGaleSeedTarget { GaleCaught: true } gale)
                    {
                        gale.UpdateGale(-(int)WorldToScreen(Vector2.Zero).Y);
                        continue;
                    }
                    if (entity is ISeedBurnTarget
                        {
                            IsSeedBurning: true,
                            FreezesDuringSeedBurn: true
                        })
                        continue;
                    if (entity is IFixedRoomEntity fixedEntity)
                    {
                        SynchronizeEnemyFrameCounter(entity, frame.Counter);
                        fixedEntity.UpdateFrame(frame, _pendingSpawns);
                        _updatedEntitiesThisFrame.Add(entity);
                    }
                    if (entity is IRoomEnemyReplacementSource replacementSource &&
                        replacementSource.TryTakeReplacement(out var replacement))
                        ReplaceEnemy(entity, replacement);
                    if (TryReplacePart(entity)) continue;
                    if (phase < 2 && _activeEntities.Contains(entity) && entity is IRoomEntityLifetime { Finished: true } completed)
                    {
                        // enemyDie allocates its PART before enemyDelete. Do
                        // this during that slot's dispatch, before later
                        // enemies, parts, and room-clear interactions run.
                        completed.OnFinished(_pendingSpawns);
                        ProcessSpawns(frame);
                        ApplyEnemyOutcomes(entity);
                        _activeEntities.Remove(entity);
                        FreeEntity(entity);
                    }
                    ProcessSpawns(frame);
                    if (phase == 0 && entity is IRoomEntityLifetime { Finished: true } &&
                        _enemySlots.Remove(entity, out int releasedSlot))
                        _reservedEnemySlots.Remove(releasedSlot);
                }
                if (phase == 2)
                {
                    foreach (var entity in _activeEntities.ToArray())
                    {
                        if (_updatedEntitiesThisFrame.Contains(entity) && entity is IRoomInteractionChildSource children)
                            children.UpdateChildren(frame, _pendingSpawns);
                    }
                }
            }
            ProcessSpawns(frame);
            UpdateScreenShake();
            // specialObjects.s clears this after the companion update;
            // native interaction events publish the next update's lock.
            CompanionRuntimeState.SetMountingLock(_runtimeState, 0);
            _specialObjectsUpdatedBeforePlayer.Clear();
            anyButtonJustPressed = false;
        }

        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (!timeWarpArrival && !textActive &&
                !RoomEntityFreezeActive() &&
                !_linkCollisionsAndMenuDisabled &&
                (player.AcceptsRoomEntityContact ||
                    entity is TimePortalRoomEntity && player.AcceptsTimePortalContact) &&
                entity is ILinkContactEntity contactEntity)
            {
                if (entity is IPostObjectMeleeCollisionRoomEntity or ISeedCollisionTarget or IPostObjectLinkContactRoomEntity or ISomariaBlockCollisionRoomEntity or IBoomerangCollisionRoomEntity ||
                    entity is ISwitchHookHittableRoomEntity && SwitchHook?.Item is { CollisionEnabled: true })
                    _deferredSwitchHookContacts.Add(entity);
                else contactEntity.HandleLinkContact(player);
            }
        }
        RemoveFinishedEntities();
        DispatchPendingRoomWarp();
    }

    internal void UpdateDuringHarp(double delta, Player player)
    {
        _platformRiding.BeginUpdate(PlayingInstrumentSource());
        // ITEM_HARP writes $7e to wDisabledObjects: every ordinary object
        // category is frozen. ENEMY_POLS_VOICE is the exception: its handler
        // explicitly reads wLinkPlayingInstrument before ordinary state
        // dispatch and dies during this enemy phase. The dormant portal
        // spawner separately carries the interaction always-update bit, as do
        // the floating notes owned by HarpController.
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            var frame = new RoomEntityFrame(
                player, _enemyFrameCounter, AnyButtonJustPressed: false,
                ScentSeedTarget: null);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (entity is IInstrumentReactiveRoomEntity reactive)
                {
                    SynchronizeEnemyFrameCounter(entity, frame.Counter);
                    reactive.UpdateDuringInstrument(frame, _pendingSpawns);
                }
            }
            ProcessSpawns(frame);
            // enemyDie creates PART_ENEMY_DESTROYED in the following part
            // phase, before always-updating interactions run.
            RemoveFinishedEntities(frame);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (entity is TimePortalRoomEntity portal)
                    portal.UpdateFrame(frame, _pendingSpawns);
            }
            ProcessSpawns(frame);
            RemoveFinishedEntities(frame);
        }
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IRoomBlocker blocker && blocker.BlocksLink(linkCenter))
                return true;
        }
        return false;
    }

    internal bool TimeWarpPositionOccupied(TimeWarpLandingDatabase database, int packedPosition)
    {
        // objectMarkSolidPosition reserves the whole short-position metatile,
        // independently of an NPC's collision radius or facing.
        foreach (IRoomEntity entity in _activeEntities)
            if (entity.Node is NpcCharacter { Active: true } npc &&
                database.MarksSolidPosition(npc.BaseRecord) &&
                _roomForActiveEntities.GetPackedPosition(npc.TimeWarpSolidPosition) == packedPosition)
                return true;
        return false;
    }

    internal void UpdatePushableEntities(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IRoomPushableEntity pushable)
                pushable.UpdatePushAttempt(linkPosition, facing, movementInput);
        }
    }

    public NpcCharacter? FindTalkTarget(Player player) =>
        FindNpcInteractionTarget(player)?.Npc;

    internal NpcInteractionTarget? FindNpcInteractionTarget(Player player)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is not ITalkTarget talkTarget ||
                talkTarget.FindTalkTarget(player) is not { } npc)
            {
                continue;
            }
            _npcTalkLifecycles.TryGetValue(
                npc, out INpcTalkLifecycle? lifecycle);
            return new NpcInteractionTarget(npc, lifecycle);
        }
        return null;
    }

    internal NpcInteractionTarget ResolveNpcInteractionTarget(
        NpcCharacter npc)
    {
        _npcTalkLifecycles.TryGetValue(
            npc, out INpcTalkLifecycle? lifecycle);
        return new NpcInteractionTarget(npc, lifecycle);
    }

    internal bool TryInteract(Player player)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IPlayerInteractable interactable &&
                interactable.TryInteract(player))
            {
                return true;
            }
        }
        return false;
    }

    internal bool TryUseBracelet(
        Player player,
        Vector2I releaseDirection) =>
        TryUseBracelet(player, releaseDirection, out _);

    internal bool TryUseBracelet(
        Player player,
        Vector2I releaseDirection,
        out IBraceletPullInteractableRoomEntity? pullInteraction)
    {
        pullInteraction = null;
        if (!player.IsCarryingObject)
        {
            if (ReservedBraceletChildActive) return false;
            foreach (var candidate in _grabbableObjects)
                if (_activeEntities.Contains(candidate) && candidate.TryUseBracelet(player, releaseDirection))
                    return true;
        }
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (!player.IsCarryingObject && entity is INativeBraceletRoomEntity) continue;
            if (releaseDirection == Vector2I.Zero &&
                entity is IBraceletPullInteractableRoomEntity pull &&
                pull.TryBeginBraceletPull(player))
            {
                pullInteraction = pull;
                return true;
            }
            if (entity is IBraceletInteractableRoomEntity bracelet &&
                bracelet.TryUseBracelet(player, releaseDirection))
            {
                return true;
            }
        }
        return false;
    }

    internal bool ApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition)
    {
        bool hit = false;
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IShovelHittableRoomEntity shovelHittable)
                hit |= shovelHittable.ApplyShovelHit(hitbox, sourcePosition);
        }
        return hit;
    }

    internal void NotifyTileDug(int packedPosition)
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IDugTileRoomEntity dugTile)
                dugTile.NotifyTileDug(packedPosition);
        }
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2? sourcePosition = null,
        int damage = 2) =>
        ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            EnemyKnockbackStrength.Low,
            collectItemDrops: true);

    internal bool ApplySwordHit(
        Rect2 hitbox,
        Vector2? sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        bool collectItemDrops = false,
        Action<SwordAttackerKnockback>? attackerKnockback = null,
        SwordActionState swordState = SwordActionState.Swing,
        int swordLevel = 1,
        int itemZ = -2,
        bool expertPunch = false,
        Action? deferredContact = null,
        Func<bool>? meleeActive = null)
    {
        bool hit = false;
        Vector2 source = sourcePosition ?? hitbox.GetCenter();
        bool ApplyHit(IRoomEntity entity)
        {
            if (entity is not ISwordHittableRoomEntity swordHittable) return false;
            // collisionEffects.s compares Enemy.zh with Item.zh before XY.
            int targetZ = entity is IObjectCollisionHeightRoomEntity height ? height.CollisionZ : 0;
            if (!ObjectCollisionZOverlaps(targetZ, itemZ, radius: 0x07)) return false;
            if (entity is ILinkSwordStateAwareRoomEntity stateAware)
                stateAware.SetLinkSwordState(swordState, swordLevel);
            bool accepted = expertPunch && entity is IExpertPunchHittableRoomEntity expertTarget
                ? expertTarget.ApplyExpertPunch(hitbox, source, damage, _pendingSpawns)
                : swordHittable.ApplySwordHit(hitbox, source, damage, knockbackStrength, _pendingSpawns);
            if (accepted && attackerKnockback is not null &&
                entity is ISwordAttackerKnockbackRoomEntity recoilSource &&
                recoilSource.TryGetSwordAttackerKnockback(knockbackStrength, out SwordAttackerKnockback response))
                attackerKnockback(response);
            return accepted;
        }
        _postObjectMeleeHitFactory = entity =>
        {
            if (entity is not IPostObjectMeleeCollisionRoomEntity postObjectMelee ||
                entity is not ISwordHittableRoomEntity) return null;
            return () =>
            {
                if (entity is IRoomEntityLifetime { Finished: true } ||
                    meleeActive?.Invoke() == false || !ApplyHit(entity)) return false;
                if (postObjectMelee.MeleeReportsContact) deferredContact?.Invoke();
                return true;
            };
        };
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (collectItemDrops &&
                entity is ILinkSwordCollectibleRoomEntity collectible)
            {
                // COLLISIONEFFECT_23 does not write Item.var2a, so collecting
                // a drop must not count as enemy contact for the sword.
                collectible.TryCollectWithSword(hitbox);
            }
            if (_postObjectMeleeHitFactory(entity) is { } deferred) _postObjectMeleeHits[entity] = deferred;
            else hit |= ApplyHit(entity);
            ProcessSpawns();
        }
        RemoveFinishedEntities();
        return hit;
    }

    internal void ApplyThrownObjectHit(
        Rect2 hitbox,
        int itemZ,
        int collisionZRadius,
        int damage)
    {
        _postObjectThrownHits.Add((hitbox, itemZ, collisionZRadius, damage));
        Vector2 source = hitbox.GetCenter();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IPostObjectItemCollisionRoomEntity) continue;
            if (entity is ISwordHittableRoomEntity or
                IItemCollisionHittableRoomEntity)
            {
                int targetZ =
                    entity is IObjectCollisionHeightRoomEntity height
                        ? height.CollisionZ
                        : 0;
                if (!ObjectCollisionZOverlaps(
                        targetZ, itemZ, collisionZRadius))
                {
                    continue;
                }
                if (entity is IItemCollisionHittableRoomEntity itemHittable)
                {
                    itemHittable.ApplyItemCollision(
                        RoomEntityItemCollision.ThrownObject,
                        hitbox,
                        source,
                        damage,
                        _pendingSpawns);
                }
                else
                {
                    ((ISwordHittableRoomEntity)entity).ApplySwordHit(
                        hitbox,
                        source,
                        damage,
                        EnemyKnockbackStrength.Normal,
                        _pendingSpawns);
                }
            }
            ProcessSpawns();
        }
        RemoveFinishedEntities();
    }

    internal static bool ObjectCollisionXYOverlaps(Rect2 target, Rect2 other)
    {
        // checkObjectsCollidedFromVariables uses high bytes and includes
        // the negative summed-radius edge, excluding the positive edge.
        Vector2 targetCenter = target.GetCenter(), otherCenter = other.GetCenter();
        int radiusX = (int)((target.Size.X + other.Size.X) / 2);
        int radiusY = (int)((target.Size.Y + other.Size.Y) / 2);
        return ((Mathf.FloorToInt(otherCenter.X) - Mathf.FloorToInt(targetCenter.X) + radiusX) & 255) < ((radiusX * 2) & 255) &&
            ((Mathf.FloorToInt(otherCenter.Y) - Mathf.FloorToInt(targetCenter.Y) + radiusY) & 255) < ((radiusY * 2) & 255);
    }

    internal static bool ObjectCollisionZOverlaps(
        int targetZ,
        int itemZ,
        int radius)
    {
        if (radius is <= 0 or > 0x7f)
            return false;
        // collisionEffects.s performs this in one-byte arithmetic:
        // enemyZ - itemZ + $07 must be below $0e.
        return ((targetZ - itemZ + radius) & 0xff) < radius * 2;
    }

    private void ResolveSeedCollisions(bool preMovement)
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is not ISeedProjectileRoomEntity
                { CollisionEnabled: true } seed)
            {
                continue;
            }
            foreach (IRoomEntity target in _activeEntities.ToArray())
            {
                if (target is ISeedCollisionTarget || target is not ISeedHittableRoomEntity hittable ||
                    (seed.SeedItem is 0x22 or 0x23 || target is ISeedPreMovementCollisionTarget) != preMovement)
                {
                    continue;
                }
                if (seed.SeedItem == 0x22 && target is IObjectCollisionHeightRoomEntity height &&
                    !ObjectCollisionZOverlaps(height.CollisionZ, seed.CollisionZ, 7)) continue;
                SeedHitResult result = seed.SeedItem == 0x23 && target is IGaleSeedTarget gale &&
                    gale.TryCatchGale(seed.CollisionBounds, seed.CollisionZ, NextRandomValue)
                    ? SeedHitResult.Activate :
                    target is ISeedHeightAwareHittableRoomEntity heightAware
                        ? heightAware.ApplySeedHitAtHeight(
                            seed.CollisionBounds,
                            seed.CollisionBounds.GetCenter(),
                            seed.CollisionZ,
                            seed.SeedItem,
                            _pendingSpawns)
                        : hittable.ApplySeedHit(
                            seed.CollisionBounds,
                            seed.CollisionBounds.GetCenter(),
                            seed.SeedItem,
                            _pendingSpawns);
                if (result == SeedHitResult.None)
                {
                    continue;
                }
                ISeedBounceTarget? bounceTarget =
                    target as ISeedBounceTarget;
                if (preMovement &&
                    result == SeedHitResult.Bounce &&
                    bounceTarget is null)
                {
                    throw new InvalidOperationException(
                        $"{target.GetType().Name} returned {result} from " +
                        "the pre-movement seed-collision pass without " +
                        $"implementing {nameof(ISeedBounceTarget)}.");
                }
                seed.OnCollision(
                    result,
                    result == SeedHitResult.Ignite ? hittable as ISeedBurnTarget : null,
                    bounceTarget,
                    _pendingSpawns, preMovement);
                break;
            }
        }
    }

    internal Vector2? ActiveScentSeedTarget()
    {
        Vector2? result = null;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is ISeedProjectileRoomEntity
                { ScentTarget: { } target })
            {
                // The original item loop leaves hFFB2/hFFB3 containing the
                // last active scent item's coordinates.
                result = target;
            }
        }
        return result;
    }

    private void ResolvePlayerProjectileCollisions()
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is not IPlayerProjectileRoomEntity
                { CollisionEnabled: true } projectile)
            {
                continue;
            }
            foreach (IRoomEntity target in _activeEntities.ToArray())
            {
                if (target is IPostObjectItemCollisionRoomEntity) continue;
                if (projectile is DimitriMouthRoomEntity mouth)
                {
                    if (!mouth.TrySwallow(target)) continue;
                    mouth.OnEnemyCollision(_pendingSpawns);
                    break;
                }
                if (target is not ISwordHittableRoomEntity &&
                    target is not IItemCollisionHittableRoomEntity)
                    continue;
                bool accepted =
                    target is IItemCollisionHittableRoomEntity itemHittable
                        ? itemHittable.ApplyItemCollision(
                            RoomEntityItemCollision.SwordBeam,
                            projectile.CollisionBounds,
                            projectile.CollisionBounds.GetCenter(),
                            projectile.Damage,
                            _pendingSpawns)
                        : ((ISwordHittableRoomEntity)target).ApplySwordHit(
                            projectile.CollisionBounds,
                            projectile.CollisionBounds.GetCenter(),
                            projectile.Damage,
                            EnemyKnockbackStrength.Normal,
                            _pendingSpawns);
                if (!accepted)
                {
                    continue;
                }
                projectile.OnEnemyCollision(_pendingSpawns);
                break;
            }
        }
    }

    private void ResolveBombExplosionCollisions()
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is not IBombExplosionRoomEntity
                { CollisionEnabled: true } bomb)
            {
                continue;
            }
            foreach (IRoomEntity target in _activeEntities.ToArray())
            {
                if (target is IPostObjectItemCollisionRoomEntity) continue;
                if (ReferenceEquals(entity, target) ||
                    (target is not ISwordHittableRoomEntity &&
                     target is not IItemCollisionHittableRoomEntity))
                {
                    continue;
                }
                int targetZ =
                    target is IObjectCollisionHeightRoomEntity height
                        ? height.CollisionZ
                        : 0;
                if (!ObjectCollisionZOverlaps(
                        targetZ,
                        bomb.CollisionZ,
                        bomb.CollisionZRadius))
                {
                    continue;
                }
                if (target is IItemCollisionHittableRoomEntity itemHittable)
                {
                    itemHittable.ApplyItemCollision(
                        RoomEntityItemCollision.Bomb,
                        bomb.CollisionBounds,
                        bomb.CollisionBounds.GetCenter(),
                        bomb.Damage,
                        _pendingSpawns);
                }
                else
                {
                    ((ISwordHittableRoomEntity)target).ApplySwordHit(
                        bomb.CollisionBounds,
                        bomb.CollisionBounds.GetCenter(),
                        bomb.Damage,
                        EnemyKnockbackStrength.High,
                        _pendingSpawns);
                }
                ProcessSpawns();
            }
        }
    }

    private void ResolveBossBombCatches()
    {
        foreach (IRoomEntity target in _activeEntities.ToArray())
        {
            if (target is not IBombCatchRoomEntity catcher)
                continue;
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (entity is BombRoomEntity
                    {
                        Finished: false
                    } bomb &&
                    catcher.TryCatchBomb(bomb.Bomb))
                {
                    return;
                }
            }
        }
    }

    private void ResolveMapleBombPulling()
    {
        MapleEncounter? maple = null;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is MapleEncounterRoomEntity mapleEntity &&
                mapleEntity.Maple.CanPullBomb)
            {
                maple = mapleEntity.Maple;
                break;
            }
        }
        if (maple is null)
            return;

        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is not BombRoomEntity
                {
                    Finished: false,
                    Bomb: { CanMaplePull: true }
                } bomb ||
                !maple.OverlapsBomb(bomb.Bomb))
            {
                continue;
            }
            maple.BeginBombPull();
            if (bomb.Bomb.PullTowardMaple(maple.Position))
                maple.BeginBombStun();
            return;
        }
    }

    internal bool TrySpawnSwordBeam(Vector2 linkPosition, int direction)
    {
        if (!DynamicItemSlotAvailable) return false;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is SwordBeamRoomEntity { Finished: false })
                return false;
        }
        _pendingSpawns.Add(new SwordBeamSpawn(linkPosition, direction));
        ProcessSpawns();
        return true;
    }

    internal bool TryPickupBomb(
        Player player,
        out BombEffect? bomb)
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is BombRoomEntity
                {
                    Finished: false
                } bombEntity &&
                bombEntity.Bomb.OverlapsForPickup(player))
            {
                bomb = bombEntity.Bomb;
                return true;
            }
        }
        bomb = null;
        return false;
    }

    internal bool TrySpawnDebugEnemy(int id, int subId, Vector2 position, out string error)
        => CanSpawnDebugObject(position, out error) &&
            TrySpawnEnemy(id, subId, position, "Debug spawn", out error);

    internal bool TrySpawnEnemy(int id, int subId, Vector2 position, string source, out string error)
    {
        // Native interaction state zero can create enemies during scrolling
        // (goron_targetCarts_loadCrystals). The preload pass initializes these
        // later children; the debug UI's transition restriction does not apply.
        if (!CanSpawnRoomObject(position, out error))
            return false;
        int slot = Enumerable.Range(0, 16).FirstOrDefault(
            candidate => !_reservedEnemySlots.Contains(candidate), -1);
        if (slot < 0)
        {
            error = $"Enemy ${id:x2}:${subId:x2}: all $10 enemy slots are occupied.";
            return false;
        }
        IRoomEntity? entity = _factory.CreateStandaloneEnemy(
            id, subId, _roomForActiveEntities, position, source, out error);
        if (entity is null)
            return false;
        RegisterEnemySlot(entity, slot);
        AddEntity(entity);
        return true;
    }

    internal bool TrySpawnDebugItemDrop(int subId, Vector2 position, out string error)
    {
        if (!CanSpawnDebugObject(position, out error))
            return false;
        if (!ItemDropDatabase.IsRuntimeSupported(subId))
        {
            error = $"PART_ITEM_DROP ${subId:x2} has no supported runtime handler.";
            return false;
        }
        Spawn(new ItemDropSpawn(subId, position));
        return true;
    }

    private bool CanSpawnDebugObject(Vector2 position, out string error)
    {
        if (_screenTransitionActive)
        {
            error = "Wait for an active room with no transition.";
            return false;
        }
        return CanSpawnRoomObject(position, out error);
    }

    private bool CanSpawnRoomObject(Vector2 position, out string error)
    {
        error = string.Empty;
        if (_disposed || _roomForActiveEntities is null)
            error = "Wait for an active room with no transition.";
        else if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
            position.X != MathF.Truncate(position.X) || position.Y != MathF.Truncate(position.Y) ||
            position.X < 0 || position.Y < 0 ||
            position.X >= _roomForActiveEntities.Width || position.Y >= _roomForActiveEntities.Height)
            error = "Spawn coordinates must be whole pixels inside the playable room.";
        return error.Length == 0;
    }

    internal Node2D Spawn(RoomEntitySpawn spawn) =>
        AddEntity(_factory.Create(spawn, _roomForActiveEntities)).Node;

    internal T Spawn<T>(RoomEntitySpawn spawn) where T : Node2D
    {
        return (T)Spawn(spawn);
    }

    internal TimePortal SpawnTemporaryTimePortal(Vector2 position)
    {
        IRoomEntity entity = AddEntity(
            _factory.CreateTemporaryTimePortal(_roomForActiveEntities, position));
        return (TimePortal)entity.Node;
    }

    internal GroundTreasurePickup SpawnGroundTreasure(
        GroundTreasureGrantRequest request) =>
        Spawn<GroundTreasurePickup>(new GroundTreasureGrantSpawn(request));

    internal GroundTreasurePickup GrantGroundTreasure(
        GroundTreasureGrantRequest request,
        Player player)
    {
        if (request.SpawnMode != 0)
        {
            throw new InvalidOperationException(
                $"Immediate ground-treasure grant from {request.Source} " +
                $"uses spawn mode ${request.SpawnMode:x2} instead of $00.");
        }
        GroundTreasurePickup treasure =
            Spawn<GroundTreasurePickup>(new GroundTreasureGrantSpawn(request));
        ActivateGroundTreasure(treasure, player, immediate: true);
        return treasure;
    }

    internal void SpawnBreakableDrop(
        int dropType,
        Vector2 position,
        Vector2I shovelDirection)
    {
        int? subId = _itemDrops.DecideBreakableDrop(
            dropType, _random, _inventory, _saveData);
        if (subId.HasValue)
        {
            int angle = shovelDirection == Vector2I.Up ? 0x00
                : shovelDirection == Vector2I.Right ? 0x08
                : shovelDirection == Vector2I.Down ? 0x10
                : shovelDirection == Vector2I.Left ? 0x18
                : throw new ArgumentOutOfRangeException(nameof(shovelDirection));
            Spawn<ItemDropEffect>(new ItemDropSpawn(
                subId.Value, position, angle, DugUp: true));
        }
    }

    internal void SpawnBreakableDrop(int dropType, Vector2 position)
    {
        int? subId = _itemDrops.DecideBreakableDrop(
            dropType, _random, _inventory, _saveData);
        if (subId.HasValue)
        {
            Spawn<ItemDropEffect>(new ItemDropSpawn(
                subId.Value, position));
        }
    }

    internal void SpawnItemHazardEffect(
        Vector2 position,
        HazardType hazard,
        ObjectFellInHoleKind? objectKind = null)
    {
        if (hazard is HazardType.Water or
            HazardType.Lava)
        {
            ItemDropEnteredHazard?.Invoke(position, hazard);
        }
        else if (hazard == HazardType.Hole)
        {
            if (objectKind.HasValue)
                ObjectFellInHole?.Invoke(objectKind.Value);
            Spawn<FallingDownHoleEffect>(
                new FallingDownHoleSpawn(position));
        }
    }

    internal void ClearPhysicalPlayerItems()
    {
        // bank0.s:clearAllItemsAndPutLinkOnGround clears the physical Item
        // slots without collision, explosion, loot, or ordinary finish effects.
        Somaria?.Cancel();
        BoomerangParent.Clear();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is SomariaBlockRoomEntity somaria)
            {
                somaria.ClearPhysicalItem();
                continue;
            }
            // Ember's free flame / attached burning-enemy phase represents a
            // Part slot; clearing Items must not strand its burn target.
            if (entity is EmberSeedRoomEntity { IsFlamePart: true }) continue;
            if (entity is not (IPlayerProjectileRoomEntity or ISeedProjectileRoomEntity or BombRoomEntity or BoomerangRoomEntity))
                continue;
            _activeEntities.Remove(entity);
            FreeEntity(entity);
        }
    }

    internal void MarkPreviousSomariaBlock()
    {
        if (_dynamicItems.FindItem(0x18) is SomariaBlockRoomEntity previous)
            previous.Block.Flags |= 0x20;
    }

    internal bool TryCreateBoomerang(Vector2 position, int angle, int zHigh)
    {
        // itemCreateChildWithID/getFreeItemSlotWithObjectCap: the existing
        // ITEM$06 still counts while hidden in its four-update catch state.
        if (_dynamicItems.FindItem(InventoryState.ItemBoomerang) is not null || !DynamicItemSlotAvailable) return false;
        Spawn<BoomerangItem>(new BoomerangSpawn(position, angle, zHigh));
        return true;
    }

    internal void RequestSomariaPush(int direction)
    {
        // findItemWithID is independent of tile position and state; a stale
        // $da tile with no ITEM$18 consumes the push attempt without a spawn.
        if (_dynamicItems.FindItem(0x18) is SomariaBlockRoomEntity block)
            block.Block.RequestPush(direction);
    }

    internal bool TryCreateSomariaBlock(Player player, int group, Vector2 position, int z)
    {
        if (!DynamicItemSlotAvailable) return false;
        AddEntity(new SomariaBlockRoomEntity(_roomForActiveEntities,group,player,position,z,_animationTick,
            OnSoundRequested,
            (point,height)=>
            {
                if (InteractionSlotAvailable)
                    Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(point,OracleSoundEngine.SndPoof,ZHigh:height));
            },
            (kind,point,height)=>
            {
                if (!InteractionSlotAvailable) return;
                SpawnItemHazardEffect(point+Vector2.Down*height,
                kind switch { 1=>HazardType.Water, 2=>HazardType.Hole, 4=>HazardType.Lava,
                    _=>throw new NotSupportedException($"ITEM$18 hazard ${kind:x2} is not represented.") },
                    ObjectFellInHoleKind.CaneOfSomariaBlock);
            }));
        return true;
    }

    public void Clear()
    {
        ReservedKeyDoor?.Cancel();
        ReservedPushBlock?.Cancel();
        _grabbableObjects.Clear();
        _postObjectMeleeHits.Clear();
        _postObjectMeleeHitFactory = null;
        _deferredSwitchHookContacts.Clear();
        _postObjectThrownHits.Clear();
        SwitchHook?.Cancel();
        Somaria?.Cancel();
        BoomerangParent.Clear();
        _specialObjectsUpdatedBeforePlayer.Clear();
        _preShockBackgroundPalettes = null;
        _activeObjectPaletteOverride = null;
        ClearEntities(_outgoingEntities);
        ClearEntities(_activeEntities);
        _dynamicItems.Clear();
        _enemySlots.Clear();
        _reservedEnemySlots.Clear();
        _unownedOutgoingEnemySlots.Clear();
        Array.Clear(_deletedEnemyCounter1);
        Array.Clear(_inactiveInteractionCounter2);
        _unreleasedEnemyCounts = 0;
        _pendingSpawns.Clear();
        _pendingRoomWarp = null;
        _screenTransitionActive = false;
        _enemyFrameAccumulator = 0.0;
        _screenTransitionFrameAccumulator = 0.0;
        _screenShakeCounter = 0;
        _horizontalScreenShakeCounter = 0;
        _screenShakeMagnitude = 0;
        _linkCollisionsAndMenuDisabled = false;
        _smogLinkAndMenuLocked = false;
        _bossShutterSignal.Clear();
        ScreenShakeChanged?.Invoke(Vector2.Zero);
    }

    internal void ClearRecentEnemyDefeats() => _recentEnemyDefeats.Clear();

    private void AddRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        // parseObjectData clears wNumEnemies even when outgoing slots are
        // retained during a scroll. Failed allocation counts are room-local.
        _unreleasedEnemyCounts = 0;
        _bossShutterSignal.Clear();
        _smogLinkAndMenuLocked = false;
        // loadTilesetAndRoomLayout runs the common tile substitutions before
        // parseObjectData. Layout shutters $78-$7f can exist only in that
        // layout and therefore must be opened before placed entities are read.
        _factory.ApplyEntryShutterSubstitution(room, placementContext);
        // wActiveTriggers is room-local scratch state cleared by room loading.
        _activeTriggers = 0;
        _platformRiding.BeginUpdate(0);
        _runtimeState.SetWramByte(OracleRuntimeState.Lever1PullDistanceAddress, 0);
        _runtimeState.SetWramByte(OracleRuntimeState.Lever2PullDistanceAddress, 0);
        _runtimeState.SetWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress, 0);
        // parseObjectData loads wEnemyPlacement.killedEnemiesBitset from the
        // last-eight-room list before rebuilding w4RandomBuffer.
        _recentEnemyDefeats.BeginRoom(room.Id);
        // parseObjectData clears wEnemyPlacement, then rebuilds w4RandomBuffer.
        // This consumes 256 values from the game-wide RNG on every room parse.
        _random.BeginRoomParse();
        foreach (IRoomEntity entity in _factory.CreateRoomEntities(
            group, room, placementContext))
            AddEntity(entity);
        RefreshNpcState(_activeEntities);
        RoomEntitiesLoaded?.Invoke(group, room);
    }

    private void RefreshNpcState()
    {
        RefreshNpcState(_outgoingEntities);
        RefreshNpcState(_activeEntities);
    }

    private int CountRoomEnemies()
    {
        int count = _unreleasedEnemyCounts;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IRoomEnemyCounterEntity { CountsAsEnemy: true })
                count++;
        }
        return count;
    }

    private void RetainFailedPlacementCount(int flags)
    {
        // objectDataOp6's US failure branch only clears Enemy.enabled.
        // decEnemyCounterIfApplicable already cancelled uncounted objects.
        if ((flags & 0x02) == 0) _unreleasedEnemyCounts++;
    }

    private bool BossShuttersClosed()
    {
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IBossShutterState { BossIntroReady: false })
                return false;
        }
        return true;
    }

    internal void BeginScreenShake(int updates)
    {
        if (updates <= 0)
            throw new ArgumentOutOfRangeException(nameof(updates));
        _screenShakeCounter = updates;
        _horizontalScreenShakeCounter = updates;
    }

    private void BeginHorizontalScreenShake(int updates)
    {
        if (updates <= 0)
            throw new ArgumentOutOfRangeException(nameof(updates));
        _horizontalScreenShakeCounter = updates;
    }

    private void DisableLinkCollisionsAndMenu() =>
        _linkCollisionsAndMenuDisabled = true;

    private void EnableLinkCollisionsAndMenu() =>
        _linkCollisionsAndMenuDisabled = false;

    // INTERAC$33 owns wDisabledObjects=$01 plus wMenuDisabled. Do not route
    // this through the broad room freeze or wDisableLinkCollisionsAndMenu.
    internal void LockSmogLinkAndMenu() => _smogLinkAndMenuLocked = true;
    private void ReleaseSmogLinkAndMenu() => _smogLinkAndMenuLocked = false;

    internal void SetScreenShake(int y, int x, int magnitude)
    {
        if ((uint)y > 255 || (uint)x > 255 || (uint)magnitude > 2)
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        _screenShakeCounter = y;
        _horizontalScreenShakeCounter = x;
        _screenShakeMagnitude = magnitude;
        if (y == 0 && x == 0) ScreenShakeChanged?.Invoke(Vector2.Zero);
    }

    internal void UpdateScreenShake()
    {
        if (_screenShakeCounter == 0 &&
            _horizontalScreenShakeCounter == 0)
        {
            ScreenShakeChanged?.Invoke(Vector2.Zero);
            return;
        }
        // bank1.updateScreenShake: one shared RNG draw per active axis, Y first.
        int[] amounts = _screenShakeMagnitude switch
        {
            0 => [-2, -1, 1, 2],
            1 => [-1, -1, 1, 1],
            2 => [-3, -3, 3, 3],
            _ => throw new InvalidOperationException("Invalid wScreenShakeMagnitude.")
        };
        int y = 0;
        int x = 0;
        if (_screenShakeCounter != 0)
        {
            y = amounts[_random.Next().Value & 3];
            _screenShakeCounter--;
        }
        if (_horizontalScreenShakeCounter != 0)
        {
            x = amounts[_random.Next().Value & 3];
            _horizontalScreenShakeCounter--;
        }
        Vector2 offset = new(x, y);
        ScreenShakeChanged?.Invoke(offset);
    }

    internal int RoomEnemyCount => CountRoomEnemies();

    private bool TriggerIsActive(int bit)
    {
        if (bit is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(bit));
        return (_activeTriggers & (1 << bit)) != 0;
    }

    private void SetTrigger(int bit, bool active)
    {
        if (bit is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(bit));
        int mask = 1 << bit;
        _activeTriggers = active
            ? (byte)(_activeTriggers | mask)
            : (byte)(_activeTriggers & ~mask);
    }

    private void OnGashaInteractionRequested(
        GashaSpotInteraction interaction,
        Player player) => GashaInteractionRequested?.Invoke(interaction, player);

    private void OnGashaNutCaught(
        GashaSpotInteraction interaction,
        Player player) => GashaNutCaught?.Invoke(interaction, player);

    private void RefreshNpcState(IEnumerable<IRoomEntity> entities)
    {
        if (_saveData is null)
            return;
        foreach (IRoomEntity entity in entities)
        {
            if (entity is IRoomSaveStateEntity stateEntity)
                stateEntity.RefreshSaveState();
            if (entity is IOrdinaryNpcEntity ordinary)
            {
                NpcCharacter npc = ordinary.Npc;
                npc.SetFlagVisible(_npcVisibility.ShouldShow(
                    npc.BaseRecord, _saveData, _runtimeState));
                if (_familyState.TryResolveDialogue(
                    npc.BaseRecord, _saveData, out Dialogue familyDialogue))
                {
                    npc.SetDialogue(
                        familyDialogue.TextId,
                        familyDialogue.Message,
                        npc.BaseRecord.CanFace);
                }
                else if (_npcDialogue.TryResolve(
                    npc.BaseRecord, _saveData,
                    out NpcDialogueRuleDatabaseDialogue dialogue))
                {
                    npc.SetDialogue(
                        dialogue.TextId, dialogue.Message, dialogue.CanFace);
                }
                if (_npcPositions.TryResolve(
                    npc.BaseRecord, _saveData, out Vector2 position))
                {
                    npc.SetStatePosition(position);
                }
            }
        }
    }

    private IRoomEntity AddEntity(IRoomEntity entity)
    {
        if (entity.Node is EnemyCharacter enemy)
        {
            enemy.TryCreateKnockbackDust = TryCreateKnockbackDust;
            enemy.BindMovementMemory(_runtimeState);
        }
        if (entity.Node is SmogProjectilePart smogProjectile)
            smogProjectile.BindMovementMemory(_runtimeState);
        if (entity.Node is EnemyArrowProjectile arrowProjectile)
            arrowProjectile.BindMovementMemory(_runtimeState);
        if (entity.Node is SwordBeamEffect swordBeam)
            swordBeam.BindMovementMemory(_runtimeState);
        if (entity.Node is BombEffect bomb)
            bomb.BindMovementMemory(_runtimeState);
        if (entity.Node is EmberSeedEffect seed)
            seed.BindMovementMemory(_runtimeState);
        if (entity.Node is ItemDropEffect drop)
            drop.BindMovementMemory(_runtimeState);
        if (entity.Node is BoomerangItem boomerang)
            boomerang.BindMovementMemory(_runtimeState);
        if (entity.Node is SomariaBlock somariaBlock)
            somariaBlock.BindMovementMemory(_runtimeState);
        if (entity.Node is FallingDownHoleEffect fallingHole)
            fallingHole.BindMovementMemory(_runtimeState);
        if (entity.Node is MovingSideScrollPlatformRoomEntity sidePlatform)
            sidePlatform.BindMovementMemory(_runtimeState);
        int dynamicId=DynamicItemId(entity);
        if(dynamicId>=0 && _dynamicItems.TryAllocate(entity,dynamicId,
            ()=>_activeEntities.Contains(entity) && entity is not IRoomEntityLifetime {Finished:true} && DynamicItemId(entity)>=0)<0)
        {
            FreeEntity(entity);
            throw new NotSupportedException($"ITEM${dynamicId:x2}: caller requested creation with all dynamic slots $d7-$db occupied; use a checked allocation path.");
        }
        if (entity is INativeBraceletRoomEntity grabbable)
            grabbable.BindGrabbablePublisher(PublishGrabbableObject);
        if (entity is DungeonEssence essence)
        {
            essence.BindEnergySwirl(center => CreateEnergySwirl(center), () => _runtimeState.SetWramByte(BlueEnergyBeadDatabase.Shared.DeleteAddress, 1));
            essence.BindChildren(CreateEssenceChildren);
        }
        if (UsesInteractionSlot(entity))
        {
            int slot = FindFreeInteractionSlot();
            if (slot < 0) throw new InvalidOperationException($"{entity.GetType().Name}: native dynamic INTERACTION pool is full ($d2-$df).");
            _interactionSlots.Add(entity, slot);
            if (_inactiveInteractionCounter2[slot] != 0)
            {
                WriteSmogInteractionCounter(slot, _inactiveInteractionCounter2[slot]);
                _inactiveInteractionCounter2[slot] = 0;
            }
            if (entity.Node is PuzzlePuffEffect puff)
                puff.BindNativeTiming(slot, () => _enemyFrameCounter);
            if (entity.Node is ClinkEffect clink)
                clink.BindNativeTiming(slot, () => _enemyFrameCounter);
        }
        if (EntityPhase(entity) == 1 && !_partSlots.ContainsKey(entity))
        {
            int slot = FindFreePartSlot();
            if (slot >= 0) _partSlots.Add(entity, slot);
            if (entity.Node is VolcanoRock rock) rock.SetPartSlot(slot);
            if (entity.Node is FallingBoulder boulder) boulder.SetPartSlot(slot);
        }
        if (_activeObjectPaletteOverride is not null) ApplyObjectPaletteOverride(entity);
        // Children created by enemy handlers also occupy the shared pool.
        // Placed entities and itemDrop_spawnEnemy already registered their slot.
        if ((entity.Node is EnemyCharacter || entity is ArmosSpawnerRoomEntity or FireballShooterRoomEntity) && entity is IRoomEnemyCounterEntity &&
            !_enemySlots.ContainsKey(entity))
        {
            for (int slot = 0; slot < 16; slot++)
            {
                if (_reservedEnemySlots.Contains(slot)) continue;
                RegisterEnemySlot(entity, slot);
                break;
            }
        }
        if (entity is INpcTalkLifecycle lifecycle &&
            !_npcTalkLifecycles.TryAdd(lifecycle.TalkNpc, lifecycle))
        {
            throw new InvalidOperationException(
                $"{entity.GetType().Name} registered duplicate talk " +
                $"lifecycle ownership for NPC ${lifecycle.TalkNpc.Record.Id:x2}:" +
                $"${lifecycle.TalkNpc.Record.SubId:x2}.");
        }
        if (entity.Node is TransitionOffsetNode2D drawable)
            drawable.SetWorldToScreen(position => WorldToScreen(position));
        _activeEntities.Add(entity);
        // The source collision pass scans objects created during the earlier
        // enemy/part passes too. Retain the weapon request for these targets.
        if (_postObjectMeleeHitFactory?.Invoke(entity) is { } melee)
            _postObjectMeleeHits[entity] = melee;
        if (entity is ICompanionBarrierTarget target)
        {
            foreach (IRoomEntity active in _activeEntities)
            {
                if (active is CompanionBarrierRoomEntity barrier)
                    barrier.BindTarget(target);
            }
        }
        else if (entity is CompanionBarrierRoomEntity barrier)
        {
            foreach (IRoomEntity active in _activeEntities)
            {
                if (active is ICompanionBarrierTarget activeTarget)
                {
                    barrier.BindTarget(activeTarget);
                    break;
                }
            }
        }
        _worldRoot.AddChild(entity.Node);
        if (_interactionSlots.TryGetValue(entity, out int drawSlot))
            ApplyInteractionDrawOrder(entity, drawSlot);
        if (entity is IFixedRoomEntity)
        {
            // Entering the tree can enable an overridden _PhysicsProcess
            // callback. Fixed room entities are advanced only by this manager
            // and must never race that owner.
            entity.Node.SetPhysicsProcess(false);
        }
        return entity;
    }

    private void ApplyInteractionDrawOrder(IRoomEntity entity, int slot)
    {
        // bank0.queueDrawEverything visits $d040..$df40 in ascending
        // slot order. Within a visible&3 priority bucket the earlier
        // OAM entry wins. Godot's equal-Z siblings paint in the opposite
        // direction, so insert later slots before earlier slots. Keep
        // the update list and slot allocation order unchanged.
        IRoomEntity? lowerSlot = _interactionSlots
            .Where(pair => pair.Value < slot && pair.Key.Node.GetParent() == _worldRoot)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key).FirstOrDefault();
        if (lowerSlot is not null)
            _worldRoot.MoveChild(entity.Node, lowerSlot.Node.GetIndex());
    }

    private void ProcessSpawns(RoomEntityFrame? frame = null)
    {
        while (_pendingSpawns.Count > 0)
        {
            RoomEntitySpawn spawn = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            if (spawn is SpikedBallSpawn ball && FindFreePartSlot() < 0)
            {
                // ecom_spawnProjectile checks failure for the head. The
                // three chain allocations do not: HL=$e0c0 aliases main
                // stack RAM, and the last CP$04 still returns Z to state0.
                if (ball.Part.SubId != 0)
                {
                    var head = _partSlots.FirstOrDefault(pair => ReferenceEquals(pair.Key.Node, ball.Part.Head));
                    _runtimeState.SetWramByte(0xc0c0, 0x2a);
                    _runtimeState.SetWramByte(0xc0c1, (byte)ball.Part.SubId);
                    _runtimeState.SetWramByte(0xc0d6, 0xc0);
                    _runtimeState.SetWramByte(0xc0d7, (byte)(head.Key is null ? 0xe0 : 0xd0 + head.Value));
                }
                ball.Part.Free();
                continue;
            }
            if(spawn is TargetCartDebrisSpawn && !InteractionSlotAvailable) continue;
            // swordBeam.s @collision always deletes ITEM$27 after attempting
            // INTERAC_CLINK$81, including when objectCreateInteraction fails.
            if (spawn is SwordBeamClinkSpawn && !InteractionSlotAvailable) continue;
            if (spawn is EnemyDeathPuffSpawn puff && FindFreePartSlot() < 0)
            {
                if (puff.DecrementsRoomCount) _unreleasedEnemyCounts++;
                continue; // No retry, kill sound, or item drop on allocation failure.
            }
            IRoomEntity entity = AddEntity(_factory.Create(spawn, _roomForActiveEntities));
            if (spawn.UpdateThisFrame && entity is not ItemDropRoomEntity && EntityPhase(entity) != 0 &&
                frame.HasValue && entity is IFixedRoomEntity fixedEntity)
            {
                SynchronizeEnemyFrameCounter(entity, frame.Value.Counter);
                fixedEntity.UpdateFrame(frame.Value, _pendingSpawns);
                _updatedEntitiesThisFrame.Add(entity);
            }
        }
    }

    private IEnumerable<IRoomEntity> EntitiesForUpdatePhase(int phase)
    {
        if (phase is 0 or 1)
        {
            for (int slot = 0; slot < 16; slot++)
            {
                IRoomEntity? entity = (phase==0?_enemySlots:_partSlots).FirstOrDefault(pair => pair.Value == slot &&
                    (phase==0||pair.Key is not IRoomEntityLifetime {Finished:true})).Key;
                if (entity is not null) yield return entity;
            }
            yield break;
        }
        // updateInteractions walks live slots $d0-$df. A child allocated above
        // the cursor runs in this pass; a reused lower slot waits until the
        // next pass. Insertion-order snapshots lose both rules ($bd pushes
        // allocate ordinary $14 children while traversing this same pool).
        var logicalOwners = _activeEntities.Where(entity => EntityPhase(entity) == phase &&
            !_interactionSlots.ContainsKey(entity)).ToArray();
        foreach (var entity in logicalOwners.Where(entity => entity is DungeonEssenceGlow))
            yield return entity;
        for (int slot = 0; slot < 16; slot++)
        {
            IRoomEntity? entity = _interactionSlots.FirstOrDefault(pair => pair.Value == slot &&
                pair.Key is not IRoomEntityLifetime { Finished: true }).Key;
            if (entity is not null) yield return entity;
        }
        foreach (var entity in logicalOwners.Where(entity => entity is not DungeonEssenceGlow))
            yield return entity;
    }

    private int EntityPhase(IRoomEntity entity) =>
        _enemySlots.ContainsKey(entity) ? 0 :
        entity is ItemDropRoomEntity or BridgeSpawnerRoomEntity or GroundButtonRoomEntity or BeamosBeamRoomEntity or SpikedBallRoomEntity or SmogProjectileRoomEntity or ZoraFireRoomEntity or DungeonSwitchRoomEntity
            or FountainFairyHeartRoomEntity or VolcanoRockRoomEntity or FallingBoulderRoomEntity or GoronBombRoomEntity or KingMoblinBombRoomEntity
            or EnemySwordRoomEntity or StalfosBoneRoomEntity or BurningEnemyRoomEntity or KeeseFireRoomEntity
            or BossShadowRoomEntity or BossDeathExplosionRoomEntity or DeathPuffRoomEntity or MovingOrbRoomEntity or DungeonOrbRoomEntity or SeedShooterEyeStatueRoomEntity or BlueEnergyBeadRoomEntity
            or OwlStatueRoomEntity or RotatableSeedThingRoomEntity or SeedReflectorChildRoomEntity or LightableTorchRoomEntity or DarkRoomHandlerRoomEntity ? 1 : 2;

    internal IReadOnlyList<BlueEnergyBeadRoomEntity> CreateEnergySwirl(Vector2 center, byte duration = 0xff)
    {
        var beads = new List<BlueEnergyBeadRoomEntity>();
        for (int index = BlueEnergyBeadDatabase.Shared.Count - 1; index >= 0 && FindFreePartSlot() >= 0; index--)
            beads.Add((BlueEnergyBeadRoomEntity)AddEntity(_factory.Create(new BlueEnergyBeadSpawn(index, center, duration), _roomForActiveEntities)));
        OnSoundRequested(OracleSoundEngine.SndEnergyThing);
        return beads;
    }

    private void CreateEssenceChildren(DungeonEssence essence)
    {
        // The pedestal takes the first free dynamic slot. The live walk
        // determines whether it initializes this pass or next; it survives
        // ROOMFLAG_ITEM. Reserved $d1 has
        // already run, so the new glow first dispatches next update.
        if (FindFreeInteractionSlot() >= 0)
        {
            var pedestal = essence.CreatePedestal();
            AddEntity(pedestal);
        }
        if (essence.Collected)
        {
            // Keep only the logical collection record; source state0 deletes
            // the dynamic parent after allocating the persistent pedestal.
            _interactionSlots.Remove(essence);
            return;
        }
        // essence.s clears reserved $d1 even if its previous object belongs
        // to the outgoing room; it is one global slot, not one per room.
        foreach (var previous in _activeEntities.Concat(_outgoingEntities).OfType<DungeonEssenceGlow>().ToArray())
        { _activeEntities.Remove(previous); _outgoingEntities.Remove(previous); FreeEntity(previous); }
        AddEntity(essence.CreateGlow());
    }

    private bool TryReplacePart(IRoomEntity original)
    {
        if (original is not IRoomPartReplacementSource source ||
            !source.TryTakePartReplacement(out var spawn)) return false;
        if (!_partSlots.Remove(original, out int slot))
            throw new InvalidOperationException($"{original.GetType().Name}: objectReplaceWithID has no native PART slot.");
        ApplyEnemyOutcomes(original);
        int index = _activeEntities.IndexOf(original);
        var replacement = _factory.Create(spawn, _roomForActiveEntities);
        _activeEntities.RemoveAt(index);
        FreeEntity(original);
        _partSlots.Add(replacement, slot);
        AddEntity(replacement);
        _activeEntities.Remove(replacement);
        _activeEntities.Insert(index, replacement);
        return true;
    }

    private void ReplaceEnemy(IRoomEntity original, RoomEnemyReplacement replacement)
    {
        if (!_enemySlots.TryGetValue(original, out int slot))
            throw new InvalidOperationException($"{replacement.Source.Source}: enemy replacement has no original slot.");
        int index = _activeEntities.IndexOf(original);
        var next = _factory.CreateEnemyReplacement(replacement, _roomForActiveEntities);
        _activeEntities.RemoveAt(index);
        FreeEntity(original);
        RegisterEnemySlot(next, slot);
        AddEntity(next);
        _activeEntities.Remove(next);
        _activeEntities.Insert(index, next);
        // The enemy-pass snapshot contains the old slot's dispatch. Its
        // replacement starts state0 next update, without a death outcome.
    }

    private void KillMoldormRelatedParts(MoldormCharacter head)
    {
        // Clean US moldorm.s leaves L in $c0..$ff after its conveyor lookup.
        // ecom_killObjectH therefore writes PART health/collision at each tail's
        // object-page index. It does not kill the corresponding ENEMY tail.
        foreach (int slot in new[] { head.Tail1Slot, head.Tail2Slot })
        {
            if (slot < 0) continue;
            var part = _partSlots.FirstOrDefault(pair => pair.Value == slot).Key;
            if (part is null or IRoomEntityLifetime { Finished: true }) continue;
            if (part is INativePartHealthRoomEntity health)
            {
                health.ClearHealthAndCollision();
                continue;
            }
            throw new NotSupportedException($"Clean-US ENEMY_MOLDORM $4f death writes health/collision to PART slot ${slot:x2} " +
                $"({part.GetType().Name}); this part's raw health write is not represented. " +
                "object_code/common/enemies/moldorm.s:@dead -> ecom_killObjectH.");
        }
    }

    private int FindFreePartSlot()
    {
        // getFreePartSlot scans $d0..$df in ascending order. Retain holes:
        // source slot parity controls each falling part's terrain shadow.
        for (int slot = 0; slot < 16; slot++)
        {
            bool occupied = false;
            foreach (var pair in _partSlots)
            {
                if (pair.Value == slot && pair.Key is not IRoomEntityLifetime { Finished: true })
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied) return slot;
        }
        return -1;
    }

    // These owners represent native INTERACTION objects. The default update
    // phase also contains logical controllers and ITEM/SPECIALOBJECT owners,
    // which must not consume one of the fourteen dynamic allocations.
    private static bool UsesInteractionSlot(IRoomEntity entity) => entity is
        KnowItAllBirdRoomEntity or KnockbackDustRoomEntity or EnemyClearStairsRoomEntity or RalphAfterChevalRoomEntity or DungeonEntranceRoomEntity or StatueEyeballSpawnerRoomEntity or StatueEyeballRoomEntity or MinibossPortalRoomEntity or
        RidgeBridgeControllerRoomEntity or CollapsingFloorRoomEntity or ExclamationMarkRoomEntity or FallingDownHoleRoomEntity or DefeatedMoblinActorRoomEntity or DungeonDoorRoomEntity or DungeonRewardRoomEntity or KillPuffRoomEntity or SwordBeamClinkRoomEntity or NpcRoomEntity or DungeonEssence or DungeonEssencePedestal ||
        entity.Node is PuzzlePuffEffect or EyesoarSpawnEffect or OwlStatueSparkleEffect or DungeonKeyUseEffect || entity is GoronCaveRoomEntity or TargetCartDebrisRoomEntity or SmogEncounterRoomEntity or MovingSideScrollPlatformRoomEntity or DungeonTriggerChestScriptRoomEntity or DungeonPuzzleChestRoomEntity or DungeonPatternHintRoomEntity
            or RetractableTriggerChestRoomEntity or TorchTriggerTranslatorRoomEntity or LightableTorchScannerRoomEntity or ButtonBridgeRoomEntity or PushBlockTriggerRoomEntity or ColoredCubeRoomEntity or ColoredCubeSensorRoomEntity or ColoredCubeFlameRoomEntity
            or DungeonStateController or MinecartGateRoomEntity
            or PushBlockSynchronizerRoomEntity or SynchronizedPushBlockRoomEntity or PuzzleTrapResetRoomEntity or WallSquishRoomEntity;

    internal bool InteractionSlotAvailable => FindFreeInteractionSlot() >= 0;
    internal bool PartSlotAvailable => FindFreePartSlot() >= 0;
    internal bool EnemySlotAvailable => _reservedEnemySlots.Count<16;
    internal int InteractionSlot(Node2D actor) =>
        _interactionSlots.Single(pair => ReferenceEquals(pair.Key.Node, actor)).Value;

    private int FindFreeInteractionSlot()
    {
        // getFreeInteractionSlot starts at FIRST_DYNAMIC_INTERACTION_INDEX
        // $d2; $d0/$d1 are reserved and never satisfy a dynamic allocation.
        for (int slot = 2; slot < 16; slot++)
            if (!_interactionSlots.Any(pair => pair.Value == slot && pair.Key is not IRoomEntityLifetime { Finished: true })) return slot;
        return -1;
    }

    private void PrepareIncomingEntitiesForScreenTransition(Player? player)
    {
        // updateEnemies/updateInteractions still dispatch source state 0 while
        // wScrollMode is active. Complete that work before the incoming room
        // is exposed, then freeze ordinary state-8+ updates. Native dispatch
        // order is ENEMY, PART, INTERACTION, each in slot order; object-stream
        // insertion order can place a controller before the enemy it observes.
        var prepared = new HashSet<IRoomEntity>();
        int Order(IRoomEntity entity, int phase) => phase switch
        {
            0 => _enemySlots[entity],
            1 => _partSlots.GetValueOrDefault(entity,16 + _activeEntities.IndexOf(entity)),
            _ => entity is DungeonEssenceGlow ? -1 :
                _interactionSlots.GetValueOrDefault(entity,16 + _activeEntities.IndexOf(entity))
        };
        while (_activeEntities.Any(entity => !prepared.Contains(entity)))
        {
            for (int phase = 0; phase < 3; phase++)
            {
                int cursor = -1;
                while (true)
                {
                    IRoomEntity? entity = _activeEntities.Where(candidate => !prepared.Contains(candidate) &&
                        EntityPhase(candidate) == phase && Order(candidate,phase) >= cursor)
                        .OrderBy(candidate => Order(candidate,phase)).FirstOrDefault();
                    if (entity is null) break;
                    cursor = Order(entity,phase) + 1;
                    prepared.Add(entity);
                    PrepareIncomingEntityForScreenTransition(entity, player);
                }
            }
        }
    }

    private void PrepareIncomingEntityForScreenTransition(IRoomEntity entity, Player? player)
    {
        if (entity is IScreenTransitionPreloadRoomEntity preloader)
        {
            ScreenTransitionPresentation presentation =
                preloader.PrepareForScreenTransition(player, _pendingSpawns);
            ProcessScreenTransitionPreloadSpawns();
            if (entity is IRoomEntityLifetime { Finished: true } && _enemySlots.Remove(entity, out int finishedSlot))
                _reservedEnemySlots.Remove(finishedSlot);
            ValidateScreenTransitionPresentation(entity, presentation);
            if (entity is IRoomEntityLifetime { Finished: true } lifetime)
            {
                ApplyEnemyOutcomes(entity);
                lifetime.OnFinished(_pendingSpawns);
                _activeEntities.Remove(entity);
                FreeEntity(entity);
                ProcessScreenTransitionPreloadSpawns();
            }
            return;
        }

        if (!entity.Node.Visible)
        {
            throw new InvalidOperationException(
                $"Incoming room entity {entity.GetType().Name} " +
                $"('{entity.Node.Name}') is hidden after creation and " +
                $"does not implement " +
                $"{nameof(IScreenTransitionPreloadRoomEntity)}. Source " +
                $"state 0 must resolve its transition presentation " +
                $"explicitly so it cannot pop in after scrolling.");
        }
    }

    private static void ValidateScreenTransitionPresentation(
        IRoomEntity entity,
        ScreenTransitionPresentation presentation)
    {
        bool expectedVisible = presentation switch
        {
            ScreenTransitionPresentation.Visible => true,
            ScreenTransitionPresentation.Hidden => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(presentation), presentation,
                "Unknown screen-transition presentation result.")
        };
        if (entity.Node.Visible == expectedVisible)
            return;

        throw new InvalidOperationException(
            $"Incoming room entity {entity.GetType().Name} " +
            $"('{entity.Node.Name}') reported transition presentation " +
            $"{presentation} but its root visibility is " +
            $"{entity.Node.Visible}.");
    }

    private void ProcessScreenTransitionPreloadSpawns()
    {
        while (_pendingSpawns.Count > 0)
        {
            RoomEntitySpawn spawn = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            IRoomEntity entity =
                AddEntity(_factory.Create(spawn, _roomForActiveEntities));
            if (spawn.UpdateThisFrame &&
                entity is not IScreenTransitionPreloadRoomEntity)
            {
                throw new InvalidOperationException(
                    $"Screen-transition preload spawn {spawn.GetType().Name} " +
                    $"created {entity.GetType().Name} with UpdateThisFrame but " +
                    $"without {nameof(IScreenTransitionPreloadRoomEntity)}.");
            }
        }
    }

    private static void SynchronizeEnemyFrameCounter(
        IRoomEntity entity,
        int frameCounter)
    {
        if (entity.Node is EnemyCharacter enemy)
            enemy.SetGlobalFrameCounter(frameCounter);
    }

    private static bool UpdatesDuringDialogue(IRoomEntity entity) =>
        entity is IUpdatesDuringDialogueRoomEntity
        {
            UpdatesDuringDialogue: true
        };

    private bool RoomEntityFreezeActive()
    {
        if (FloorToggle?.Frozen == true) return true;
        if (NonInteractionObjectsDisabledSource()) return true;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IRoomEntityUpdateFreeze
                {
                    FreezesRoomEntities: true
                })
            {
                return true;
            }
        }
        return false;
    }

    internal bool TryMergeSmogClouds(int phase) => SmogCloudMerge.TryMerge(
        _enemySlots.OrderBy(pair => pair.Value).Select(pair => pair.Key.Node).OfType<SmogCharacter>(),
        phase, spawn =>
        {
            if (TryAllocateEnemy(_ => _factory.Create(spawn,_roomForActiveEntities)) is null)
                _factory.ApplyFailedSmogControllerAllocation(spawn, merged: true);
        });

    internal void ReleaseSmogSentinelCount()
    {
        var sentinels = _activeEntities.OfType<SmogRoomEntity>().Where(entity => entity.IsRoomSentinel).ToArray();
        if (sentinels.Length != 1)
            throw new InvalidOperationException($"INTERAC$33 final decrement found {sentinels.Length} live ENEMY$7c:$05 sentinels; expected one.");
        sentinels[0].ReleaseSentinelCount();
    }

    private int InitializeActiveSmogBossRoom(bool scrolling)
    {
        var owners = _activeEntities.OfType<SmogRoomEntity>().Where(entity => entity.OwnsBossRoomInitialization).ToArray();
        if (owners.Length != 1)
            throw new NotSupportedException($"Smog state0 room initialization requires one active placed sentinel; found {owners.Length}.");
        return owners[0].InitializeBossRoom(scrolling);
    }

    private void WriteSmogInteractionCounter(int slot, int value)
    {
        if (slot is < 0 or >= 16) throw new InvalidOperationException("Smog $47 write requires its native ENEMY page.");
        var target = _interactionSlots.FirstOrDefault(pair => pair.Value == slot &&
            pair.Key is not IRoomEntityLifetime { Finished: true }).Key;
        // Active actor aliases require explicit support. interactionDelete
        // clears counter2; getFreeInteractionSlot itself does not clear it.
        if (target is null)
        {
            _inactiveInteractionCounter2[slot] = unchecked((byte)value);
            return;
        }
        if (target is SmogEncounterRoomEntity smog)
        {
            smog.WriteCounter2Alias(value);
            return;
        }
        if (target is PuzzlePuffRoomEntity puff)
        {
            puff.WriteCounter2Alias(value);
            return;
        }
        if (target is DungeonRewardRoomEntity reward)
        {
            reward.WriteCounter2Alias(value);
            return;
        }
        if (target is SwordBeamClinkRoomEntity clink)
        {
            clink.WriteCounter2Alias(value);
            return;
        }
        if (target is KillPuffRoomEntity killPuff)
        {
            killPuff.WriteCounter2Alias(value);
            return;
        }
        if (target is DungeonDoorRoomEntity door)
        {
            door.WriteCounter2Alias(value);
            return;
        }
        throw new NotSupportedException($"smog.s writes ${value:x2} to INTERACTION page${0xd0+slot:x2} counter2 ($47); active {target.GetType().Name} does not represent that alias.");
    }

    private bool TryCreatePuzzlePuff(Vector2 position)
    {
        if (FindFreeInteractionSlot() < 0) return false;
        AddEntity(_factory.Create(new PuzzlePuffSpawn(position,OracleSoundEngine.SndPoof),_roomForActiveEntities));
        return true;
    }

    private int TryCreateTransformationPuff(Vector2 position)
    {
        if (!InteractionSlotAvailable) return -1;
        IRoomEntity puff = AddEntity(_factory.Create(new PuzzlePuffSpawn(
            position, OracleSoundEngine.SndPoof, AlwaysUpdates: false), _roomForActiveEntities));
        return _interactionSlots[puff];
    }

    private int InteractionAnimationParameter(int slot)
    {
        IRoomEntity? owner = _interactionSlots.FirstOrDefault(pair => pair.Value == slot).Key;
        if (owner is null or IRoomEntityLifetime { Finished: true }) return 0;
        return owner.Node switch
        {
            PuzzlePuffEffect puff => puff.Initialized ? puff.CurrentParameter : 0,
            ClinkEffect clink => clink.AnimationParameter,
            KillEnemyPuffEffect puff => puff.AnimationParameter,
            FallingDownHoleEffect fall => fall.CurrentParameter,
            OwlStatueSparkleEffect sparkle => sparkle.AnimationParameter,
            DungeonKeyUseEffect key => key.AnimationParameter,
            KnockbackDustRoomEntity dust => dust.AnimationParameter,
            NpcCharacter npc => npc.CurrentAnimationParameter,
            _ => throw new NotSupportedException($"spark_stateA relatedObj2 INTERACTION$d{slot:x1} was reused by " +
                $"{owner.GetType().Name}; its native animParameter is not represented.")
        };
    }

    private void TryCreateSparkFairy(Vector2 position, int angle)
    {
        // ecom_spawnProjectile checks PART capacity. spark_stateA deletes the
        // enemy regardless of success and never runs enemyDie/kill counters.
        if (FindFreePartSlot() >= 0)
            AddEntity(_factory.Create(new ItemDropSpawn(ItemDropDatabase.Fairy, position, angle), _roomForActiveEntities));
    }

    private bool TryCreateKnockbackDust(Vector2 position, int z)
    {
        if (!InteractionSlotAvailable) return false;
        AddEntity(new KnockbackDustRoomEntity(position, z));
        return true;
    }

    internal void TryCreateBoomerangClink(Vector2 position, int zHigh)
    {
        if (!InteractionSlotAvailable) return;
        AddEntity(_factory.Create(new EnemyClinkSpawn(position, InitializeOnUpdate: true, zHigh), _roomForActiveEntities));
    }

    private bool TryCreateOwlSparkle(OwlStatueSparkleSpawn spawn)
    {
        if (FindFreeInteractionSlot() < 0) return false;
        AddEntity(_factory.Create(spawn, _roomForActiveEntities));
        return true;
    }

    private bool TryCreateStatueEyeball(Vector2 position)
    {
        if (FindFreeInteractionSlot() < 0) return false;
        AddEntity(_factory.Create(new StatueEyeballSpawn(position), _roomForActiveEntities));
        return true;
    }

    internal bool TryCreateRockDebris(Vector2 position)
    {
        if (FindFreeInteractionSlot() < 0) return false;
        var entity = _factory.Create(new RockDebrisSpawn(position),_roomForActiveEntities);
        AddEntity(entity);
        _interactionSlots[entity] = FindFreeInteractionSlot();
        ApplyInteractionDrawOrder(entity, _interactionSlots[entity]);
        return true;
    }

    private bool TryCreateSeedReflectorChild(RotatableSeedThingRoomEntity parent,Vector2 offset,int z)
    {
        if (FindFreePartSlot() < 0) return false;
        AddEntity(new SeedReflectorChildRoomEntity(parent,offset,z));
        return true;
    }

    private bool TryCreateLightableTorch(LightableTorchState state,int packedPosition)
    {
        if (FindFreePartSlot() < 0) return false;
        AddEntity(_factory.Create(new LightableTorchSpawn(state,packedPosition),_roomForActiveEntities));
        return true;
    }

    private bool UpdatesDuringRoomEntityFreeze(IRoomEntity entity) =>
        entity is IUpdatesDuringRoomEntityFreeze { UpdatesDuringRoomEntityFreeze: true } ||
        entity is IRoomEntityUpdateFreeze { FreezesRoomEntities: true } ||
        EntityPhase(entity) == 2 &&
        FloorToggle?.Frozen != true &&
        entity is not (IPlayerProjectileRoomEntity or ISeedProjectileRoomEntity or BombRoomEntity or IPlayerRideableRoomEntity) &&
        !_activeEntities.Any(candidate =>
            candidate is IRoomEntityUpdateFreeze { FreezesRoomEntities: true, FreezesInteractions: true });

    private bool HasPlayerRestriction(
        Func<IPlayerRestriction, bool> predicate)
    {
        if (FloorToggle is not null && predicate(FloorToggle)) return true;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IPlayerRestriction restriction &&
                predicate(restriction))
            {
                return true;
            }
        }
        return false;
    }

    private void RemoveFinishedEntities(RoomEntityFrame? frame = null)
    {
        for (int index = _activeEntities.Count - 1; index >= 0; index--)
        {
            IRoomEntity entity = _activeEntities[index];
            ApplyEnemyOutcomes(entity);
            if (entity is not IRoomEntityLifetime { Finished: true } lifetime)
                continue;
            lifetime.OnFinished(_pendingSpawns);
            _activeEntities.RemoveAt(index);
            FreeEntity(entity);
        }
        ProcessSpawns(frame);
    }

    private void ApplyEnemyOutcomes(IRoomEntity entity)
    {
        if (entity is not IRoomEnemyOutcomeSource source)
            return;

        while (source.TryTakeEnemyOutcome(out RoomEnemyOutcome outcome))
        {
            if (outcome.MarksRecentDefeat &&
                outcome.KillableEnemyIndex > 0)
            {
                _recentEnemyDefeats.MarkKilled(
                    outcome.KillableEnemyIndex);
            }
            if (outcome.AdvancesKillCounters)
                EnemyDefeated?.Invoke();
        }
    }

    private void UpdateAlwaysEntitiesDuringScreenTransition(double delta, Player player)
    {
        _screenTransitionFrameAccumulator += delta * 60.0;
        while (_screenTransitionFrameAccumulator >= 1.0)
        {
            _screenTransitionFrameAccumulator -= 1.0;
            // bank0 mainThread advances wFrameCounter before runGameLogic,
            // including scroll updates that dispatch enabled bit-$80 objects.
            AdvanceFrozenRoomFrame();
            var frame = new RoomEntityFrame(player, _enemyFrameCounter, false, null);
            ReservedKeyDoor?.UpdateDuringScreenTransition();
            ReservedPushBlock?.UpdateDuringScreenTransition(player);
            foreach (IRoomEntity entity in ScreenTransitionUpdateOrder())
                if (entity is IAlwaysUpdateDuringScreenTransitionRoomEntity always)
                    always.UpdateDuringScreenTransition(frame);
            RemoveFinishedScreenTransitionEntities(_outgoingEntities);
            RemoveFinishedScreenTransitionEntities(_activeEntities);
        }
    }

    private void RemoveFinishedScreenTransitionEntities(
        List<IRoomEntity> entities)
    {
        for (int index = entities.Count - 1; index >= 0; index--)
        {
            IRoomEntity entity = entities[index];
            if (entity is not IAlwaysUpdateDuringScreenTransitionRoomEntity ||
                entity is not IRoomEntityLifetime { Finished: true } lifetime)
            {
                continue;
            }
            lifetime.OnFinished(_pendingSpawns);
            entities.RemoveAt(index);
            FreeEntity(entity);
        }
        if (_pendingSpawns.Count != 0)
        {
            throw new InvalidOperationException(
                "An always-updating screen-transition presentation tried to spawn a room entity.");
        }
    }

    private IEnumerable<IRoomEntity> ScreenTransitionUpdateOrder()
    {
        foreach (IRoomEntity entity in _outgoingEntities.Concat(_activeEntities).Where(entity =>
            !_enemySlots.ContainsKey(entity) && !_interactionSlots.ContainsKey(entity) && !_partSlots.ContainsKey(entity)).ToArray())
            yield return entity;
        // The scroll gate changes eligibility, not updateInteractions' live
        // ascending walk. A chest puff allocated above this cursor must run
        // state 0 in the same pass; a child below it waits until the next pass.
        foreach (var slots in new[] { _enemySlots, _partSlots, _interactionSlots })
            for (int slot = 0; slot < 16; slot++)
            {
                IRoomEntity? entity = slots.FirstOrDefault(pair => pair.Value == slot &&
                    pair.Key is not IRoomEntityLifetime { Finished: true }).Key;
                if (entity is not null)
                    yield return entity;
            }
    }

    private void ClearEntities(List<IRoomEntity> entities)
    {
        foreach (IRoomEntity entity in entities)
        {
            int slot = _enemySlots.GetValueOrDefault(entity, -1);
            FreeEntity(entity);
            // clearObjectsWithEnabled2 clears all $40 object bytes. Unlike
            // a handler's enemyDelete path it cannot retain counter1.
            if (slot >= 0)
                _deletedEnemyCounter1[slot] = 0;
        }
        entities.Clear();
    }

    private void FreeEntity(IRoomEntity entity)
    {
        _interactionSlots.Remove(entity);
        _partSlots.Remove(entity);
        if (_enemySlots.Remove(entity, out int enemySlot))
        {
            if (entity is INativeEnemyCounter1RoomEntity { RetainsCounter1AfterDeletion: true } counter)
                _deletedEnemyCounter1[enemySlot] = checked((byte)counter.Counter1);
            _reservedEnemySlots.Remove(enemySlot);
        }
        if (entity is INpcTalkLifecycle lifecycle &&
            _npcTalkLifecycles.TryGetValue(
                lifecycle.TalkNpc, out INpcTalkLifecycle? registered) &&
            ReferenceEquals(registered, lifecycle))
        {
            _npcTalkLifecycles.Remove(lifecycle.TalkNpc);
        }
        Node2D node = entity.Node;
        if (!GodotObject.IsInstanceValid(node) ||
            node.IsQueuedForDeletion())
        {
            return;
        }
        if (node.GetParent() == _worldRoot)
            _worldRoot.RemoveChild(node);
        node.QueueFree();
    }

    private void OnTimePortalEntered(TimePortal portal) => TimePortalEntered?.Invoke(portal);
    private void OnGroundTreasureCollected(
        GroundTreasurePickup treasure,
        Player player) =>
        ActivateGroundTreasure(treasure, player, immediate: false);

    private void ActivateGroundTreasure(
        GroundTreasurePickup treasure,
        Player player,
        bool immediate)
    {
        GroundTreasureDatabaseRecord record = treasure.Record;
        if (!immediate &&
            (record.SoundOrder != GroundTreasureSoundOrder.BehaviourThenGrab ||
             record.DialogueTiming != GroundTreasureDialogueTiming.BeforeGrab ||
             record.CompletionOwner !=
                GroundTreasureCompletionOwner.SharedInteraction))
        {
            throw new InvalidOperationException(
                $"Collectible ground treasure from {record.Source} has " +
                "immediate-grant-only policy.");
        }
        if (_inventory is null)
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} cannot write inventory " +
                "without an InventoryState.");
        }

        TreasureObjectRecord treasureObject =
            _treasures.GetObject(record.TreasureObject);
        switch (record.InventoryWrite)
        {
            case GroundTreasureInventoryWrite.TreasureObject:
                _inventory.GiveTreasure(treasureObject);
                break;
            case GroundTreasureInventoryWrite.UnappraisedRing:
                _inventory.GiveUnappraisedRing(record.InventoryParameter);
                break;
            default:
                throw new InvalidOperationException(
                    $"Ground treasure from {record.Source} has unsupported " +
                    $"inventory policy {record.InventoryWrite}.");
        }

        if (record.RoomFlagTiming == GroundTreasureRoomFlagTiming.OnActivation)
        {
            if (_saveData is null)
            {
                throw new InvalidOperationException(
                    $"Ground treasure from {record.Source} cannot set " +
                    "ROOMFLAG_ITEM without OracleSaveData.");
            }
            _saveData.SetRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlagItem);
        }

        if (record.GrabMode != 3 && record.SoundOrder == GroundTreasureSoundOrder.BehaviourThenGrab)
            PlayGroundTreasureBehaviourSound(treasureObject);

        GroundTreasureCollected?.Invoke(treasure, player);
        if (record.DialogueTiming == GroundTreasureDialogueTiming.BeforeGrab)
            RequestGroundTreasureDialogue(treasure, treasureObject, player);

        if (!immediate)
            return;

        treasure.BeginGranted(player);
        if (record.GrabMode != 3 && record.SoundOrder == GroundTreasureSoundOrder.GrabThenBehaviour)
            PlayGroundTreasureBehaviourSound(treasureObject);
        if (record.DialogueTiming == GroundTreasureDialogueTiming.AfterGrab)
            RequestGroundTreasureDialogue(treasure, treasureObject, player);
    }

    private void PlayGroundTreasureBehaviourSound(
        TreasureObjectRecord treasure)
    {
        int sound = _treasures.GetBehaviour(treasure.TreasureId).Sound;
        if (sound != 0)
            OnSoundRequested(sound);
    }

    private void RequestGroundTreasureDialogue(
        GroundTreasurePickup treasure,
        TreasureObjectRecord treasureObject,
        Player player)
    {
        if (!string.IsNullOrEmpty(treasureObject.Message))
        {
            GroundTreasureDialogueRequested?.Invoke(
                treasure, treasureObject, player);
        }
    }
    private void OnDungeonEntranceTriggered(int textId, string message) =>
        DungeonEntranceTriggered?.Invoke(textId, message);
    private void OnRoomWarpRequested(Warp warp) =>
        _pendingRoomWarp = warp;
    private void OnItemDropEnteredHazard(
        Vector2 position,
        HazardType hazard) => ItemDropEnteredHazard?.Invoke(position, hazard);
    private void OnObjectFellInHole(ObjectFellInHoleKind kind) =>
        ObjectFellInHole?.Invoke(kind);

    private void OnDungeonEssenceTriggered(
        DungeonEssence essence,
        Player player) => DungeonEssenceTriggered?.Invoke(essence, player);

    private void OnRoomEntityDialogueRequested(
        int textId,
        string message,
        Vector2 position) =>
        RoomEntityDialogueRequested?.Invoke(textId, message, position);

    private void OnMapleDialogueRequested(
        int textId,
        string message,
        Player player) =>
        MapleDialogueRequested?.Invoke(textId, message, player);

    private void OnMapleItemCollected(
        MapleItemRecord item,
        Player player) =>
        MapleItemCollected?.Invoke(item, player);

    private void OnSeedTreeMessageRequested(
        int textId,
        string message,
        Vector2 position) =>
        SeedTreeMessageRequested?.Invoke(textId, message, position);

    private void OnOwlStatueMessageRequested(
        int textId,
        string message,
        Vector2 position) =>
        OwlStatueMessageRequested?.Invoke(textId, message, position);

    private void OnRoomTileChanged() => RoomTileChanged?.Invoke();
    private void OnSoundRequested(int sound) => SoundRequested?.Invoke(sound);
    private void OnRoomMusicRequested(int group, int room) =>
        RoomMusicRequested?.Invoke(group, room);

    private void DispatchPendingRoomWarp()
    {
        if (_pendingRoomWarp is not { } warp)
            return;
        _pendingRoomWarp = null;
        RoomWarpRequested?.Invoke(warp);
    }

    private static List<T> SelectNodes<T>(IEnumerable<IRoomEntity> entities) where T : Node2D
    {
        var result = new List<T>();
        foreach (IRoomEntity entity in entities)
        {
            if (entity.Node is T node)
                result.Add(node);
        }
        return result;
    }

    private static bool ReadGameButtonJustPressed() =>
        Input.IsActionJustPressed("attack") ||
        Input.IsActionJustPressed("item") ||
        Input.IsActionJustPressed("move_up") ||
        Input.IsActionJustPressed("move_right") ||
        Input.IsActionJustPressed("move_down") ||
        Input.IsActionJustPressed("move_left");
}

internal readonly record struct RoomEntityManagerState(
    byte ActiveTriggers,
    double FrameAccumulator,
    int FrameCounter,
    RecentEnemyDefeatsState RecentEnemyDefeats);

internal readonly record struct RoomEntityFrame(
    Player Player,
    int Counter,
    bool AnyButtonJustPressed,
    Vector2? ScentSeedTarget = null,
    int SwitchHookState = 0);

internal sealed record SwordBeamSpawn(Vector2 LinkPosition, int Direction)
    : RoomEntitySpawn;

internal sealed record ItemDropSpawn(
    int SubId,
    Vector2 Position,
    int Angle = 0,
    bool DugUp = false,
    bool UpdateThisFrame = false,
    int ZHigh = 0) : RoomEntitySpawn(UpdateThisFrame);

internal sealed record FallingDownHoleSpawn(Vector2 Position, bool Silent = false) : RoomEntitySpawn;

internal enum ObjectFellInHoleKind
{
    Bomb = 0,
    Bombchu = 1,
    CaneOfSomariaBlock = 2,
    EmberSeed = 3,
    ScentSeed = 4,
    GaleSeed = 5,
    MysterySeed = 6,
    BraceletObject = 7,
    PushBlock = 8,
    HarmlessHardhatBeetle = 9
}

internal sealed record EnemySplashSpawn(
    Vector2 Position,
    HazardType Hazard) : RoomEntitySpawn;
