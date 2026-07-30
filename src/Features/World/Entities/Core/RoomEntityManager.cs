using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Owns the active and outgoing room-entity sets. Per-type behavior is exposed
/// through small capability interfaces and constructed by RoomEntityFactory.
/// </summary>
public sealed class RoomEntityManager : IDisposable
{
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
    internal event Action<SpiritsGraveEssence, Player>? SpiritsGraveEssenceTriggered;
    internal event Action<int, string, Player>? MapleDialogueRequested;
    internal event Action<MapleItemRecord, Player>? MapleItemCollected;
    internal event Action<int, string, Vector2>? SeedTreeMessageRequested;
    internal event Action<int, string, Vector2>? OwlStatueMessageRequested;
    public event Action<int>? SoundRequested;
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
    private bool _linkCollisionsAndMenuDisabled;
    private bool _disposed;

    internal Func<bool> GameButtonJustPressedSource { get; set; } =
        ReadGameButtonJustPressed;
    internal Func<bool> GroundTreasureCollectionAllowed { get; set; } =
        static () => true;
    internal Func<Vector2, Vector2> WorldToScreen { get; set; } =
        static position => position;
    internal Func<bool> TextActiveSource { get; set; } =
        static () => false;
    internal Func<int> PlayingInstrumentSource { get; set; } =
        static () => 0;

    public bool ScreenTransitionActive => _screenTransitionActive;
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
        _linkCollisionsAndMenuDisabled;
    internal bool IsDisposed => _disposed;
    internal bool PlayerRidingObject
    {
        get
        {
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is IPlayerRideableRoomEntity { LinkRiding: true })
                    return true;
            }
            return false;
        }
    }
    internal bool HasActiveSeedProjectile
    {
        get
        {
            foreach (IRoomEntity entity in _activeEntities)
            {
                if (entity is ISeedProjectileRoomEntity &&
                    entity is not IRoomEntityLifetime { Finished: true })
                {
                    return true;
                }
            }
            return false;
        }
    }
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
    public bool PlayerItemUsageDisabled
        => HasPlayerRestriction(static restriction => restriction.DisablesItems);
    public bool PlayerMovementDisabled
    {
        get
        {
            if (HasPlayerRestriction(
                    static restriction => restriction.DisablesMovement))
                return true;
            return PlayerSwordDisabled && (_enemyFrameCounter & 1) != 0;
        }
    }
    public bool PlayerMenusDisabled
    {
        get
        {
            if (_linkCollisionsAndMenuDisabled)
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
            enemyIndex => _recentEnemyDefeats.WasKilled(enemyIndex),
            TriggerIsActive, () => _activeTriggers, SetTrigger,
            OnRoomTileChanged,
            OnSpiritsGraveEssenceTriggered,
            BossShuttersClosed,
            BeginScreenShake,
            DisableLinkCollisionsAndMenu,
            EnableLinkCollisionsAndMenu,
            OnRoomMusicRequested,
            OnMapleDialogueRequested,
            OnSeedTreeMessageRequested,
            OnOwlStatueMessageRequested,
            () => TextActiveSource(),
            OnMapleItemCollected,
            BeginHorizontalScreenShake,
            position => WorldToScreen(position), _animationTick, rooms);
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

    public List<T> Entities<T>() where T : Node2D => SelectNodes<T>(_activeEntities);
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

    public void BeginScreenTransition(int group, OracleRoomData room, Vector2 incomingOffset)
    {
        BeginScreenTransition(
            group, room, incomingOffset, EnemyPlacementContext.Unrestricted);
    }

    internal void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        Vector2I scrollDirection,
        int entryPackedPosition)
    {
        BeginScreenTransition(
            group, room, incomingOffset,
            EnemyPlacementContext.Scrolling(
                scrollDirection, entryPackedPosition));
    }

    private void BeginScreenTransition(
        int group,
        OracleRoomData room,
        Vector2 incomingOffset,
        EnemyPlacementContext placementContext)
    {
        // updateSeedTreeRefillData runs after getNextActiveRoom only when the
        // outgoing tileset is outdoors. Warp/direct loads bypass this path.
        if ((_roomForActiveEntities.TilesetFlags & 0x01) != 0)
            _factory.UpdateSeedTreeRefillState(group, room.Id);
        ClearEntities(_outgoingEntities);
        _outgoingEntities.AddRange(_activeEntities);
        _activeEntities.Clear();
        _screenTransitionActive = true;
        _screenTransitionFrameAccumulator = 0.0;
        _roomForActiveEntities = room;
        AddRoomEntities(group, room, placementContext);
        PrepareIncomingEntitiesForScreenTransition();
        SetScreenTransitionOffsets(Vector2.Zero, incomingOffset);
    }

    public void SetScreenTransitionOffsets(Vector2 outgoingOffset, Vector2 incomingOffset)
    {
        if (!_screenTransitionActive)
            return;
        foreach (IRoomEntity entity in _outgoingEntities)
            entity.SetTransitionDrawOffset(outgoingOffset);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(incomingOffset);
    }

    public void FinishScreenTransition()
    {
        if (!_screenTransitionActive)
            return;
        ClearEntities(_outgoingEntities);
        foreach (IRoomEntity entity in _activeEntities)
            entity.SetTransitionDrawOffset(Vector2.Zero);
        _screenTransitionActive = false;
        _screenTransitionFrameAccumulator = 0.0;
    }

    public void Update(double delta, Player player)
    {
        // The original engine freezes both enabled $02 outgoing objects and
        // enabled $01 destination objects until scrolling has completed,
        // except interactions carrying the explicit always-update bit.
        if (_screenTransitionActive)
        {
            UpdateAlwaysEntitiesDuringScreenTransition(delta);
            return;
        }

        bool textActive = TextActiveSource();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (textActive && !UpdatesDuringDialogue(entity))
                continue;
            if (entity is IVariableRoomEntity variableEntity)
                variableEntity.Update(delta, player);
        }

        bool anyButtonJustPressed = GameButtonJustPressedSource();
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            var frame = new RoomEntityFrame(player, _enemyFrameCounter, anyButtonJustPressed);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if ((!textActive || UpdatesDuringDialogue(entity)) &&
                    entity is IPlayerForcedMovement forcedMovement)
                {
                    forcedMovement.UpdatePlayerForcedMovement(player);
                }
            }
            if (!textActive)
            {
                ResolvePlayerProjectileCollisions();
                ResolveBombExplosionCollisions();
                ResolveMapleBombPulling();
            }

            // updateInteractions/updateParts still run newly allocated state
            // 0 objects while wTextIsActive is set. UpdateThisFrame spawns
            // model that creation update and therefore remain unconditional.
            ProcessSpawns(frame);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (textActive && !UpdatesDuringDialogue(entity))
                    continue;
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
                }
                ProcessSpawns(frame);
            }
            if (!textActive)
                ResolveSeedCollisions();
            ProcessSpawns(frame);
            UpdateScreenShake();
            anyButtonJustPressed = false;
        }

        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (!textActive &&
                !_linkCollisionsAndMenuDisabled &&
                player.AcceptsRoomEntityContact &&
                entity is ILinkContactEntity contactEntity)
            {
                contactEntity.HandleLinkContact(player);
            }
        }
        RemoveFinishedEntities();
        DispatchPendingRoomWarp();
    }

    internal void UpdateDuringHarp(double delta, Player player)
    {
        // ITEM_HARP writes $7e to wDisabledObjects: every ordinary object
        // category is frozen. The dormant portal spawner explicitly carries
        // the interaction always-update bit, as do the floating notes owned
        // by HarpController.
        _enemyFrameAccumulator += delta * 60.0;
        while (_enemyFrameAccumulator >= 1.0)
        {
            _enemyFrameAccumulator -= 1.0;
            _enemyFrameCounter = (_enemyFrameCounter + 1) & 0xff;
            var frame = new RoomEntityFrame(
                player, _enemyFrameCounter, AnyButtonJustPressed: false);
            foreach (IRoomEntity entity in _activeEntities.ToArray())
            {
                if (entity is TimePortalRoomEntity portal)
                    portal.UpdateFrame(frame, _pendingSpawns);
            }
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
        Vector2I releaseDirection)
    {
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is IBraceletInteractableRoomEntity bracelet &&
                bracelet.TryUseBracelet(player, releaseDirection))
            {
                return true;
            }
        }
        return false;
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
        bool collectItemDrops = false)
    {
        bool hit = false;
        Vector2 source = sourcePosition ?? hitbox.GetCenter();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (collectItemDrops &&
                entity is ILinkSwordCollectibleRoomEntity collectible)
            {
                // COLLISIONEFFECT_23 does not write Item.var2a, so collecting
                // a drop must not count as enemy contact for the sword.
                collectible.TryCollectWithSword(hitbox);
            }
            if (entity is ISwordHittableRoomEntity swordHittable)
                hit |= swordHittable.ApplySwordHit(
                    hitbox,
                    source,
                    damage,
                    knockbackStrength,
                    _pendingSpawns);
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
        Vector2 source = hitbox.GetCenter();
        foreach (IRoomEntity entity in _activeEntities.ToArray())
        {
            if (entity is ISwordHittableRoomEntity hittable)
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
                hittable.ApplySwordHit(
                    hitbox,
                    source,
                    damage,
                    EnemyKnockbackStrength.Normal,
                    _pendingSpawns);
            }
            ProcessSpawns();
        }
        RemoveFinishedEntities();
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

    private void ResolveSeedCollisions()
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
                if (target is not ISeedHittableRoomEntity hittable)
                    continue;
                SeedHitResult result = hittable.ApplySeedHit(
                    seed.CollisionBounds,
                    seed.CollisionBounds.GetCenter(),
                    seed.SeedItem,
                    _pendingSpawns);
                if (result == SeedHitResult.None)
                {
                    continue;
                }
                seed.OnCollision(result, hittable as ISeedBurnTarget);
                break;
            }
        }
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
                if (target is not ISwordHittableRoomEntity hittable)
                    continue;
                if (!hittable.ApplySwordHit(
                    projectile.CollisionBounds,
                    projectile.CollisionBounds.GetCenter(),
                    projectile.Damage,
                    EnemyKnockbackStrength.Normal,
                    _pendingSpawns))
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
                if (ReferenceEquals(entity, target) ||
                    target is not ISwordHittableRoomEntity hittable)
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
                hittable.ApplySwordHit(
                    bomb.CollisionBounds,
                    bomb.CollisionBounds.GetCenter(),
                    bomb.Damage,
                    EnemyKnockbackStrength.High,
                    _pendingSpawns);
                ProcessSpawns();
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

    internal T Spawn<T>(RoomEntitySpawn spawn) where T : Node2D
    {
        IRoomEntity entity = AddEntity(_factory.Create(spawn, _roomForActiveEntities));
        return (T)entity.Node;
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

    public void Clear()
    {
        ClearEntities(_outgoingEntities);
        ClearEntities(_activeEntities);
        _pendingSpawns.Clear();
        _pendingRoomWarp = null;
        _screenTransitionActive = false;
        _enemyFrameAccumulator = 0.0;
        _screenTransitionFrameAccumulator = 0.0;
        _screenShakeCounter = 0;
        _horizontalScreenShakeCounter = 0;
        _linkCollisionsAndMenuDisabled = false;
        ScreenShakeChanged?.Invoke(Vector2.Zero);
    }

    internal void ClearRecentEnemyDefeats() => _recentEnemyDefeats.Clear();

    private void AddRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        // loadTilesetAndRoomLayout runs the common tile substitutions before
        // parseObjectData. Ordinary $78-$7b shutters can exist only in that
        // layout and therefore must be opened before placed entities are read.
        _factory.ApplyEntryShutterSubstitution(room, placementContext);
        // wActiveTriggers is room-local scratch state cleared by room loading.
        _activeTriggers = 0;
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
        int count = 0;
        foreach (IRoomEntity entity in _activeEntities)
        {
            if (entity is IRoomEnemyCounterEntity { CountsAsEnemy: true })
                count++;
        }
        return count;
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

    private void UpdateScreenShake()
    {
        if (_screenShakeCounter == 0 &&
            _horizontalScreenShakeCounter == 0)
            return;
        int[] amounts = { -2, -1, 1, 2 };
        int y = 0;
        int x = 0;
        if (_screenShakeCounter != 0)
        {
            y = amounts[_random.Next().Value & 3];
            x = amounts[_random.Next().Value & 3];
            _screenShakeCounter--;
        }
        if (_horizontalScreenShakeCounter != 0)
        {
            x = amounts[_random.Next().Value & 3];
            _horizontalScreenShakeCounter--;
        }
        Vector2 offset = new(x, y);
        ScreenShakeChanged?.Invoke(offset);
        if (_screenShakeCounter == 0 &&
            _horizontalScreenShakeCounter == 0)
            ScreenShakeChanged?.Invoke(Vector2.Zero);
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
        _worldRoot.AddChild(entity.Node);
        return entity;
    }

    private void ProcessSpawns(RoomEntityFrame? frame = null)
    {
        while (_pendingSpawns.Count > 0)
        {
            RoomEntitySpawn spawn = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            IRoomEntity entity = AddEntity(_factory.Create(spawn, _roomForActiveEntities));
            if (spawn.UpdateThisFrame && frame.HasValue && entity is IFixedRoomEntity fixedEntity)
            {
                SynchronizeEnemyFrameCounter(entity, frame.Value.Counter);
                fixedEntity.UpdateFrame(frame.Value, _pendingSpawns);
            }
        }
    }

    private void PrepareIncomingEntitiesForScreenTransition()
    {
        // parseObjectData and the destination's source state-0 creation work
        // make presentation children drawable before scrolling exposes their
        // part of the room. Ordinary state updates remain frozen afterward.
        // The list may grow while a preloader creates source-ordered children,
        // so walk by index until the complete transitive set is prepared.
        for (int index = 0; index < _activeEntities.Count; index++)
        {
            if (_activeEntities[index] is not
                IScreenTransitionPreloadRoomEntity preloader)
            {
                continue;
            }
            preloader.PrepareForScreenTransition(_pendingSpawns);
            ProcessScreenTransitionPreloadSpawns();
        }
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

    private bool HasPlayerRestriction(
        Func<IPlayerRestriction, bool> predicate)
    {
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

    private void RemoveFinishedEntities()
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
        ProcessSpawns();
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

    private void UpdateAlwaysEntitiesDuringScreenTransition(double delta)
    {
        _screenTransitionFrameAccumulator += delta * 60.0;
        while (_screenTransitionFrameAccumulator >= 1.0)
        {
            _screenTransitionFrameAccumulator -= 1.0;
            UpdateAlwaysEntitiesDuringScreenTransition(_outgoingEntities);
            UpdateAlwaysEntitiesDuringScreenTransition(_activeEntities);
        }
    }

    private void UpdateAlwaysEntitiesDuringScreenTransition(
        List<IRoomEntity> entities)
    {
        foreach (IRoomEntity entity in entities.ToArray())
        {
            if (entity is IAlwaysUpdateDuringScreenTransitionRoomEntity always)
                always.UpdateDuringScreenTransition();
        }

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

    private void ClearEntities(List<IRoomEntity> entities)
    {
        foreach (IRoomEntity entity in entities)
            FreeEntity(entity);
        entities.Clear();
    }

    private void FreeEntity(IRoomEntity entity)
    {
        if (entity is INpcTalkLifecycle lifecycle &&
            _npcTalkLifecycles.TryGetValue(
                lifecycle.TalkNpc, out INpcTalkLifecycle? registered) &&
            ReferenceEquals(registered, lifecycle))
        {
            _npcTalkLifecycles.Remove(lifecycle.TalkNpc);
        }
        if (entity.Node.GetParent() == _worldRoot)
            _worldRoot.RemoveChild(entity.Node);
        entity.Node.QueueFree();
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

        if (record.SoundOrder == GroundTreasureSoundOrder.BehaviourThenGrab)
            PlayGroundTreasureBehaviourSound(treasureObject);

        GroundTreasureCollected?.Invoke(treasure, player);
        if (record.DialogueTiming == GroundTreasureDialogueTiming.BeforeGrab)
            RequestGroundTreasureDialogue(treasure, treasureObject, player);

        if (!immediate)
            return;

        treasure.BeginGranted(player);
        if (record.SoundOrder == GroundTreasureSoundOrder.GrabThenBehaviour)
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

    private void OnSpiritsGraveEssenceTriggered(
        SpiritsGraveEssence essence,
        Player player) => SpiritsGraveEssenceTriggered?.Invoke(essence, player);

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
    bool AnyButtonJustPressed);

internal sealed record SwordBeamSpawn(Vector2 LinkPosition, int Direction)
    : RoomEntitySpawn;

internal sealed record ItemDropSpawn(
    int SubId,
    Vector2 Position,
    int Angle = 0,
    bool DugUp = false,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);

internal sealed record FallingDownHoleSpawn(Vector2 Position) : RoomEntitySpawn;

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
    PushBlock = 8
}

internal sealed record EnemySplashSpawn(
    Vector2 Position,
    HazardType Hazard) : RoomEntitySpawn;
