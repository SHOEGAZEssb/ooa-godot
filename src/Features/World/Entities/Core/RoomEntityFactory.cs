using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Creation shares the manager's authoritative state and named operations.
// Live providers are sampled by the owner when actors call them, including
// providers assigned after this factory and those actors were constructed.
internal sealed class RoomEntityFactory
{
    private readonly RoomEntityManager owner;
    private readonly EnemyDatabase enemies;
    private readonly TimePortalDatabase timePortals;
    private readonly RoomSession? rooms;
    internal RoomEntityResources Resources { get; }
    internal RoomEnemyFactory EnemyFactory { get; }

    private readonly BipinBlossomFamilyStateResolver _familyState;
    private readonly ItemDropDatabase _itemDrops;
    private readonly OracleRandom _random;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtimeState;
    private readonly InventoryState? _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly Func<long> _animationTick;
    private readonly MovingPlatformRidingState _platformRiding;

    internal RoomEntityFactory(RoomEntityManager owner, EnemyDatabase enemies,
        TimePortalDatabase timePortals, RoomSession? rooms)
    {
        this.owner = owner;
        this.enemies = enemies;
        this.timePortals = timePortals;
        this.rooms = rooms;
        _familyState = owner.FamilyStateResolver;
        _itemDrops = owner.ItemDrops;
        _random = owner.Random;
        _saveData = owner.SaveData;
        _runtimeState = owner.RuntimeState;
        _inventory = owner.Inventory;
        _treasures = owner.Treasures;
        _animationTick = owner.AnimationTick;
        _platformRiding = owner.PlatformRiding;
        Resources = new(rooms);
        EnemyFactory = new(owner, enemies, Resources, rooms);
    }

    internal IEnumerable<bool> PrepareResources() => Resources.PrepareResources();

    /// <summary>
    /// Mirrors replaceShutterForLinkEntering for layout shutters $78-$7f.
    /// The matching ordinary shutter becomes floor $a0; the matching minecart
    /// shutter becomes direction-appropriate track $5d/$5e so a ridden cart
    /// can enter the preloaded room during the scroll.
    /// </summary>
    internal void ApplyEntryShutterSubstitution(
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        if (placementContext.Kind != EnemyPlacementEntryKind.Scrolling ||
            placementContext.EntryPackedPosition < 0)
        {
            return;
        }

        int packedPosition = placementContext.EntryPackedPosition;
        Vector2 position = PointForPackedPosition(packedPosition);
        int tile = room.GetMetatile(position);
        if (!DungeonShutterEntry.TryGetReplacement(
                placementContext,
                packedPosition,
                tile,
                Resources.DungeonMechanics.OpenTile,
                out int replacement))
        {
            return;
        }

        room.SetPositionTileAndCollision(
            position, checked((byte)replacement), null, _animationTick());
    }

    internal void UpdateSeedTreeRefillState(
        int activeGroup,
        int activeRoom) =>
        Resources.SeedTrees.UpdateRefillState(
            _runtimeState, activeGroup, activeRoom);

    public IEnumerable<IRoomEntity> CreateRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        EnemyPlacementReservations.BeginRoomParse(_runtimeState);
        bool hasKeyholeController = Resources.KeyholeControllers.TryGet(group, room.Id, out var keyholeController)
            && _saveData is not null;
        // A leading controller allocates immediately; later controllers are
        // merged with their actual preceding companion placements below.
        if (hasKeyholeController && keyholeController.Order == 0)
            yield return new KeyholeControllerRoomEntity(keyholeController, _saveData!, rooms);
        // addRoomToEnemiesKilledList snapshots the room's current byte. Later
        // defeats update the history, not this parser scratch byte.
        _runtimeState.SetWramByte(EnemyPlacementMemory.KilledEnemies, owner.ActiveRoomDefeatBitset);
        var reservations = new EnemyPlacementReservations(_runtimeState);
        var deferredMermaidEnemies = new List<DungeonObjectRecord>();
        IEnumerable<IRoomEntity> InsertMermaidEnemies(int sourceOrder)
        {
            foreach (var record in deferredMermaidEnemies)
            {
                if (Resources.MermaidDungeon.EnemyStreamOrder(record) != sourceOrder || !owner.EnemySlotAvailable) continue;
                if (record.Kind == DungeonObjectKind.Vire)
                    yield return CreateVire(record,room,placementContext);
                else if (TryChooseRandomEnemyPosition(room,0x20,reservations,placementContext,out var position))
                    yield return CreateOctogon(record,position,placementContext);
            }
        }
        int activeGroup = group;
        Resources.KingMoblin.ApplyRoomLayout(group,room,_animationTick());
        // initializeRoom calls createSeaEffectsPartIfApplicable after room
        // code, before companions and the ordered placed object stream.
        if (SuctionPitDatabase.Shared.ShouldSpawn(room) && owner.PartSlotAvailable)
            yield return new SuctionPitRoomEntity(owner);
        if (group == Resources.Patch.ResetGroup && room.Id == Resources.Patch.ResetRoom)
            for (int address = 0xcfd0; address < 0xcfd8; address++) _runtimeState.SetWramByte(address, 0);
        foreach (var waterfall in Resources.WaterfallWarps.Records)
            if (waterfall.Group == group && waterfall.Room == room.Id && _saveData is not null)
                yield return new WaterfallWarpRoomEntity(waterfall, _saveData, _runtimeState,
                    () => CompanionRuntimeState.IsActive(_runtimeState, CompanionRuntimeState.DimitriId), owner.OnRoomWarpRequested);
        bool companionSlotActive = CompanionRuntimeState.AnyActive(_runtimeState);
        bool rickySpawnerActive = !companionSlotActive &&
            _saveData is not null &&
            Resources.Ricky.ShouldSpawn(activeGroup, room.Id, _saveData);
        IRoomEntity? companionEntity = null;
        ICompanionBarrierTarget? companionBarrierTarget = null;
        if (CompanionRuntimeState.IsActive(_runtimeState, CompanionRuntimeState.DimitriId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            if (active.Room == room.Id)
            {
                var dimitri = CreateDimitri(new(active.Position, active.Direction, activeGroup, room.Id, Riding: true), room);
                companionEntity = dimitri;
                companionBarrierTarget = dimitri;
            }
        }
        else if (CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RaftId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            if (active.Room == room.Id && _saveData is not null)
            {
                companionEntity = new RaftRoomEntity(
                    new RaftSpawn(active.Position, active.Direction,
                        activeGroup, room.Id, Riding: true),
                    room, Resources.Raft.Behavior, _runtimeState);
            }
        }
        else if (CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RickyId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            if (active.Room == room.Id)
            {
                RickyCompanionRoomEntity ricky = CreateRicky(new RickyCompanionSpawn(
                    active.Position,
                    active.Direction,
                    activeGroup,
                    room.Id,
                    Riding: true), room);
                companionEntity = ricky;
                companionBarrierTarget = ricky;
            }
        }
        else if (CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.MooshId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            if (active.Room == room.Id)
            {
                MooshCompanionRoomEntity moosh = CreateMoosh(new MooshCompanionSpawn(
                    active.Position,
                    active.Direction,
                    activeGroup,
                    room.Id,
                    Riding: true), room);
                companionEntity = moosh;
                companionBarrierTarget = moosh;
            }
        }
        else if (!companionSlotActive &&
            CompanionRuntimeState.TryGetRemembered(
                _runtimeState,
                CompanionRuntimeState.RickyId,
                activeGroup,
                room.Id,
                out Vector2 rememberedRicky))
        {
            RickyCompanionRoomEntity ricky = CreateRicky(new RickyCompanionSpawn(
                rememberedRicky,
                ObjectDirection.Down,
                activeGroup,
                room.Id), room);
            companionEntity = ricky;
            companionBarrierTarget = ricky;
        }
        else if (!companionSlotActive &&
            CompanionRuntimeState.TryGetRemembered(
                _runtimeState,
                CompanionRuntimeState.MooshId,
                activeGroup,
                room.Id,
                out Vector2 rememberedMoosh))
        {
            MooshCompanionRoomEntity moosh = CreateMoosh(new MooshCompanionSpawn(
                rememberedMoosh,
                ObjectDirection.Down,
                activeGroup,
                room.Id), room);
            companionEntity = moosh;
            companionBarrierTarget = moosh;
        }
        else if (!companionSlotActive && CompanionRuntimeState.TryGetRemembered(
            _runtimeState, SpecialObjectId.Dimitri, activeGroup, room.Id, out Vector2 rememberedDimitri))
        {
            var dimitri = CreateDimitri(new(rememberedDimitri, ObjectDirection.Down, activeGroup, room.Id), room);
            companionEntity = dimitri;
            companionBarrierTarget = dimitri;
        }
        else if (!companionSlotActive &&
            _saveData is not null &&
            _inventory is not null &&
            Resources.MooshGoodbye.ShouldSpawn(
                activeGroup, room.Id, _saveData, _inventory))
        {
            // INTERAC_COMPANION_SPAWNER $67:$01 writes only
            // wRememberedCompanionId after installing the fixed Moosh preset.
            MooshGoodbyeEventRecord goodbye = Resources.MooshGoodbye.Record;
            CompanionRuntimeState.InstallPreset(_runtimeState, new(goodbye.MooshX, goodbye.MooshY));
            MooshCompanionRoomEntity moosh = CreateMoosh(new MooshCompanionSpawn(
                new Vector2(goodbye.MooshX, goodbye.MooshY),
                goodbye.FlightAngle >> 3,
                activeGroup,
                room.Id,
                Goodbye: goodbye), room);
            companionEntity = moosh;
            companionBarrierTarget = moosh;
        }
        if (companionEntity is null && !companionSlotActive && _saveData is not null)
        {
            // companionSpawner.s subid $03 / preset $03.
            bool preset = Resources.Dimitri.ShouldSpawnPreset(activeGroup, room.Id, _saveData);
            if (preset)
            {
                // Room initialization restores a remembered companion before
                // $67:$03; the spawner then sees the occupied slot and deletes.
                Vector2 position = Resources.Dimitri.PresetPosition;
                CompanionRuntimeState.InstallPreset(_runtimeState, position);
                var dimitri = CreateDimitri(new(position, ObjectDirection.Down, activeGroup, room.Id), room);
                companionEntity = dimitri;
                companionBarrierTarget = dimitri;
            }
        }
        if (companionEntity is null && !companionSlotActive && !rickySpawnerActive && _saveData is not null &&
            Resources.Ricky.ShouldSpawnPreset(activeGroup, room.Id, _saveData))
        {
            var record = Resources.Ricky.Record;
            Vector2 position = new(record.RickyX, record.RickyY);
            CompanionRuntimeState.InstallPreset(_runtimeState, position);
            var ricky = CreateRicky(new(position, ObjectDirection.Down, activeGroup, room.Id), room);
            companionEntity = ricky;
            companionBarrierTarget = ricky;
        }
        if (companionEntity is not null)
            yield return companionEntity;
        bool raftCreated = companionEntity is RaftRoomEntity;
        if (!raftCreated && !companionSlotActive && _saveData is not null &&
            (room.TilesetFlags & Resources.Raft.Behavior.PastTilesetMask) != 0 &&
            CompanionRuntimeState.TryGetRemembered(
                _runtimeState, CompanionRuntimeState.RaftId,
                activeGroup, room.Id, out Vector2 rememberedRaft))
        {
            yield return new RaftRoomEntity(
                new RaftSpawn(rememberedRaft, ObjectDirection.Up, activeGroup, room.Id),
                room, Resources.Raft.Behavior, _runtimeState);
            raftCreated = true;
        }
        // Both placed subids pass through @subid1, which deletes the
        // interaction whenever w1Companion.id is SPECIALOBJECT_RAFT. During
        // scrolling that live owner still names the outgoing room, so this
        // guard must precede destination-room placement creation.
        if (!raftCreated && !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RaftId) && _saveData is not null)
        {
            foreach (RaftPlacement placement in
                Resources.Raft.GetPlacements(activeGroup, room.Id))
            {
                bool enabled = placement.SubId switch
                {
                    0 => (_saveData.ReadWramByte(
                            Resources.Raft.Behavior.DimitriStateAddress) &
                            Resources.Raft.Behavior.DimitriMask) != 0 &&
                        _saveData.HasGlobalFlag(Resources.Raft.Behavior.ChangedRoomsFlag),
                    1 => _saveData.HasGlobalFlag(
                        Resources.Raft.Behavior.ChangedRoomsFlag),
                    _ => throw new InvalidOperationException(
                        $"Placed INTERAC_RAFT has unsupported subid " +
                        $"${placement.SubId:x2} in {placement.Source}.")
                };
                if (!enabled)
                    continue;
                yield return new RaftRoomEntity(
                    new RaftSpawn(new Vector2(placement.X, placement.Y), ObjectDirection.Up,
                        activeGroup, room.Id),
                    room, Resources.Raft.Behavior, _runtimeState);
                raftCreated = true;
                break;
            }
        }
        if (_saveData is not null)
        {
            var placements = new Dictionary<int, IRoomEntity>();
            foreach (CompanionTutorialRecord tutorial in
                Resources.CompanionTutorials.GetRoomRecords(activeGroup, room.Id))
            {
                placements.Add(tutorial.Order, new CompanionTutorialRoomEntity(
                    tutorial,
                    _runtimeState,
                    _saveData,
                    owner.OnRoomEntityDialogueRequested));
            }
            foreach (CompanionBarrierRecord barrier in
                Resources.CompanionBarriers.GetRoomRecords(activeGroup, room.Id))
            {
                placements.Add(barrier.Order, new CompanionBarrierRoomEntity(
                    barrier,
                    companionBarrierTarget,
                    _saveData,
                    owner.OnRoomEntityDialogueRequested));
            }
            if (hasKeyholeController && keyholeController.Order != 0)
                placements.Add(keyholeController.Order, new KeyholeControllerRoomEntity(keyholeController, _saveData, rooms));
            // Keep allocation lazy. Only a consecutive supported
            // source prefix establishes native ordering; a gap does not reserve
            // imaginary slots for unmigrated NPC/script/spawner owners.
            int nextOrder = hasKeyholeController && keyholeController.Order == 0 ? 1 : 0;
            while (placements.Remove(nextOrder, out IRoomEntity? placement))
            {
                if (placement is CompanionBarrierRoomEntity barrier) barrier.NativeAllocationEnabled = true;
                if (placement is CompanionTutorialRoomEntity tutorial) tutorial.NativeAllocationEnabled = true;
                yield return placement;
                nextOrder++;
            }
            foreach (IRoomEntity placement in placements.Values)
            {
                if (placement is KeyholeControllerRoomEntity)
                    throw new InvalidOperationException($"{keyholeController.Source}: keyhole controller has an unsupported preceding object at source order {nextOrder}.");
                yield return placement;
            }
        }
        bool spawnMaple =
            _saveData is not null &&
            _inventory is not null &&
            Resources.Maple.IsEligibleLocation(
                activeGroup, room.Id, _inventory.AnimalCompanion) &&
            _saveData.MapleKillCounter >=
                RingEffects.MapleKillThreshold(_inventory);
        if (spawnMaple)
            _saveData!.SetMapleKillCounter(0);

        // The original object-pointer and tileset tables alias side-scrolling
        // groups $06/$07 to dungeon/interior groups $04/$05. Keep ActiveGroup
        // on the RoomSession, but resolve every placed object through the
        // shared source group just as getObjectDataAddress does.
        group = group switch
        {
            6 => 4,
            7 => 5,
            _ => group
        };
        foreach (DungeonSpinnerPlacement spinner in
            Resources.DungeonSpinners.GetRoomRecords(group, room.Id))
        {
            if (DungeonSpinnerDatabase.IsEnabled(spinner, _saveData) && owner.InteractionSlotAvailable)
            {
                yield return new DungeonSpinnerRoomEntity(
                    spinner,
                    _runtimeState,
                    Resources.DungeonVisuals.Visual("spinner"),
                    owner.OnSoundRequested,
                    owner.BeginScreenShake);
            }
        }
        if (group == Resources.Room2e3.Record.Group &&
            room.Id == Resources.Room2e3.Record.Room)
        {
            IRoomEntity? interaction = CreateRoom2e3Interaction();
            if (interaction is not null)
                yield return interaction;
        }
        if (group == Resources.Room5b6.Record.Group &&
            room.Id == Resources.Room5b6.Record.Room)
        {
            IRoomEntity? interaction = CreateRoom5b6Interaction();
            if (interaction is not null)
                yield return interaction;
        }
        if (group == Resources.Room5bf.Constants.Group &&
            room.Id == Resources.Room5bf.Constants.Room)
        {
            foreach (IRoomEntity interaction in CreateRoom5bfInteractions())
                yield return interaction;
        }
        foreach (IRoomEntity controller in
            CreateMinecartShutterControllers(room, placementContext))
        {
            yield return controller;
        }
        int dungeon = rooms?.World.GetDungeonIndex(group, room.Id) ?? -1;
        if (dungeon >= 0 || group>=4)
        {
            if(dungeon>=0) MinecartRuntimeState.EnsureInitialized(
                _runtimeState, Resources.StaticDungeonObjects.Minecarts(dungeon));
            foreach (ActiveMinecart cart in
                MinecartRuntimeState.StationaryInRoom(
                    _runtimeState, room.Id))
            {
                yield return CreateMinecart(cart, room);
            }
            if (MinecartRuntimeState.TryGetRide(
                    _runtimeState, room.Id, out ActiveMinecart ride))
            {
                yield return CreateMinecart(ride, room);
            }
        }
        IReadOnlyList<DarkRoomDatabaseRecord> darkRoomRecords =
            Resources.DarkRooms.GetRoomRecords(group, room.Id);
        if (darkRoomRecords.Count > 0)
        {
            var darkRoomState = new DarkRoomState(room, Resources.DarkRooms);
            foreach (DarkRoomDatabaseRecord record in darkRoomRecords)
            {
                yield return record.Kind switch
                {
                    DarkRoomDatabaseObjectKind.Reward =>
                        new DarkRoomRewardRoomEntity(
                            record, Resources.DarkRooms, darkRoomState, _saveData),
                    DarkRoomDatabaseObjectKind.Handler =>
                        new DarkRoomHandlerRoomEntity(
                            record, room, Resources.DarkRooms, darkRoomState),
                    _ => throw new InvalidOperationException(
                        $"Unsupported dark-room object kind in {record.Source}.")
                };
            }
        }

        // Buttons and trigger-controlled shutters use wActiveTriggers without
        // depending on the enemy roster. Push triggers require a complete live
        // wNumEnemies equivalent. Enemy-shutter controllers are retained even
        // when that count is incomplete so an incoming shutter can perform the
        // original entry substitution. That crossed route remains open for
        // safe backtracking; solving and non-entry shutters stay disabled.
        IReadOnlyList<DungeonMechanicDatabaseRecord> dungeonRecords =
            Resources.DungeonMechanics.GetRoomRecords(group, room.Id);
        LightableTorchState? lightableTorchState = null;
        foreach (DungeonMechanicDatabaseRecord record in dungeonRecords)
        {
            if (record is { Id: InteractionId.TriggerTranslator, SubId: 0x02 } or
                { Id: InteractionId.CreateObjectAtEachTileIndex, SubId: 0x08 })
            {
                lightableTorchState = new LightableTorchState();
                break;
            }
        }
        IReadOnlyList<PlacementRecord>
            sharedDungeonRecords = Resources.DungeonEntrances.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> spiritsGraveRecords =
            Resources.SpiritsGrave.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> wingDungeonRecords =
            Resources.WingDungeon.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> moonlitGrottoRecords =
            Resources.MoonlitGrotto.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> laterDungeonRecords =
            Resources.SkullDungeon.GetRoomRecords(group, room.Id).Concat(Resources.CrownDungeon.GetRoomRecords(group, room.Id))
                .Concat(Resources.MermaidDungeon.GetRoomRecords(group,room.Id))
                .Concat(Resources.TrapResets.GetRoomRecords(group,room.Id).Select(record => new DungeonObjectRecord(
                    group,room.Id,record.Order,DungeonObjectKind.PuzzleTrapReset,InteractionId.MiscPuzzles,0x1f,0,0,
                    DungeonObjectCondition.Always,record.Source)))
                .Concat(Resources.PushSynchronizers.GetRoomRecords(group,room.Id).Select(record => new DungeonObjectRecord(
                    group,room.Id,record.Order,DungeonObjectKind.PushBlockSynchronizer,InteractionId.PushBlockSynchronizer,0,0,0,
                    DungeonObjectCondition.Always,record.Source)))
                .Concat(Resources.EyeStatues.GetRoomRecords(group,room.Id).Select(record => new DungeonObjectRecord(
                    group,room.Id,record.Order,DungeonObjectKind.SeedShooterEyeStatue,InteractionId.Shopkeeper,record.Subid,
                    (record.PackedPosition >> 4) * 16 + 8,(record.PackedPosition & 15) * 16 + 8,
                    DungeonObjectCondition.Always,record.Source)))
                .OrderBy(record => record.Order).ToArray();
        if (laterDungeonRecords.Any(record => record.Kind == DungeonObjectKind.MermaidTorchOrder))
            lightableTorchState ??= new LightableTorchState();
        IReadOnlyList<MovingSideScrollPlatformPlacement> sidePlatformRecords =
            Resources.SidePlatforms.GetRoomRecords(group, room.Id);
        ColoredCubePuzzleState? spiritsGravePuzzle =
            group == 4 && room.Id == 0x20 ? CreateColoredCubePuzzleState() : null;
        ColoredCubePuzzleState? wingDungeonPuzzle =
            wingDungeonRecords.Count > 0 &&
            HasColoredCubeState(wingDungeonRecords)
                ? CreateColoredCubePuzzleState()
                : null;
        ColoredCubePuzzleState? skullDungeonPuzzle = HasColoredCubeState(laterDungeonRecords)
            ? CreateColoredCubePuzzleState() : null;
        bool enemyMechanicsSupported = DungeonEnemyMechanicsAreSupported(
            dungeonRecords, group, room);
        int mechanicIndex = 0;
        int sharedIndex = 0;
        int spiritsGraveIndex = 0;
        int wingDungeonIndex = 0;
        int moonlitGrottoIndex = 0;
        int laterDungeonIndex = 0;
        int sidePlatformIndex = 0;
        while (mechanicIndex < dungeonRecords.Count ||
               sharedIndex < sharedDungeonRecords.Count ||
               spiritsGraveIndex < spiritsGraveRecords.Count ||
               wingDungeonIndex < wingDungeonRecords.Count ||
               moonlitGrottoIndex < moonlitGrottoRecords.Count ||
               laterDungeonIndex < laterDungeonRecords.Count ||
               sidePlatformIndex < sidePlatformRecords.Count)
        {
            int mechanicOrder = mechanicIndex < dungeonRecords.Count
                ? dungeonRecords[mechanicIndex].Order : int.MaxValue;
            int sharedOrder = sharedIndex < sharedDungeonRecords.Count
                ? sharedDungeonRecords[sharedIndex].Order : int.MaxValue;
            int spiritsGraveOrder = spiritsGraveIndex < spiritsGraveRecords.Count
                ? spiritsGraveRecords[spiritsGraveIndex].Order : int.MaxValue;
            int wingDungeonOrder = wingDungeonIndex < wingDungeonRecords.Count
                ? wingDungeonRecords[wingDungeonIndex].Order : int.MaxValue;
            int moonlitGrottoOrder =
                moonlitGrottoIndex < moonlitGrottoRecords.Count
                    ? moonlitGrottoRecords[moonlitGrottoIndex].Order
                    : int.MaxValue;
            int sidePlatformOrder = sidePlatformIndex < sidePlatformRecords.Count
                ? sidePlatformRecords[sidePlatformIndex].Order : int.MaxValue;
            int laterDungeonOrder = laterDungeonIndex < laterDungeonRecords.Count
                ? laterDungeonRecords[laterDungeonIndex].Order : int.MaxValue;
            if (laterDungeonOrder < Math.Min(mechanicOrder, Math.Min(sharedOrder,
                Math.Min(spiritsGraveOrder, Math.Min(wingDungeonOrder, Math.Min(moonlitGrottoOrder, sidePlatformOrder))))))
            {
                var record = laterDungeonRecords[laterDungeonIndex++];
                if (!DungeonObjectConditionMet(record)) continue;
                if (record.Kind is DungeonObjectKind.Octogon or DungeonObjectKind.Vire)
                {
                    deferredMermaidEnemies.Add(record);
                    continue;
                }
                if (record.Kind == DungeonObjectKind.SeedShooterEyeStatue && !owner.PartSlotAvailable)
                    continue;
                if (record.Kind is DungeonObjectKind.OctogonInitializer or DungeonObjectKind.SmogController or DungeonObjectKind.PushBlockSynchronizer
                    or DungeonObjectKind.WallSquish or DungeonObjectKind.ButtonBridge or DungeonObjectKind.PuzzleTrapReset
                    or DungeonObjectKind.PatternHint or DungeonObjectKind.TilePatternChest or DungeonObjectKind.TriggerChestScript
                    or DungeonObjectKind.BossReward or DungeonObjectKind.MinibossReward or DungeonObjectKind.Essence or DungeonObjectKind.OrbChest
                    or DungeonObjectKind.TileFiller or DungeonObjectKind.FloorFillChest or DungeonObjectKind.BlueFlameChest
                    or DungeonObjectKind.ColoredCube or DungeonObjectKind.CubeLightSensor or DungeonObjectKind.CubeFloorSensor or DungeonObjectKind.CubeFlame
                    or DungeonObjectKind.FloorPatternTrigger or DungeonObjectKind.FloorPatternKey or DungeonObjectKind.ToggleFloor or DungeonObjectKind.FloorColorChanger or DungeonObjectKind.MermaidChangingFloor or DungeonObjectKind.MermaidBossKey
                    or DungeonObjectKind.FloorSwitchBit or DungeonObjectKind.CubeSwitchSensor or DungeonObjectKind.MinecartGate or DungeonObjectKind.RoomFlagMirror or DungeonObjectKind.MermaidTorchOrder or DungeonObjectKind.Spinner or DungeonObjectKind.BossKeyMirror or DungeonObjectKind.SignalScript)
                {
                    if (!owner.InteractionSlotAvailable) continue;
                }
                if (record.Kind == DungeonObjectKind.Lever)
                {
                    LeverProfile profile = Resources.Levers.Profile(record.SubId);
                    var lever = new LeverRoomEntity(profile.ToNpcRecord(record),
                        new LeverState(_runtimeState, (record.SubId & 0x40) == 0
                            ? WramAddress.wLever1PullDistance : WramAddress.wLever2PullDistance),
                        profile.Behavior, owner.OnSoundRequested,
                        parent => owner.TryCreateLeverConnection(parent,profile.ToNpcRecord(record,connection:true),profile));
                    yield return lever;
                    continue;
                }
                yield return record.Kind switch
                {
                    DungeonObjectKind.ArmosWarrior => CreateArmosWarrior(record, room, placementContext),
                    DungeonObjectKind.Smasher => CreateSmasher(record, room, placementContext),
                    DungeonObjectKind.OctogonInitializer => new OctogonEncounterInitializerRoomEntity(
                        Resources.MermaidDungeon.OctogonInitialization,_runtimeState),
                    DungeonObjectKind.SignalScript => CreateDungeonSignalScript(record),
                    DungeonObjectKind.RoomFlagMirror => CreateRoomFlagMirror(record),
                    DungeonObjectKind.Spinner => new DungeonSpinnerRoomEntity(record,_runtimeState,
                        Resources.DungeonVisuals.Visual("spinner"),owner.OnSoundRequested,owner.BeginScreenShake),
                    DungeonObjectKind.BossKeyMirror => new DungeonBossKeyMirrorRoomEntity(
                        () => owner.ActiveRoomHasFlag(_saveData ?? throw new InvalidOperationException($"{record.Source}: boss-key mirror requires save state."),0x20),
                        () => (_inventory ?? throw new InvalidOperationException($"{record.Source}: boss-key mirror requires inventory."))
                            .GrantDungeonBossKey(Resources.BossKeyMirrors.Dungeon(record))) { Visible=false,Name="DungeonBossKeyMirror" },
                    DungeonObjectKind.SmogSentinel => CreateSmogSentinel(record, room, placementContext),
                    DungeonObjectKind.SmogController => CreateSmogEncounter(new(record.X,record.Y),room),
                    DungeonObjectKind.PushBlockSynchronizer => new PushBlockSynchronizerRoomEntity(
                        room,Resources.PushSynchronizers,() => owner.RequiredReservedPushBlock,owner.TryCreateSynchronizedBlock),
                    DungeonObjectKind.WallSquish => new WallSquishRoomEntity(room,
                        () => rooms!.BlockPushAngle, () => _runtimeState.ReadWramByte(WramAddress.wLinkRaisedFloorOffset), record.Source),
                    DungeonObjectKind.ButtonBridge => new ButtonBridgeRoomEntity(Resources.CrownDungeon.ButtonBridge,room,
                        () => owner.ActiveTriggers,(position,tile) =>
                        {
                            if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                                throw new InvalidOperationException($"INTERAC $90:$19 lost its room at {record.Source}.");
                            room.SetUnderlyingStorageMetatile(position,tile);
                            if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        },owner.OnSoundRequested,owner.TryCreateRockDebris),
                    DungeonObjectKind.PuzzleTrapReset => new PuzzleTrapResetRoomEntity(
                        Resources.TrapResets.GetRoomRecords(group,room.Id).Single(reset => reset.Order == record.Order),
                        room,_runtimeState,owner.OnSoundRequested,owner.OnRoomWarpRequested),
                    DungeonObjectKind.PatternHint => new DungeonPatternHintRoomEntity(Resources.CrownDungeon.PatternHint,
                        () => owner.ActiveTriggers,(position,tile) =>
                        {
                            if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                                throw new InvalidOperationException($"INTERAC $21:$16 lost its active room at {record.Source}.");
                            if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        },owner.TryCreatePuzzlePuff),
                    DungeonObjectKind.TilePatternChest => new DungeonPuzzleChestRoomEntity(record,room,
                        () => Resources.ChestPatterns.Matches(record.Id,record.SubId,room),DungeonChestItemFlagSet,Resources.DungeonMechanics.ChestTile,
                        owner.OnSoundRequested,owner.OnRoomTileChanged,_animationTick,owner.TryCreatePuzzlePuff,owner.IsOutgoingEntity,() => WriteDungeonChest(record)),
                    DungeonObjectKind.TriggerChestScript => new DungeonTriggerChestScriptRoomEntity(
                        record.Position,_runtimeState,Resources.CrownDungeon.TriggerChestValue,Resources.CrownDungeon.TriggerChestWait,
                        () => owner.ActiveTriggers,() => _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem) == true,
                        owner.IsScriptTextActive,owner.OnSoundRequested,() => WriteDungeonChest(record)),
                    DungeonObjectKind.SeedShooterEyeStatue => new SeedShooterEyeStatueRoomEntity(
                        Resources.EyeStatues.GetRoomRecords(group,room.Id).Single(eye => eye.Order == record.Order),
                        Resources.EyeStatues,Resources.DungeonVisuals.Visual("seed-shooter-eye-statue"),owner.SetTrigger),
                    DungeonObjectKind.Eyesoar => CreateEyesoar(record, room, placementContext),
                    DungeonObjectKind.Essence => CreateLaterDungeonEssence(record, room),
                    DungeonObjectKind.BossReward => CreateDungeonReward(record, "TREASURE_OBJECT_HEART_CONTAINER_00", falling: false),
                    DungeonObjectKind.MinibossReward => new DungeonRewardRoomEntity(record, Resources.DungeonInteractions,
                        _saveData, () => owner.RoomEnemyCount, null, owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal),
                    DungeonObjectKind.SwitchTileToggler => CreateSwitchTileToggler(record,room),
                    DungeonObjectKind.MovingPlatform => CreateMovingPlatform(record.Position, record.SubId,
                        rooms?.World.GetDungeonIndex(record.Group, record.Room) ?? -1),
                    DungeonObjectKind.MovingOrb => new MovingOrbRoomEntity(record, Resources.DungeonVisuals.Visual("grotto-orb"), _runtimeState, owner.OnSoundRequested, Resources.DungeonMechanics.SwitchSound),
                    DungeonObjectKind.OrbChest => new DungeonOrbChestRoomEntity(record,_runtimeState,owner.OnSoundRequested,
                        owner.IsScriptTextActive,() => _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem) == true,
                        () => WriteDungeonChest(record)),
                    DungeonObjectKind.ToggleFloor => CreateToggleFloor(room),
                    DungeonObjectKind.FloorColorChanger => CreateFloorColorChanger(record),
                    DungeonObjectKind.LeverLavaFiller => new LeverLavaFillerRoomEntity(
                        room, Resources.LeverLava.Script(record.SubId), new LeverState(_runtimeState, WramAddress.wLever1PullDistance),
                        _random, owner.OnSoundRequested, (position, tile) =>
                        {
                            if (rooms is null || !ReferenceEquals(rooms.CurrentRoom, room))
                                throw new InvalidOperationException($"INTERAC $d8 lost its active room at {record.Source}.");
                            if (rooms.TrySetTile(position, tile)) owner.OnRoomTileChanged();
                        }),
                    DungeonObjectKind.FloorPatternTrigger => new DungeonPatternTriggerRoomEntity(
                        room, Resources.DungeonInteractions.Constant("red-toggle-floor"), Resources.SkullDungeon.Pattern(record.SubId), owner.SetTrigger, owner.IsOutgoingEntity),
                    DungeonObjectKind.FloorPatternKey => new DungeonPatternKeyRoomEntity(
                        record, () => owner.ActiveRoom, Resources.DungeonInteractions.Constant("red-toggle-floor"), Resources.SkullDungeon.Pattern(record.SubId),
                        CreateFallingSmallKeyRequest(record), DungeonChestItemFlagSet, owner.TryCreateGroundTreasure, owner.IsOutgoingEntity),
                    DungeonObjectKind.ColoredCube or DungeonObjectKind.CubeFlame or DungeonObjectKind.CubeLightSensor =>
                        CreateColoredCubeInteraction(record, room, skullDungeonPuzzle),
                    DungeonObjectKind.CubeFloorSensor => new ColoredCubeFloorSensorRoomEntity(record,
                        () => owner.ActiveRoom,RequireColoredCubePuzzle(skullDungeonPuzzle,record),Resources.DungeonInteractions,
                        owner.IsOutgoingEntity,(position,tile) => {
                            if (rooms is null) throw new InvalidOperationException($"{record.Source}: cube floor sensor has no active room session.");
                            if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        },owner.OnSoundRequested),
                    DungeonObjectKind.MermaidChangingFloor => new MermaidChangingFloorRoomEntity(
                        Resources.MermaidChangingFloor,_runtimeState,owner.TryCreateMermaidChangingFloorWorker),
                    DungeonObjectKind.MermaidTorchOrder => CreateMermaidTorchOrder(record,room,
                        lightableTorchState ?? throw new InvalidOperationException($"{record.Source}: missing shared torch state.")),
                    DungeonObjectKind.MermaidBossKey => new MermaidBossKeyRoomEntity(record,_runtimeState,_random,
                        DungeonChestItemFlagSet,() => owner.RoomEnemyCount,owner.SetTrigger,
                        () => owner.ParseRandomEnemies(Resources.MermaidDungeon.BossKeyFailureEnemies,
                            EnemyPlacementContext.FromWarpDestination(_runtimeState.ReadWramByte(WramAddress.wWarpDestPos))),
                        owner.OnSoundRequested,() => {
                            owner.OnSoundRequested(SoundId.SndSolvePuzzle);
                            WriteDungeonChest(record);
                            owner.TryCreatePuzzlePuff(record.Position);
                        }),
                    DungeonObjectKind.FloorSwitchBit or DungeonObjectKind.CubeSwitchSensor => new DungeonStateController(
                        record, () => owner.ActiveRoom, Resources.DungeonInteractions, skullDungeonPuzzle ?? CreateColoredCubePuzzleState(), _runtimeState, owner.SetTrigger),
                    DungeonObjectKind.MinecartGate => new MinecartGateRoomEntity(
                        record, () => owner.ActiveRoom, _runtimeState, Resources.DungeonVisuals.Visual("minecart-gate"), owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick),
                    DungeonObjectKind.TileFiller => new TileFillerRoomEntity(
                        record, room, Resources.DungeonInteractions, owner.OnSoundRequested, (position,tile) =>
                        {
                            if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                                throw new InvalidOperationException($"INTERAC $25 lost its active room at {record.Source}.");
                            bool written = rooms.TrySetTile(position,tile);
                            if (written) owner.OnRoomTileChanged();
                            return written;
                        },owner.TryCreatePuzzlePuff),
                    // findTileInRoom scans $bf..$00, including column padding.
                    // loadRoomLayout clears the extra $b0..$bf guard row to
                    // zero; it cannot match BLUE_FLOOR and is implicit here.
                    DungeonObjectKind.FloorFillChest => new DungeonPuzzleChestRoomEntity(
                        record, room, () => !room.Layout.AsSpan().Contains((byte)Resources.DungeonInteractions.Constant("blue-floor")), DungeonChestItemFlagSet,
                        Resources.DungeonInteractions.Constant("chest"), owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick,owner.TryCreatePuzzlePuff,owner.IsOutgoingEntity,() => WriteDungeonChest(record)),
                    DungeonObjectKind.BlueFlameChest => new DungeonPuzzleChestRoomEntity(
                        record, room, () => (RequireColoredCubePuzzle(skullDungeonPuzzle, record).CubeColor & 0x83) == 0x82, DungeonChestItemFlagSet,
                        Resources.DungeonInteractions.Constant("chest"), owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick,owner.TryCreatePuzzlePuff,owner.IsOutgoingEntity,() => WriteDungeonChest(record)),
                    _ => throw new InvalidOperationException($"Unsupported Skull native object at {record.Source}.")
                };
                continue;
            }
            bool useShared = sharedOrder < mechanicOrder &&
                sharedOrder < spiritsGraveOrder &&
                sharedOrder < wingDungeonOrder &&
                sharedOrder < moonlitGrottoOrder &&
                sharedOrder < sidePlatformOrder;
            if (useShared)
            {
                PlacementRecord record =
                    sharedDungeonRecords[sharedIndex++];
                if (!owner.InteractionSlotAvailable) continue;
                // Room 4:e7 places a construction soldier before its dungeon
                // entry handler. CreateBlackTowerNpcs inserts this one record
                // after that first actor; every other shared record is emitted
                // here at its imported source order.
                if (group == 4 && room.Id == 0xe7 &&
                    record.Kind == DungeonEntranceInteractionDatabaseObjectKind.Entry)
                {
                    continue;
                }
                yield return CreateSharedDungeonInteraction(
                    record, room, placementContext);
                continue;
            }

            if (spiritsGraveOrder < mechanicOrder &&
                spiritsGraveOrder < wingDungeonOrder &&
                spiritsGraveOrder < moonlitGrottoOrder &&
                spiritsGraveOrder < sidePlatformOrder)
            {
                DungeonObjectRecord record =
                    spiritsGraveRecords[spiritsGraveIndex++];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateSpiritsGraveInteraction(
                    record, room, spiritsGravePuzzle, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            if (wingDungeonOrder < mechanicOrder &&
                wingDungeonOrder < moonlitGrottoOrder &&
                wingDungeonOrder < sidePlatformOrder)
            {
                DungeonObjectRecord record =
                    wingDungeonRecords[wingDungeonIndex++];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateWingDungeonInteraction(
                    record, room, wingDungeonPuzzle, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            if (moonlitGrottoOrder < mechanicOrder &&
                moonlitGrottoOrder < sidePlatformOrder)
            {
                DungeonObjectRecord record =
                    moonlitGrottoRecords[moonlitGrottoIndex++];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateMoonlitGrottoInteraction(
                    record, room, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            if (sidePlatformOrder < mechanicOrder)
            {
                MovingSideScrollPlatformPlacement placement =
                    sidePlatformRecords[sidePlatformIndex++];
                if (!owner.InteractionSlotAvailable) continue;
                yield return new MovingSideScrollPlatformRoomEntity(
                    placement,
                    Resources.DungeonInteractions.SidePlatform(placement.SubId),
                    Resources.DungeonVisuals.Visual("moving-side-platform"));
                continue;
            }

            DungeonMechanicDatabaseRecord mechanic = dungeonRecords[mechanicIndex++];
            IRoomEntity? mechanicEntity = CreateDungeonMechanic(
                mechanic, room, group, enemyMechanicsSupported, placementContext,
                lightableTorchState);
            if (mechanicEntity is not null)
                yield return mechanicEntity;
        }

        foreach (var record in Resources.CollapsingFloors.InRoom(group, room.Id))
            yield return new CollapsingFloorRoomEntity(record, room, owner.OnSoundRequested,
                owner.OnRoomTileChanged, _animationTick, () => owner.InteractionSlotAvailable);

        foreach (FountainPlacement fountain in Resources.Fountains.InRoom(group, room.Id))
            yield return new FountainDecorationRoomEntity(fountain, Resources.Fountains, _saveData);

        if (!spawnMaple)
        {
            foreach (VolcanoPlacement record in Resources.Volcano.Placements(group, room.Id))
                yield return CreateVolcanoController(record, room);
        }

        if (_saveData is not null)
        {
            // Room 3:9e places its watcher after Impa, Nayru, and Zelda.
            // CreateNayruHouseNpcs emits it at that exact source position.
            if (group != Resources.NayruHouse.Record.Group ||
                room.Id != Resources.NayruHouse.Record.Room)
            {
                foreach (RoomTileChangeWatcherDatabaseRecord record in
                    Resources.TileChangeWatchers.GetRoomRecords(group, room.Id))
                {
                    yield return new RoomTileChangeWatcherRoomEntity(
                        record, room, _saveData);
                }
            }
        }

        // ENEMY_SEEDS_ON_TREE is a main object. In room 0:78 it precedes the
        // old-lady interaction, and every controller creates its parts before
        // the remaining placed interactions receive their first update.
        foreach (SeedTreePlacementRecord record in
            Resources.SeedTrees.GetRoomRecords(group, room.Id))
        {
            if (record.Order == 0)
            {
                foreach (IRoomEntity entity in
                    CreateSeedTreeEntities(record, room))
                {
                    yield return entity;
                }
            }
        }

        foreach (TokayEntranceEyeRecord eye in
            Resources.TokayEntranceEyes.GetRoomEyes(group, room.Id))
        {
            if (eye.RequiredRoomFlag == 0 ||
                _saveData is not null && _saveData.HasRoomFlag(
                    eye.Group,
                    eye.Room,
                    checked((byte)eye.RequiredRoomFlag)))
            {
                yield return new TokayEntranceEyeRoomEntity(eye);
            }
        }

        // Rooms 2:ea/2:eb place only the $ac family spawner at this point in
        // their source object streams. Execute its state writes and expansion
        // here, before any of the spawned actors receive their first update.
        IReadOnlyList<NpcRecord> roomNpcs =
            _familyState.ResolveRoomNpcs(
                group, room.Id, _saveData, _runtimeState);
        if (group == 0 && roomNpcs.Any(record => record.Id == InteractionId.Carpenter))
        {
            if (room.Id == 0x25 && _saveData?.HasGlobalFlag(Resources.Carpenters.Constant("bridge-flag")) == true)
            {
                for (int x = 0; x < 3; x++)
                for (int y = 5; y <= 6; y++)
                    room.SetPositionTileAndCollision(new Vector2(x * 16 + 8, y * 16 + 8),
                        (byte)Resources.Carpenters.Constant(y == 5 ? "bridge-top" : "bridge-bottom"), null, _animationTick());
            }
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                yield return new CarpenterRoomEntity(NpcCharacter.CreateFromRecord(record), Resources.Carpenters,
                    _runtimeState, _saveData, room, _animationTick());
            }
        }
        else if (roomNpcs.Any(record => record.Id == InteractionId.Patch))
        {
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                if (record.Id != InteractionId.Patch || record.SubId > 2)
                    throw new InvalidOperationException($"patch.s: unexpected room NPC ${record.Id:x2}:${record.SubId:x2}.");
                yield return new PatchRoomEntity(NpcCharacter.CreateFromRecord(record));
            }
        }
        else if (roomNpcs.Any(record => record.Id == InteractionId.SymmetryNpc))
        {
            if (group == Resources.TuniNut.Constant("group") && room.Id == Resources.TuniNut.Constant("room"))
                yield return new TuniNutRoomEntity(Resources.TuniNut, Resources.Symmetry, _inventory, _saveData, room);
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                if (record.Id != InteractionId.SymmetryNpc)
                    throw new InvalidOperationException($"symmetryNpc.s: unexpected NPC ${record.Id:x2}:${record.SubId:x2} in room ${group:x}:${room.Id:x2}.");
                yield return new SymmetryRoomEntity(NpcCharacter.CreateFromRecord(record), Resources.Symmetry, _saveData);
            }
        }
        else if (group == 4 && room.Id is 0xe0 or 0xe1 or 0xe2 or 0xe7 or 0xe8)
        {
            foreach (IRoomEntity entity in CreateBlackTowerNpcs(
                room, roomNpcs, placementContext))
            {
                yield return entity;
            }
        }
        else if (Resources.MakuSproutRoom.MatchesRoom(group, room.Id))
        {
            foreach (IRoomEntity entity in
                CreateMakuSproutRoomEntities(room, roomNpcs))
            {
                yield return entity;
            }
        }
        else if (group == 1 && room.Id == 0x48)
        {
            foreach (IRoomEntity entity in CreateRoom148Npcs(roomNpcs))
                yield return entity;
        }
        else if (group == 1 && room.Id == 0x49)
        {
            foreach (IRoomEntity entity in CreateRoom149Family(roomNpcs))
                yield return entity;
        }
        else if (group == Resources.NayruHouse.Record.Group &&
            room.Id == Resources.NayruHouse.Record.Room)
        {
            foreach (IRoomEntity entity in
                CreateNayruHouseNpcs(room, roomNpcs))
            {
                yield return entity;
            }
        }
        else if (group == Resources.LynnaShop.Group &&
            (room.Id == Resources.LynnaShop.Room || room.Id == Resources.HiddenShop.Room))
        {
            foreach (IRoomEntity entity in CreateLynnaShop(room, roomNpcs))
                yield return entity;
        }
        else if (group == 3 && room.Id == 0xed)
        {
            var database = new LynnaShopDatabase(syrup: true);
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                NpcCharacter actor = NpcCharacter.CreateFromRecord(record);
                actor.SetDialogue(0, string.Empty, canFace: false);
                actor.SetScriptButtonSensitive(true);
                actor.SetBlocksLink(record.Id == 0x5f);
                actor.SetCollisionRadii(record.Id == 0x5f ? database.Constant("syrup-radius-y") : 6,
                    record.Id == 0x5f ? database.Constant("syrup-radius-x") : 6);
                yield return new SyrupShopActorRoomEntity(actor);
            }
            // syrupScript_spawnShopItems allocates after the two placed actors.
            foreach (StockRecord stock in database.ResolveStock(_saveData))
            {
                var item = new LynnaShopItem { Name = $"SyrupStock_{stock.Order}_{stock.Item.SubId:x2}",
                    ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
                item.Initialize(stock, room);
                yield return new LynnaShopItemRoomEntity(item);
            }
        }
        else if (group == Resources.TokayShop.Group && room.Id == Resources.TokayShop.Room)
        {
            foreach (IRoomEntity entity in CreateTokayShop(roomNpcs))
                yield return entity;
        }
        else if (group == Resources.VasuShop.Group && room.Id == Resources.VasuShop.Room)
        {
            foreach (IRoomEntity entity in CreateVasuShopNpcs(roomNpcs))
                yield return entity;
        }
        else
        {
            foreach (NpcRecord record in roomNpcs)
            {
                switch (record.Implementation)
                {
                    case NpcImplementationClassification.OrdinaryGeneric:
                        yield return new NpcRoomEntity(
                            NpcCharacter.CreateFromRecord(record));
                        break;
                    case NpcImplementationClassification.SpecializedNative:
                        IRoomEntity specialized = CreateSpecializedNpc(record, room);
                        yield return specialized;
                        if (specialized.Node is TokayCharacter { Accessory: { } accessory })
                            yield return accessory;
                        if (specialized is RosaNpcRoomEntity { Shovel: { } shovel })
                            yield return shovel;
                        break;
                    case NpcImplementationClassification.EventOwned:
                        yield return record is { Id: InteractionId.Plen, SubId: 0 }
                            ? new PlenRoomEntity(NpcCharacter.CreateFromRecord(record))
                            : record is { Id: InteractionId.KingMoblinDefeated, SubId: 0 }
                            ? new DefeatedMoblinActorRoomEntity(NpcCharacter.CreateFromRecord(record))
                            : record is { Id: InteractionId.BombUpgradeFairy, SubId: 0 }
                            ? new BombUpgradeFairyRoomEntity(NpcCharacter.CreateFromRecord(record))
                            : record.Id is InteractionId.Goron or InteractionId.GoronElder || record is {Id:InteractionId.ShootingGallery,SubId:1}
                                ? new GoronCaveRoomEntity(NpcCharacter.CreateFromRecord(record))
                                : new EventOwnedNpcRoomEntity(NpcCharacter.CreateFromRecord(record));
                        break;
                    case NpcImplementationClassification.DeliberatelyUnsupported:
                        break;
                    default:
                        throw UnsupportedNpcClassification(record);
                }
            }
        }

        // group1MapacObjectData places the room-flag-gated $80:$04
        // decoration immediately after the $48:$11 Tokay. Its initial state
        // deletes it while bit $80 is clear; the planting script recreates it
        // dynamically after setting that flag.
        if (_saveData is not null &&
            Resources.TokaySeedlingPlot.MatchesRoom(group, room.Id) &&
            _saveData.HasRoomFlag(
                group,
                room.Id,
                checked((byte)Resources.TokaySeedlingPlot.Record.RoomFlag)))
        {
            yield return new TokaySeedlingDecorationRoomEntity(
                Resources.TokaySeedlingPlot.Record);
        }

        // Every Ages Gasha placement precedes the room's enemy pointer. In
        // 0:7b it follows all three child interactions, so emit it after the
        // placed NPC/interaction set and before parts/enemies.
        if (_saveData is not null &&
            Resources.GashaSpots.TryGetSpot(group, room.Id, out SpotRecord spot) &&
            (!_saveData.IsGashaSpotPlanted(spot.SubId) ||
             _saveData.GetGashaSpotKillCounter(spot.SubId) >= Resources.GashaSpots.NutKills))
        {
            var gasha = new GashaSpotInteraction
            {
                Name = $"GashaSpot_{spot.SubId:x2}",
                ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
            };
            gasha.Initialize(
                Resources.GashaSpots, spot, room, _saveData, _inventory,
                owner.OnGashaInteractionRequested, owner.OnGashaNutCaught,
                owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick);
            yield return new GashaSpotRoomEntity(gasha);
        }

        foreach (GroundTreasureDatabaseRecord record in
            Resources.GroundTreasures.GetRoomRecords(group, room.Id))
        {
            if (!owner.InteractionSlotAvailable) continue;
            if (!GroundTreasureDatabase.ShouldSpawn(
                    record, _saveData, _inventory))
                continue;
            var treasure = new GroundTreasurePickup
            {
                Name = $"GroundTreasure_{record.Order}",
                ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
            };
            treasure.Initialize(record, owner.OnSoundRequested, owner.ToScreen, room, owner.TryCreatePuzzlePuff);
            yield return new GroundTreasureRoomEntity(
                treasure, owner.CanCollectGroundTreasure,
                owner.OnGroundTreasureCollected);
        }

        // $dc:$03/$04 precede both $e1 portals in the source object stream.
        // Keep their state-0 updates separate from creation and from reveal.
        if (_saveData is not null)
        {
            foreach (PortalRevealRecord record in timePortals.GetRoomReveals(group, room.Id))
                yield return new PortalRevealRoomEntity(
                    record, room, _saveData, owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick);
        }
        foreach (IRoomEntity portal in CreateTimePortals(group, room))
            yield return portal;

        // $9e follows $e1 in the water-control room's object stream. Both
        // variants perform their complementary flag check in native state 0.
        if (_saveData is not null)
            foreach (var record in Resources.WaterPushblocks.GetRoom(group, room.Id))
                yield return new WaterPushblockRoomEntity(record, Resources.WaterPushblocks, room, _saveData,
                    owner.OnSoundRequested, () => owner.OnRoomMusicRequested(group, room.Id), owner.OnRoomTileChanged, _animationTick);

        // The two non-leading Ages placements follow their room's supported
        // actors/portals and still precede the enemy pointer: 0:13 order 6
        // follows four portals plus two native interactions, while 1:25 order
        // 1 follows its construction soldier.
        foreach (SeedTreePlacementRecord record in
            Resources.SeedTrees.GetRoomRecords(group, room.Id))
        {
            if (record.Order != 0)
            {
                foreach (IRoomEntity entity in
                    CreateSeedTreeEntities(record, room))
                {
                    yield return entity;
                }
            }
        }

        if (spawnMaple)
        {
            var encounterState = new MapleEncounterState();
            var maple = new MapleEncounter
            {
                Name = "Maple",
                ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
            };
            maple.Initialize(
                activeGroup,
                Resources.Maple,
                encounterState,
                room,
                _random,
                _saveData!,
                _inventory!,
                _treasures,
                owner.OnMapleDialogueRequested,
                owner.IsTextActive,
                owner.OnSoundRequested,
                owner.BeginHorizontalScreenShake,
                owner.OnRoomMusicRequested);
            yield return new MapleEncounterRoomEntity(maple);
            // checkAndSpawnMaple allocates its interaction before parseObjectData.
            // The placed $dc:$05 must therefore consume its RNG after Maple.
            foreach (VolcanoPlacement record in Resources.Volcano.Placements(group, room.Id))
                yield return CreateVolcanoController(record, room);

            // checkAndSpawnMaple writes wcc85=$01. checkSkipPointer then
            // suppresses this room's entire enemy/item-drop pointer while
            // preserving every ordinary interaction emitted above.
            yield break;
        }

        int partSlots = 0;
        var enemyStream = enemies.GetRoomObjects(group,room.Id);
        foreach (var record in deferredMermaidEnemies)
            if (Resources.MermaidDungeon.EnemyStreamOrder(record) > enemyStream.Count)
                throw new InvalidOperationException($"{record.Source}: native enemy insertion exceeds the imported stream for room${group:x1}:${room.Id:x2}.");
        foreach (RoomObjectRecord source in enemyStream)
        {
            foreach (var entity in InsertMermaidEnemies(source.Order)) yield return entity;
            if (!RoomObjectConditionMet(source, group, room))
                continue;

            EnemyObjectHandlerResolution resolution =
                enemies.EnemyHandlers.Resolve(source);
            switch (resolution.SlotPolicy)
            {
                case EnemyObjectSlotPolicy.RandomEnemy:
                    foreach (var entity in ParseRandomEnemies(source,room,placementContext,reservations))
                        yield return entity;
                    break;

                case EnemyObjectSlotPolicy.FixedEnemy:
                    EnemyHandlerDescriptor fixedHandler =
                        resolution.RequireEnemyHandler(source);
                    int fixedKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags);
                    if (WasKilledDuringPlacement(fixedKillableEnemyIndex))
                        break;
                    int fixedSlot = owner.FindFreeEnemySlot();
                    if (fixedSlot < 0)
                        break;
                    Vector2 fixedPosition = fixedHandler.Handler ==
                        EnemyHandlerKind.VineSprout
                            ? EnemyFactory.ResolveVineSproutPosition(source, room)
                            : new Vector2(source.X, source.Y);
                    reservations.Add(room.GetPackedPosition(fixedPosition));
                    IRoomEntity? fixedEntity = EnemyFactory.CreateOrderedEnemy(
                        fixedHandler, source, room, fixedPosition, 0,
                        fixedKillableEnemyIndex, placementContext);
                    owner.RegisterEnemySlot(fixedEntity, fixedSlot);
                    if (fixedEntity is not null)
                        yield return fixedEntity;
                    break;

                case EnemyObjectSlotPolicy.ParameterEnemy:
                    EnemyHandlerDescriptor parameterHandler = resolution.RequireEnemyHandler(source);
                    int parameterSlot = owner.FindFreeEnemySlot();
                    if (parameterSlot < 0)
                        break;
                    // objectDataOp9 allocates a counted enemy but never checks
                    // recent defeats, advances the killable index, or reserves a tile.
                    IRoomEntity? parameterEntity = EnemyFactory.CreateOrderedEnemy(
                        parameterHandler, source, room, new Vector2(source.X, source.Y),
                        0, 0, placementContext);
                    owner.RegisterEnemySlot(parameterEntity, parameterSlot);
                    if (parameterEntity is not null)
                        yield return parameterEntity;
                    break;

                case EnemyObjectSlotPolicy.ItemDrop:
                    int itemKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags);
                    if (WasKilledDuringPlacement(itemKillableEnemyIndex))
                        break;
                    int itemSlot = owner.FindFreeEnemySlot();
                    if (itemSlot < 0)
                        break;
                    reservations.Add(source.PackedPosition);
                    if (ItemDropDatabase.IsRuntimeSupported(source.SubId))
                    {
                        var producer = new ItemDropProducer
                        {
                            Name = $"ItemDropProducer_{source.Order}_{source.SubId:x2}"
                        };
                        producer.Initialize(
                            source.SubId,
                            PointForPackedPosition(source.PackedPosition),
                            room,
                            _inventory,
                            _saveData,
                            _random);
                        var producerEntity = new ItemDropProducerRoomEntity(
                            producer, itemKillableEnemyIndex);
                        owner.RegisterEnemySlot(producerEntity, itemSlot);
                        yield return producerEntity;
                    }
                    else
                        owner.RegisterEnemySlot(null, itemSlot);
                    break;

                case EnemyObjectSlotPolicy.ReservingPart:
                    if (partSlots >= 16 || !owner.PartSlotAvailable)
                        break;
                    partSlots++;
                    reservations.Add(source.PackedPosition);
                    if (source.Id == PartId.OwlStatue)
                    {
                        yield return new OwlStatueRoomEntity(
                            source,
                            Resources.OwlStatues.Record(source.SubId),
                            room,
                            owner.OnOwlStatueMessageRequested,
                            owner.TryCreateOwlSparkle,
                            _animationTick);
                    }
                    else if (source.Id == PartId.WallArrowShooter)
                    {
                        yield return new WallArrowShooterRoomEntity(source, owner.TryCreateWallArrow);
                    }
                    else if (source.Id == 0x45)
                    {
                        yield return new FallingBoulderRoomEntity(new FallingBoulder(
                            source.SubId, PointForPackedPosition(source.PackedPosition),
                            Resources.FallingBoulders, _random, owner.OnSoundRequested, room, owner.ToScreen));
                    }
                    break;

                case EnemyObjectSlotPolicy.ParameterPart:
                    if (partSlots < 16)
                        partSlots++;
                    break;
            }
        }
        foreach (var entity in InsertMermaidEnemies(enemyStream.Count)) yield return entity;
    }

    private IRoomEntity? CreateSpiritsGraveInteraction(
        DungeonObjectRecord record,
        OracleRoomData room,
        ColoredCubePuzzleState? puzzle,
        EnemyPlacementContext placementContext)
    {
        switch (record.Kind)
        {
            case DungeonObjectKind.BraceletReward:
                return CreateDungeonReward(
                    record, "TREASURE_OBJECT_BRACELET_00", falling: false);
            case DungeonObjectKind.EnemySmallKey:
                return CreateDungeonReward(
                    record, "TREASURE_OBJECT_SMALL_KEY_01", falling: true);
            case DungeonObjectKind.BossReward:
                return CreateDungeonReward(
                    record, "TREASURE_OBJECT_HEART_CONTAINER_00", falling: false);
            case DungeonObjectKind.MinibossReward:
                return new DungeonRewardRoomEntity(
                    record, Resources.DungeonInteractions, _saveData,
                    () => owner.RoomEnemyCount, treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal);
            case DungeonObjectKind.MovingPlatform:
                return CreateMovingPlatform(record.Position, record.SubId,
                    rooms?.World.GetDungeonIndex(record.Group, record.Room) ?? -1);
            case DungeonObjectKind.SpawnMovingPlatform:
                return new SpiritsGraveMovingPlatformSpawner(
                    owner.TriggerIsActive,
                    owner.OnSoundRequested,
                    Resources.SpiritsGrave.Constant("moving-platform-spawn-wait"),
                    _runtimeState, owner.IsScriptTextActive,
                    owner.TryCreatePuzzlePuff, owner.TryCreateMovingPlatform);
            case DungeonObjectKind.TorchStairs:
                return new SpiritsGraveTorchStairs(
                    record, room, _saveData, owner.OnSoundRequested,
                    owner.OnRoomTileChanged, _animationTick,
                    Resources.SpiritsGrave.Constant("torch-count"),
                    Resources.SpiritsGrave.Constant("torch-tile"),
                    Resources.SpiritsGrave.Constant("solve-sound"),
                    Resources.SpiritsGrave.Constant("light-torch-sound"));
            case DungeonObjectKind.ColoredCube:
            case DungeonObjectKind.CubeFlame:
            case DungeonObjectKind.CubeLightSensor:
            case DungeonObjectKind.CubeTriggerSensor:
                return CreateColoredCubeInteraction(record, room, puzzle);
            case DungeonObjectKind.GiantGhini:
                var giantGhini = new GiantGhiniBoss
                {
                    Name = "GiantGhini",
                    ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
                };
                giantGhini.Initialize(
                    Resources.DungeonBosses.Enemy(EnemyId.GiantGhini), room, record.Position, _random,
                    owner.OnSoundRequested, owner.BossShuttersClosed,
                    owner.DisableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room));
                return new GiantGhiniBossRoomEntity(
                    giantGhini, placementContext.BossEntryDirection);
            case DungeonObjectKind.PumpkinHead:
                ImportedEnemyDefinition pumpkin = Resources.DungeonBosses.Enemy(EnemyId.PumpkinHead);
                return new PumpkinHeadBossRoomEntity(
                    new PumpkinHeadBoss(
                        pumpkin, room, record.Position, _random, owner.OnSoundRequested,
                        owner.BossShuttersClosed, owner.BeginScreenShake,
                        owner.DisableLinkCollisionsAndMenu,
                        () => owner.OnRoomMusicRequested(record.Group, record.Room),
                        Resources.DungeonBosses.Constant("pumpkin-body-palette"),
                        Resources.DungeonBosses.Constant("pumpkin-ghost-palette")),
                    pumpkin.DamageQuarters,
                    placementContext.BossEntryDirection);
            case DungeonObjectKind.Essence:
                return new DungeonEssence(
                    record,
                    Resources.DungeonVisuals.Visual("eternal-spirit"),
                    Resources.DungeonVisuals.Visual("essence-pedestal"),
                    Resources.DungeonVisuals.Visual("essence-glow"),
                    Resources.DungeonVisuals.Visual("energy-bead"),
                    room,
                    _saveData?.HasRoomFlag(
                        record.Group, record.Room, OracleSaveData.RoomFlagItem) == true,
                    _animationTick,
                    owner.OnDungeonEssenceTriggered,
                    new DungeonEssenceDefinition(
                        0,
                        Resources.SpiritsGrave.EssenceMessage,
                        new Warp(
                            4, 0x11, -1, 0, 0, 0,
                            0x8d, 0x26, 0, WarpDestinationTransition.SetRespawn)));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record), record, "Unsupported Spirit's Grave object.");
        }
    }

    private IRoomEntity CreateColoredCubeInteraction(
        DungeonObjectRecord record,
        OracleRoomData room,
        ColoredCubePuzzleState? puzzle)
    {
        // INTERAC_COLORED_CUBE and its sensors share their source dispatch across dungeons.
        switch (record.Kind)
        {
            case DungeonObjectKind.ColoredCube:
                return new ColoredCubeRoomEntity(
                    record, Resources.DungeonVisuals.Visual("colored-cube"), room,
                    Resources.DungeonInteractions,
                    RequireColoredCubePuzzle(puzzle, record),
                    Resources.DungeonVisuals.CubePalettes,
                    owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick);
            case DungeonObjectKind.CubeFlame:
                return new ColoredCubeFlameRoomEntity(
                    record, Resources.DungeonVisuals.Visual("cube-flame"),
                    RequireColoredCubePuzzle(puzzle, record));
            case DungeonObjectKind.CubeLightSensor:
            case DungeonObjectKind.CubeTriggerSensor:
                return new ColoredCubeSensorRoomEntity(
                    record, room, RequireColoredCubePuzzle(puzzle, record),
                    owner.SetTrigger, owner.OnSoundRequested);
            default:
                throw new ArgumentOutOfRangeException(nameof(record), record,
                    "Unsupported colored-cube interaction.");
        }
    }

    private MinecartRoomEntity CreateMinecart(
        ActiveMinecart cart,
        OracleRoomData room) =>
        new(
            cart,
            room,
            Resources.DungeonInteractions,
            _runtimeState,
            Resources.DungeonVisuals.Visual("minecart"),
            owner.OnSoundRequested);

    private IEnumerable<IRoomEntity> CreateMinecartShutterControllers(
        OracleRoomData room, EnemyPlacementContext placementContext)
    {
        // replaceShutterForLinkEntering creates these subids only when
        // TILESETFLAG_DUNGEON is set in the Ages engine.
        if ((room.TilesetFlags & (int)TilesetFlags.Dungeon) == 0)
            yield break;

        // commonTileSubstitutions.s scans $ae down through $01 before the
        // placed object stream. These controllers consume native slots in
        // that order, including while outgoing interactions retain capacity.
        for (int y = room.HeightInTiles-1; y >= 0; y--)
        for (int x = room.WidthInTiles-1; x >= 0; x--)
        {
            int packedPosition = (y << 4) | x;
            if (packedPosition == 0) continue;
            Vector2 point = PointForPackedPosition(packedPosition);
            int originalTile = room.GetOriginalMetatile(point);
            if (originalTile is < DungeonShutterEntry.FirstMinecartShutterTile or
                > DungeonShutterEntry.LastMinecartShutterTile)
            {
                continue;
            }
            // @temporarilyOpenDoor returns before allocation when an incoming
            // scroll's opposite edge does not match this shutter's direction.
            // Position controls opening; facing alone controls admission.
            if (placementContext.Kind == EnemyPlacementEntryKind.Scrolling &&
                !DungeonShutterEntry.FacesScrollingEntry(placementContext,
                    originalTile - DungeonShutterEntry.FirstMinecartShutterTile))
                continue;
            yield return CreateMinecartShutter(
                packedPosition, originalTile, oneShotOpener: false, room);
        }
    }

    private MinecartShutterRoomEntity CreateMinecartShutter(
        int packedPosition,
        int closedTile,
        bool oneShotOpener,
        OracleRoomData room) => new(
            packedPosition,
            closedTile,
            oneShotOpener,
            room,
            Resources.DungeonMechanics,
            owner.ToScreen,
            _animationTick,
            owner.OnSoundRequested,
            (position,tile) => {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("INTERAC$1e minecart setTile requires its active room session.");
                return rooms.TrySetTile(position,tile);
            },
            owner.UpdateBossShutterSignal,
            () => owner.DoorPaletteFadeActive,
            owner.IsScriptTextActive);

    private ToggleFloorRoomEntity CreateToggleFloor(OracleRoomData room) => new(room,Resources.DungeonInteractions,
        () => _runtimeState.ReadWramByte(WramAddress.wActiveTilePos),owner.TryCreateFloorToggleTile,
        () => owner.Entities<ToggleFloorTileRoomEntity>().Count,owner.IsOutgoingEntity,owner.OnRoomTileChanged,_animationTick);

    private DungeonSignalScriptRoomEntity CreateDungeonSignalScript(DungeonObjectRecord record)
    {
        if (rooms is null || _saveData is null) throw new InvalidOperationException($"{record.Source}: dungeon signal script requires active room/save owners.");
        var profile=Resources.SignalScripts.Profile(rooms.World.GetDungeonIndex(record.Group,record.Room),record);
        return new(profile,_runtimeState,() => owner.ActiveTriggers,owner.SetTrigger,
            () => owner.ActiveRoomHasFlag(_saveData,(byte)profile.OutputMask),
            () => _saveData.SetRoomFlag(owner.ActiveRoom.Group,owner.ActiveRoom.Id,(byte)profile.OutputMask),
            owner.IsScriptTextActive,owner.TryCreateBridgeSpawner,owner.OnSoundRequested)
            { Name=$"DungeonSignalScript_{record.SubId:x2}",Visible=false };
    }

    private DungeonRoomFlagMirrorRoomEntity CreateRoomFlagMirror(DungeonObjectRecord record)
    {
        var profile = Resources.RoomFlagMirrors.Profile(record);
        if (_saveData is null) throw new InvalidOperationException($"{profile.Source}: room-flag mirror requires live save bytes.");
        return new(record,owner.IsOutgoingEntity,() => owner.ActiveRoomHasFlag(_saveData,profile.Flag),
            () => _saveData.SetRoomFlag(owner.ActiveRoom.Group,profile.TargetRoom,profile.Flag));
    }

    private MermaidTorchOrderRoomEntity CreateMermaidTorchOrder(DungeonObjectRecord record,OracleRoomData room,LightableTorchState state)
    {
        if (_saveData is null || rooms is null) throw new InvalidOperationException($"{record.Source}: torch order requires live room/save owners.");
        return new(Resources.MermaidTorchOrder,room,() => owner.ActiveRoomHasFlag(_saveData,0x80),
            () => owner.RecreateLightableTorches(state),(position,tile) => {
                if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
            },owner.OnSoundRequested,() => {
                // FF~(DISABLE_ITEMS|DISABLE_ALL_BUT_INTERACTIONS) = $6f.
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0x6f);
                _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0x6f);
                _saveData.SetRoomFlag(owner.ActiveRoom.Group,0x25,0x40);
                owner.OnWallRetractionTriggered();
            });
    }

    internal LightableTorchScannerRoomEntity CreateLightableTorchScanner(LightableTorchState state) => new(
        new(owner.ActiveRoom.Group,owner.ActiveRoom.Id,0,InteractionId.CreateObjectAtEachTileIndex,0x08,0x06,0x10,TriggerPredicate.None,true),
        owner.ActiveRoom,state,Resources.DarkRooms,owner.TryCreateLightableTorch);

    internal MermaidChangingFloorWorkerRoomEntity CreateMermaidChangingFloorWorker(int subid) => new(
        subid,Resources.MermaidChangingFloor.Worker(subid),_runtimeState,(position,tile) =>
        {
            if (rooms is null) throw new InvalidOperationException($"INTERAC$90:${subid:x2}: floor worker requires the active room session.");
            if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
        });

    private FloorColorChangerRoomEntity CreateFloorColorChanger(DungeonObjectRecord record) => new(record,
        () => owner.ActiveRoom,Resources.DungeonInteractions,owner.TryCreateFloorColorWorker,
        () => owner.Entities<FloorColorWorkerRoomEntity>().Count);

    internal FloorColorWorkerRoomEntity CreateFloorColorWorker(Vector2 position,byte tile) => new(position,tile,
        Resources.DungeonInteractions,() => owner.ActiveRoom,(packed,replacement) =>
        {
            if (rooms is null) throw new InvalidOperationException("INTERAC $22:$01 lost its active room session.");
            bool accepted = rooms.TrySetTile(packed,replacement);
            if (accepted) owner.OnRoomTileChanged();
            return accepted;
        },_random,_runtimeState);

    internal ToggleFloorTileRoomEntity CreateFloorToggleTile(byte position,byte takeoff) => new(position,takeoff,
        Resources.DungeonInteractions.Constant("red-toggle-floor"),() => owner.ActiveRoom,(packed,tile) =>
        {
            if (rooms is null) throw new InvalidOperationException("INTERAC$15:$01 setTile requires its active room session.");
            bool written = rooms.TrySetTile(packed,tile);
            if (written) owner.OnRoomTileChanged();
            return written;
        },owner.OnSoundRequested);

    private IRoomEntity? CreateWingDungeonInteraction(
        DungeonObjectRecord record,
        OracleRoomData room,
        ColoredCubePuzzleState? puzzle,
        EnemyPlacementContext placementContext)
    {
        if (record.Kind is DungeonObjectKind.FloorPatternKey or DungeonObjectKind.ColoredBlockKey or DungeonObjectKind.ToggleFloor or DungeonObjectKind.FloorColorChanger && !owner.InteractionSlotAvailable)
            return null;
        if (record.Kind is DungeonObjectKind.CubeSwitchSensor or DungeonObjectKind.RedFloorTrigger or DungeonObjectKind.FloorSwitchBit or
            DungeonObjectKind.CubeColorSource or DungeonObjectKind.RedFlameTrigger or DungeonObjectKind.MinecartGate && !owner.InteractionSlotAvailable) return null;
        switch (record.Kind)
        {
            case DungeonObjectKind.RupeeReward:
                return CreateImmediateDungeonReward(
                    record, "TREASURE_OBJECT_RUPEES_0c");
            case DungeonObjectKind.FeatherReward:
                return CreateImmediateDungeonReward(
                    record, "TREASURE_OBJECT_FEATHER_00");
            case DungeonObjectKind.FloorPatternKey:
            case DungeonObjectKind.ColoredBlockKey:
                return new DungeonPatternKeyRoomEntity(
                    record,
                    () => owner.ActiveRoom,
                    Resources.DungeonInteractions.Constant(
                        record.Kind == DungeonObjectKind.FloorPatternKey
                            ? "red-toggle-floor"
                            : "red-pushable-block"),
                    [
                        Resources.WingDungeon.Pattern(record.Kind, 0),
                        Resources.WingDungeon.Pattern(record.Kind, 1),
                        Resources.WingDungeon.Pattern(record.Kind, 2)
                    ],
                    CreateFallingSmallKeyRequest(record),
                    () => owner.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem), owner.TryCreateGroundTreasure, owner.IsOutgoingEntity);
            case DungeonObjectKind.ToggleFloor:
                return CreateToggleFloor(room);
            case DungeonObjectKind.ColoredCube:
            case DungeonObjectKind.CubeFlame:
            case DungeonObjectKind.CubeLightSensor:
                return CreateColoredCubeInteraction(record, room, puzzle);
            case DungeonObjectKind.CubeSwitchSensor:
            case DungeonObjectKind.RedFloorTrigger:
            case DungeonObjectKind.FloorSwitchBit:
            case DungeonObjectKind.CubeColorSource:
            case DungeonObjectKind.RedFlameTrigger:
                return new DungeonStateController(
                    record, () => owner.ActiveRoom, Resources.DungeonInteractions,
                    puzzle ?? CreateColoredCubePuzzleState(),
                    _runtimeState, owner.SetTrigger);
            case DungeonObjectKind.SwitchTileToggler:
                return CreateSwitchTileToggler(record,room);
            case DungeonObjectKind.MinecartGate:
                return new MinecartGateRoomEntity(
                    record, () => owner.ActiveRoom, _runtimeState,
                    Resources.DungeonVisuals.Visual("minecart-gate"), owner.OnSoundRequested,
                    owner.OnRoomTileChanged, _animationTick);
            case DungeonObjectKind.EnemySmallKey:
                return new DungeonRewardRoomEntity(
                    record, Resources.DungeonInteractions, _saveData, () => owner.RoomEnemyCount,
                    CreateFallingSmallKeyRequest(record),
                    owner.EnableLinkCollisionsAndMenu);
            case DungeonObjectKind.MinibossReward:
                return new DungeonRewardRoomEntity(
                    record, Resources.DungeonInteractions, _saveData,
                    () => owner.RoomEnemyCount, treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal);
            case DungeonObjectKind.FloorColorChanger:
                return CreateFloorColorChanger(record);
            case DungeonObjectKind.CircularSidePlatform:
                return new CircularSideScrollPlatformRoomEntity(
                    record,
                    Resources.DungeonVisuals.Visual("circular-side-platform"),
                    Resources.DungeonInteractions.CircularSidePlatform(record.SubId));
            case DungeonObjectKind.HeadThwomp:
                ImportedEnemyDefinition headThwomp =
                    Resources.DungeonBosses.Enemy(EnemyId.HeadThwomp);
                var headThwompBoss = new HeadThwompBoss();
                headThwompBoss.Initialize(
                    headThwomp,
                    room,
                    record.Position,
                    _random,
                    Resources.DungeonBosses.HeadThwompPalettes,
                    owner.OnSoundRequested,
                    owner.BeginScreenShake,
                    owner.DisableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room),
                    _animationTick);
                return new HeadThwompBossRoomEntity(
                    headThwompBoss,
                    placementContext.BossEntryDirection);
            case DungeonObjectKind.Swoop:
                ImportedEnemyDefinition swoopRecord =
                    Resources.DungeonBosses.Enemy(EnemyId.Swoop);
                var swoop = new SwoopBoss();
                swoop.Initialize(
                    swoopRecord,
                    room,
                    record.Position,
                    _random,
                    owner.OnSoundRequested,
                    owner.BossShuttersClosed,
                    owner.BeginScreenShake,
                    owner.DisableLinkCollisionsAndMenu,
                    owner.EnableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room),
                    owner.OnRoomEntityDialogueRequested,
                    owner.IsTextActive,
                    _animationTick,
                    Resources.WingDungeon.SwoopMessage);
                return new SwoopBossRoomEntity(
                    swoop, placementContext.BossEntryDirection);
            case DungeonObjectKind.BossReward:
                DungeonBossRewardScriptDefinition bossReward =
                    Resources.WingDungeon.BossReward;
                return new HeadThwompRewardScript(
                    record,
                    bossReward,
                    room,
                    _saveData,
                    () => owner.RoomEnemyCount,
                    CreateDungeonBossRewardRequest(record, bossReward),
                    owner.EnableLinkCollisionsAndMenu,
                    owner.OnRoomTileChanged,
                    _animationTick);
            case DungeonObjectKind.Essence:
                return new DungeonEssence(
                    record,
                    Resources.DungeonVisuals.Visual("ancient-wood"),
                    Resources.DungeonVisuals.Visual("essence-pedestal"),
                    Resources.DungeonVisuals.Visual("essence-glow"),
                    Resources.DungeonVisuals.Visual("energy-bead"),
                    room,
                    _saveData?.HasRoomFlag(
                        record.Group,
                        record.Room,
                        OracleSaveData.RoomFlagItem) == true,
                    _animationTick,
                    owner.OnDungeonEssenceTriggered,
                    new DungeonEssenceDefinition(
                        1,
                        Resources.WingDungeon.EssenceMessage,
                        new Warp(
                            4, 0x38, -1, 0, 0, 1,
                            0x83, 0x25, 0, WarpDestinationTransition.SetRespawn)));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record), record, "Unsupported Wing Dungeon object.");
        }
    }

    private ImmediateDungeonRewardRoomEntity CreateImmediateDungeonReward(
        DungeonObjectRecord record,
        string treasureName)
    {
        var request = new GroundTreasureGrantRequest(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            treasureName,
            record.Source)
        {
            SpawnMode = TreasureSpawnMode.Instant,
            GrabMode = TreasureGrabMode.TwoHands
        };
        return new ImmediateDungeonRewardRoomEntity(record, request);
    }

    private IRoomEntity CreateMoonlitGrottoInteraction(
        DungeonObjectRecord record,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        switch (record.Kind)
        {
            case DungeonObjectKind.Essence:
                return new DungeonEssence(
                    record,
                    Resources.DungeonVisuals.Visual("echoing-howl"),
                    Resources.DungeonVisuals.Visual("essence-pedestal"),
                    Resources.DungeonVisuals.Visual("essence-glow"),
                    Resources.DungeonVisuals.Visual("energy-bead"),
                    room,
                    _saveData?.HasRoomFlag(
                        record.Group,
                        record.Room,
                        OracleSaveData.RoomFlagItem) == true,
                    _animationTick,
                    owner.OnDungeonEssenceTriggered,
                    new DungeonEssenceDefinition(
                        2,
                        Resources.MoonlitGrotto.EssenceMessage,
                        new Warp(
                            4, 0x49, -1, 0, 0, 0,
                            0xba, 0x55, 0, WarpDestinationTransition.SetRespawn)));
            case DungeonObjectKind.BossReward:
                return CreateDungeonReward(
                    record,
                    "TREASURE_OBJECT_HEART_CONTAINER_00",
                    falling: false);
            case DungeonObjectKind.MinibossReward:
                return new DungeonRewardRoomEntity(
                    record,
                    Resources.DungeonInteractions,
                    _saveData,
                    () => owner.RoomEnemyCount,
                    treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal);
            case DungeonObjectKind.Subterror:
                ImportedEnemyDefinition definition =
                    Resources.DungeonBosses.Enemy(EnemyId.Subterror);
                var subterror = new SubterrorBoss();
                subterror.Initialize(
                    definition,
                    room,
                    record.Position,
                    _random,
                    owner.OnSoundRequested,
                    owner.BossShuttersClosed,
                    owner.DisableLinkCollisionsAndMenu,
                    owner.EnableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room),
                    owner.OnRoomEntityDialogueRequested,
                    owner.IsTextActive,
                    _animationTick,
                    Resources.MoonlitGrotto.SubterrorMessage);
                return new SubterrorBossRoomEntity(
                    subterror,
                    placementContext.BossEntryDirection);
            case DungeonObjectKind.ShadowHag:
                ImportedEnemyDefinition shadowHagDefinition =
                    Resources.DungeonBosses.Enemy(EnemyId.ShadowHag);
                var shadowHag = new ShadowHagBoss();
                shadowHag.Initialize(
                    shadowHagDefinition,
                    room,
                    record.Position,
                    _random,
                    owner.OnSoundRequested,
                    owner.BossShuttersClosed,
                    owner.DisableLinkCollisionsAndMenu,
                    owner.EnableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room),
                    owner.OnRoomEntityDialogueRequested,
                    owner.IsTextActive,
                    Resources.MoonlitGrotto.ShadowHagMessage);
                return new ShadowHagBossRoomEntity(
                    shadowHag,
                    placementContext.BossEntryDirection);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record),
                    record,
                    "Unsupported Moonlit Grotto native object.");
        }
    }

    private GroundTreasureGrantRequest CreateFallingSmallKeyRequest(
        DungeonObjectRecord record) =>
        new(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            "TREASURE_OBJECT_SMALL_KEY_01",
            record.Source)
        {
            SpawnMode = TreasureSpawnMode.FromScreenTop,
            GrabMode = TreasureGrabMode.TwoHands,
            SpawnDelayFrames =
                Resources.DungeonInteractions.Constant("falling-key-spawn-delay"),
            BounceCount = 2,
            Gravity = 0x10,
            BounceSpeed = -0xaa,
            SpawnSound = SoundId.SndSolvePuzzle,
            LandingSound = SoundId.SndDropEssence,
            InitialZAboveScreen = true
        };

    private GroundTreasureGrantRequest CreateFallingSmallKeyRequest(
        DungeonMechanicDatabaseRecord record,
        string source) => CreateFallingSmallKeyRequest(
            record.Group,
            record.Room,
            record.Order,
            record.PackedPosition,
            record.Parameter,
            source);

    private GroundTreasureGrantRequest CreateFallingSmallKeyRequest(
        int group,
        int room,
        int order,
        int y,
        int x,
        string source) =>
        new(
            group,
            room,
            order,
            y,
            x,
            "TREASURE_OBJECT_SMALL_KEY_01",
            source)
        {
            SpawnMode = TreasureSpawnMode.FromScreenTop,
            GrabMode = TreasureGrabMode.TwoHands,
            SpawnDelayFrames =
                Resources.DungeonInteractions.Constant("falling-key-spawn-delay"),
            BounceCount = 2,
            Gravity = 0x10,
            BounceSpeed = -0xaa,
            SpawnSound = SoundId.SndSolvePuzzle,
            LandingSound = SoundId.SndDropEssence,
            InitialZAboveScreen = true
        };

    private IRoomEntity CreateArmosWarrior(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        var actor = new ArmosWarriorActor();
        var entry = new BossEntryMovement(placementContext.BossEntryDirection);
        actor.Initialize(Resources.DungeonBosses.Enemy(EnemyId.ArmosWarrior), new ArmosWarriorEnvironment(room, Resources.ArmosWarrior, Resources.DungeonBosses,
            _random, owner.BossShuttersClosed, () => owner.PartSlotAvailable, owner.CanAllocateEnemies,
            () => -(int)owner.ToScreen(Vector2.Zero).Y, owner.OnSoundRequested, owner.BeginScreenShake,
            owner.DisableLinkCollisionsAndMenu, owner.EnableLinkCollisionsAndMenu,
            () => owner.OnRoomMusicRequested(record.Group, record.Room), owner.OnRoomEntityDialogueRequested, entry), record.Position);
        return new ArmosWarriorRoomEntity(actor, entry);
    }

    private IRoomEntity CreateSmasher(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        var entry = new BossEntryMovement(placementContext.BossEntryDirection);
        SmasherRoomEnvironment? environment = null;
        environment = new SmasherRoomEnvironment(
            _ => (owner.TryAllocateEnemy(slot =>
            {
                var child = new SmasherCharacter { Name = "Smasher_74_Parent" };
                child.InitializePending(enemies.ImportedEnemy(EnemyId.Smasher,1),room,Vector2.Zero,_random,slot);
                return new SmasherRoomEntity(child,environment!,false);
            }) as SmasherRoomEntity)?.Character,
            () => { owner.EnableLinkCollisionsAndMenu(); owner.OnSoundRequested(SoundId.SndCtrlStopMusic); },
            () => { owner.EnableLinkCollisionsAndMenu(); owner.OnSoundRequested(SoundId.MusMiniboss); },
            () => owner.InteractionSlotAvailable,() => owner.PartSlotAvailable,owner.DisableLinkCollisionsAndMenu,
            () => owner.OnRoomMusicRequested(record.Group,record.Room),owner.OnSoundRequested,0,true,entry,() => owner.FrameCounter,owner.TryCreatePuzzlePuff);
        var ball = new SmasherCharacter { Name = "Smasher_74_Ball" };
        ball.InitializePending(enemies.ImportedEnemy(EnemyId.Smasher,0),room,record.Position,_random,0);
        return new SmasherRoomEntity(ball,environment,true);
    }

    private IRoomEntity CreateVire(DungeonObjectRecord record,OracleRoomData room,EnemyPlacementContext placementContext)
    {
        var entry = new BossEntryMovement(placementContext.BossEntryDirection);
        VireRoomEnvironment? world = null;
        world = new(
            scrolling => {
                owner.EnableLinkCollisionsAndMenu(); owner.OnSoundRequested(SoundId.SndCtrlStopMusic); owner.BeginBossEntrySignal();
                // Destination state0 dispatch runs in SCROLLMODE$08, whose
                // bit0 is clear: the source queues Link's force movement.
                // Consume that request after the scroll finishes.
                entry.Arm();
            },
            () => owner.OnSoundRequested(SoundId.MusMiniboss),owner.DisableLinkCollisionsAndMenu,owner.EnableLinkCollisionsAndMenu,
            () => owner.OnRoomMusicRequested(record.Group,record.Room),owner.OnSoundRequested,
            (id,point) => owner.OnRoomEntityDialogueRequested(id,Resources.VireDialogue.Text(id),point),
            () => _saveData?.IsLinkedGame == true,() => -owner.ToScreen(Vector2.Zero),
            owner.TryCreateVirePuff,owner.InteractionAnimationParameter,owner.TryCreateVireProjectile,
            owner.CanAllocateVireBats,
            (parent,index) => owner.TryAllocateEnemy(slot => {
                var child = new VireCharacter { Name = $"VireBat_{index}" };
                child.Initialize(enemies.ImportedEnemy(EnemyId.Vire,1),room,parent.Position,_random,_runtimeState,world!,parent.Slot,index,parent.ZFixed >> 8);
                return new VireRoomEntity(child,world!);
            }) is not null,
            slot => owner.ResolveEnemySlot(slot)?.Node as VireCharacter,owner.TryCreatePart,entry);
        var actor = new VireCharacter { Name = "Vire_75_Main" };
        actor.Initialize(enemies.ImportedEnemy(EnemyId.Vire,0),room,record.Position,_random,_runtimeState,world);
        return new VireRoomEntity(actor,world);
    }

    private IRoomEntity CreateOctogon(DungeonObjectRecord record,Vector2 position,EnemyPlacementContext placementContext)
    {
        var entry = new BossEntryMovement(placementContext.BossEntryDirection);
        OctogonRoomEnvironment? world = null;
        bool SpawnInteraction(Vector2 point,int id,int parameter,int related)
        {
            if (!owner.InteractionSlotAvailable) return false;
            owner.AddEntity(new OctogonInteractionRoomEntity(point,id,parameter,parameter == 1 ? related : -1,
                _random,_runtimeState,slot => owner.ResolveEnemySlot(slot)?.Node as OctogonCharacter,SpawnInteraction,
                parameter == 0 ? related : 0));
            return true;
        }
        world = new(scrolling => {
            owner.EnableLinkCollisionsAndMenu(); owner.OnNativeSoundRequested(SoundId.SndCtrlStopMusic); owner.BeginBossEntrySignal();
            // Native destination state0 runs in SCROLLMODE$08 before bit0
            // is restored, so its force-movement request survives the scroll.
            entry.Arm();
        },() => owner.BossEntrySignal,owner.OnNativeSoundRequested,owner.NativeActiveMusicSource,owner.SetNativeActiveMusic,owner.DisableLinkCollisionsAndMenu,
        () => owner.OnRoomMusicRequested(record.Group,record.Room),
        () => { _saveData!.SetRoomFlag(5,0x2d,0x80); _saveData.SetRoomFlag(5,0x36,0x80); },
        parent => {
            int childSlot = -1;
            owner.TryAllocateEnemy(slot => {
                childSlot = slot;
                var shell = new OctogonCharacter { Name = "Octogon_7d_Shell" };
                shell.Initialize(enemies.ImportedEnemy(EnemyId.Octogon,2),Vector2.Zero,_random,_runtimeState,world!,parent.Slot);
                return new OctogonRoomEntity(shell,world!);
            });
            return childSlot;
        },slot => owner.ResolveEnemySlot(slot)?.Node as OctogonCharacter,owner.TryCreatePart,SpawnInteraction,entry);
        var actor = new OctogonCharacter { Name = $"Octogon_7d_{record.SubId:x2}" };
        actor.Initialize(enemies.ImportedEnemy(EnemyId.Octogon,record.SubId),position,_random,_runtimeState,world);
        return new OctogonRoomEntity(actor,world);
    }

    private static IRoomEntity CreateArmosChild(ArmosWarriorChildSpawn spawn)
    {
        var actor = spawn.Spawner.CreateChild(spawn.SubId);
        return new ArmosWarriorRoomEntity(actor, actor.Entry);
    }

    private static IRoomEntity CreateKingMoblinMinion(KingMoblinMinionSpawn spawn)
    {
        var actor = new KingMoblinMinion(); actor.Initialize(spawn.Boss,spawn.SubId);
        return new KingMoblinMinionRoomEntity(actor);
    }
    private static IRoomEntity CreateKingMoblinBomb(KingMoblinBombSpawn spawn)
    {
        var actor = new KingMoblinBomb(); actor.Initialize(spawn.Boss,spawn.Minion);
        return new KingMoblinBombRoomEntity(actor);
    }
    private IRoomEntity CreateExclamationMark(ExclamationMarkSpawn spawn)
    {
        var actor = new NpcCharacter();
        actor.Initialize(Resources.Moosh.CreateExclamationRecord((int)spawn.Position.Y,(int)spawn.Position.X));
        actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex); // INTERAC $9f state0: visible80.
        owner.OnSoundRequested(SoundId.SndClink);
        return new ExclamationMarkRoomEntity(actor,30);
    }

    private IRoomEntity CreateEyesoar(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        var actor = new EyesoarActor();
        var entry = new BossEntryMovement(placementContext.BossEntryDirection);
        actor.Initialize(Resources.DungeonBosses.Enemy(EnemyId.Eyesoar), new EyesoarEnvironment(room, Resources.Eyesoar, Resources.DungeonBosses,
            Resources.DungeonVisuals.Visual("eyesoar-spawn"), _random, owner.BossShuttersClosed, () => owner.PartSlotAvailable,
            owner.CanAllocateEnemies, () => owner.InteractionSlotAvailable, owner.OnSoundRequested, owner.DisableLinkCollisionsAndMenu,
            owner.EnableLinkCollisionsAndMenu, entry), record.Position);
        return new EyesoarRoomEntity(actor);
    }

    private static GroundTreasureGrantRequest CreateDungeonBossRewardRequest(
        DungeonObjectRecord record,
        DungeonBossRewardScriptDefinition definition) =>
        new(
            record.Group,
            record.Room,
            record.Order,
            definition.RewardY,
            definition.RewardX,
            "TREASURE_OBJECT_HEART_CONTAINER_00",
            definition.Source)
        {
            SpawnMode = TreasureSpawnMode.Instant,
            GrabMode = TreasureGrabMode.TwoHands
        };

    private static bool HasColoredCubeState(
        IReadOnlyList<DungeonObjectRecord> records)
    {
        foreach (DungeonObjectRecord record in records)
        {
            if (record.Kind is DungeonObjectKind.ColoredCube or
                DungeonObjectKind.CubeFlame or
                DungeonObjectKind.CubeLightSensor or
                DungeonObjectKind.CubeFloorSensor or
                DungeonObjectKind.CubeSwitchSensor or
                DungeonObjectKind.CubeColorSource or
                DungeonObjectKind.RedFlameTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private DungeonRewardRoomEntity CreateDungeonReward(
        DungeonObjectRecord record,
        string treasureName,
        bool falling)
    {
        var request = new GroundTreasureGrantRequest(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            treasureName,
            record.Source)
        {
            SpawnMode = falling ? TreasureSpawnMode.FromScreenTop :
                record.Kind == DungeonObjectKind.BossReward ? TreasureSpawnMode.Puff : TreasureSpawnMode.Instant,
            GrabMode = TreasureGrabMode.TwoHands,
            SpawnDelayFrames = falling ? 40 :
                record.Kind == DungeonObjectKind.BossReward ? Resources.DungeonInteractions.Constant("treasure-puff-wait") : 0,
            BounceCount = falling ? 2 : 0,
            Gravity = falling ? 0x10 : 0,
            BounceSpeed = falling ? -0xaa : 0,
            SpawnSound = falling ? SoundId.SndSolvePuzzle : 0,
            LandingSound = falling ? SoundId.SndDropEssence : 0,
            InitialZAboveScreen = falling
        };
        return new DungeonRewardRoomEntity(
            record, Resources.DungeonInteractions, _saveData, () => owner.RoomEnemyCount, request,
            owner.EnableLinkCollisionsAndMenu, trySpawnTreasure: owner.TryCreateGroundTreasure);
    }

    private DungeonEssence CreateLaterDungeonEssence(DungeonObjectRecord record, OracleRoomData room)
    {
        var definition = new[] { Resources.SkullDungeon.Essence, Resources.CrownDungeon.Essence, Resources.MermaidDungeon.Essence }.Single(definition =>
            definition.ExitWarp.SourceGroup == record.Group && definition.ExitWarp.SourceRoom == record.Room);
        string visual = definition.Index switch
        {
            3 => "burning-flame",
            4 => "sacred-soil",
            5 => "bereft-peak",
            _ => throw new InvalidOperationException($"{record.Source}: missing Essence visual for index${definition.Index:x2}.")
        };
        return new DungeonEssence(record, Resources.DungeonVisuals.Visual(visual),
            Resources.DungeonVisuals.Visual("essence-pedestal"), Resources.DungeonVisuals.Visual("essence-glow"),
            Resources.DungeonVisuals.Visual("energy-bead"), room,
            _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem)==true,
            _animationTick,owner.OnDungeonEssenceTriggered,definition);
    }

    private DungeonRewardRoomEntity CreateEnemySmallKeyReward(
        GroundTreasureGrantRequest request)
    {
        var record = new DungeonObjectRecord(
            request.Group,
            request.Room,
            request.Order,
            DungeonObjectKind.EnemySmallKey,
            InteractionId.DungeonStuff,
            0x01,
            request.Y,
            request.X,
            DungeonObjectCondition.ItemClear,
            request.Source);
        return new DungeonRewardRoomEntity(
            record,
            Resources.DungeonInteractions,
            _saveData,
            () => owner.RoomEnemyCount,
            request,
            owner.EnableLinkCollisionsAndMenu);
    }

    private GroundTreasureGrantRequest CreatePlacedFallingSmallKeyRequest(
        DungeonMechanicDatabaseRecord record)
    {
        int y = (record.PackedPosition >> 4) * OracleRoomData.MetatileSize + 8;
        int x = (record.PackedPosition & 0x0f) * OracleRoomData.MetatileSize + 8;
        return CreateFallingSmallKeyRequest(
            record.Group,
            record.Room,
            record.Order,
            y,
            x,
            $"objects/ages/mainData.s:group{record.Group}Map" +
            $"{record.Room:x2}ObjectData/INTERAC_DUNGEON_STUFF $12:$01");
    }

    private static ColoredCubePuzzleState RequireColoredCubePuzzle(
        ColoredCubePuzzleState? puzzle,
        DungeonObjectRecord record) =>
        puzzle ?? throw new InvalidOperationException(
            $"{record.Source} is missing its shared rotating-cube state binding.");

    private ColoredCubePuzzleState CreateColoredCubePuzzleState() =>
        new(_runtimeState,Resources.DungeonInteractions.Constant("red-pushable-block"));

    private bool DungeonObjectConditionMet(
        DungeonObjectRecord record) => record.Predicate switch
    {
        DungeonObjectCondition.Always => true,
        DungeonObjectCondition.ItemClear =>
            _saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlagItem) != true,
        DungeonObjectCondition.Flag80Clear =>
            _saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlag80) != true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(record), record, "Unknown dungeon-object predicate.")
    };

    private IRoomEntity? CreateDungeonMechanic(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        int group,
        bool enemyMechanicsSupported,
        EnemyPlacementContext placementContext,
        LightableTorchState? lightableTorchState)
    {
        // These source families are placed INTERACTION records. Test their
        // pool before constructors can write tiles or create linked state.
        if (record.Id is InteractionId.DoorController or InteractionId.TriggerTranslator or InteractionId.CreateObjectAtEachTileIndex && !owner.InteractionSlotAvailable)
            return null;
        if (record.Id == InteractionId.Miscellaneous1 && record.SubId == 0x0f && !owner.InteractionSlotAvailable)
            return null;
        if (record.Id == InteractionId.Miscellaneous1 && record.SubId == 0x0f)
            return new NuunBridgeRoomEntity(record, room,
                _saveData ?? throw new InvalidOperationException("INTERAC $6b:$0f requires live save state."),
                _runtimeState, _animationTick, owner.OnRoomTileChanged, owner.OnSoundRequested,
                (position,tile) => {
                    if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                        throw new InvalidOperationException("INTERAC$6b:$0f setTile requires its active room session.");
                    return rooms.TrySetTile(position,tile);
                });
        if (record.Id == InteractionId.Miscellaneous2 && record.SubId == 0x12)
        {
            if (!owner.InteractionSlotAvailable) return null;
            return new OrbBridgeControllerRoomEntity(record,
                _saveData ?? throw new InvalidOperationException(
                    $"Room {group:x1}:{room.Id:x2} $dc:$12 requires live save state."),
                _runtimeState, owner.OnSoundRequested, Resources.DungeonMechanics.SolveSound, rooms, owner.TryCreateBridgeSpawner);
        }
        if (record.Id == InteractionId.Miscellaneous2 && record.SubId is 0x0c or 0x0d)
            return new RidgeBridgeControllerRoomEntity(record,
                _saveData ?? throw new InvalidOperationException($"$dc:${record.SubId:x2} requires live save state."),
                () => owner.ActiveTriggers, () => owner.PartSlotAvailable, owner.OnSoundRequested, Resources.DungeonMechanics);
        if (record.Id == InteractionId.Splash)
        {
            return new DungeonOrbRoomEntity(
                record,
                Resources.DungeonMechanics,
                Resources.DungeonVisuals.Visual("grotto-orb"),
                room,
                _runtimeState,
                _animationTick,
                owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.ExtendableBridge)
        {
            return new ExtendableBridgeRoomEntity(
                record,
                room,
                Resources.DungeonMechanics,
                _runtimeState,
                _animationTick,
                owner.OnRoomTileChanged,
                owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.SmogBoss)
        {
            // obj_Part skips an allocation failure before any
            // object state or constructor side effects can run.
            if (!owner.PartSlotAvailable) return null;
            return new RotatableSeedThingRoomEntity(
                record,
                Resources.DungeonMechanics,
                Resources.DungeonVisuals.Visual("rotatable-seed-thing"),
                room,
                _runtimeState,
                _animationTick,owner.TryCreateSeedReflectorChild);
        }
        if (record.Id == InteractionId.TriggerTranslator && record.SubId == 0x02)
        {
            return new TorchTriggerTranslatorRoomEntity(
                record,
                lightableTorchState ?? throw MissingLightableTorchState(record),
                owner.SetTrigger,owner.IsOutgoingEntity);
        }
        if (record.Id == InteractionId.CreateObjectAtEachTileIndex && record.SubId == 0x08)
        {
            return new LightableTorchScannerRoomEntity(
                record,
                room,
                lightableTorchState ?? throw MissingLightableTorchState(record),
                Resources.DarkRooms,owner.TryCreateLightableTorch);
        }
        if (record.Id == InteractionId.CreateObjectAtEachTileIndex && record.SubId == 0x04)
            return new RespawnableBushScannerRoomEntity(record, room);
        if (record.Id == InteractionId.DungeonEvents && record.SubId == 0x09)
        {
            if (!owner.InteractionSlotAvailable) return null;
            return new DungeonTilePatternFallingKeyRoomEntity(
                record,
                Resources.DungeonMechanics.TilePattern(record.Id, record.SubId),
                () => owner.ActiveRoom,
                CreateFallingSmallKeyRequest(
                    record,
                    "dungeon_mechanics.tsv:INTERAC_DUNGEON_EVENTS $21:$09"),
                () => owner.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),owner.TryCreateGroundTreasure);
        }
        if (record.Id == InteractionId.DungeonEvents && record.SubId is 0x0a or 0x0c)
        {
            return new MoonlitGrottoArmosEventRoomEntity(
                record,
                Resources.DungeonMechanics,
                _saveData,
                _runtimeState,
                () => owner.ActiveTriggers,
                record.SubId == 0x0c
                    ? CreateFallingSmallKeyRequest(
                        record.Group,
                        record.Room,
                        record.Order,
                        Resources.DungeonMechanics.MoonlitButtonKeyY,
                        Resources.DungeonMechanics.MoonlitButtonKeyX,
                        "objects/ages/extraData3.s:moonlitGrotto_onArmosSwitchPressed")
                    : null);
        }
        if (record.Id == InteractionId.DungeonEvents && record.SubId == 0x0d)
        {
            if (_saveData?.HasGlobalFlag(Resources.DungeonMechanics.MoonlitGlobalFlag) == true ||
                _saveData?.HasRoomFlag(
                    group,
                    room.Id,
                    (byte)Resources.DungeonMechanics.MoonlitRoomFlag) == true)
            {
                return null;
            }
            return new MoonlitGrottoCrystalEventRoomEntity(
                record,
                Resources.DungeonMechanics,
                _saveData,
                _runtimeState,
                owner.OnSoundRequested,
                owner.BeginScreenShake,
                owner.OnRoomEntityDialogueRequested,
                owner.IsTextActive);
        }
        if (record.Id == InteractionId.DungeonEvents && record.SubId == 0x0e)
        {
            if (!owner.InteractionSlotAvailable) return null;
            return new MoonlitGrottoFallingKeyRoomEntity(
                record,
                Resources.DungeonMechanics,
                () => owner.ActiveRoom,
                CreateFallingSmallKeyRequest(
                    record,
                    "dungeon_mechanics.tsv:INTERAC_DUNGEON_EVENTS $21:$0e"),
                () => owner.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),owner.TryCreateGroundTreasure);
        }
        if (record.Id == InteractionId.TriggerTranslator)
        {
            return new MoonlitGrottoCrystalRoomEntity(
                record,
                Resources.DungeonMechanics,
                Resources.DungeonVisuals.Visual("grotto-crystal"),
                Resources.DungeonVisuals.Visual("grotto-crystal-break"),
                room,
                _saveData,
                _runtimeState,
                _animationTick,
                owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.Puff)
        {
            return CreateDungeonSwitch(record,room);
        }
        if (record.Id == InteractionId.SnowDebris)
        {
            if (!owner.PartSlotAvailable) return null;
            return new GroundButtonRoomEntity(
                record, room, Resources.DungeonMechanics, owner.SetTrigger,
                (position,tile) =>
                {
                    if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                        throw new InvalidOperationException("PART$09 setTile requires its active room session.");
                    if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                }, owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.DungeonEvents && record.SubId == 0x17)
        {
            // No object updates occur between these placed interaction rows;
            // a full pool leaves this record unallocated, without retry.
            if (!owner.InteractionSlotAvailable) return null;
            return new RetractableTriggerChestRoomEntity(record,room,Resources.DungeonMechanics,() => owner.ActiveTriggers,
                () => _saveData?.HasRoomFlag(group,room.Id,OracleSaveData.RoomFlagItem)==true,
                owner.IsOutgoingEntity,(position,tile) =>
                {
                    if(rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                        throw new InvalidOperationException("INTERAC$21:$17 setTile requires its active room session.");
                    if(rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                },owner.TryCreatePuzzlePuff,owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.DungeonScript)
        {
            return new TriggerChestRoomEntity(
                record, room, Resources.DungeonMechanics, () => owner.ActiveTriggers,
                () => _saveData?.HasRoomFlag(
                    group, room.Id, OracleSaveData.RoomFlagItem) == true,
                _animationTick, owner.OnSoundRequested);
        }
        if (record.Id == InteractionId.DungeonStuff && record.SubId == 0x01)
        {
            if (_saveData?.HasRoomFlag(
                    group, room.Id, OracleSaveData.RoomFlagItem) == true ||
                !enemyMechanicsSupported)
            {
                return null;
            }
            return CreateEnemySmallKeyReward(
                CreatePlacedFallingSmallKeyRequest(record));
        }
        if (record.Id == InteractionId.DungeonStuff && record.SubId == 0x04)
        {
            if (!record.CountSourceComplete || !DungeonEnemyCountIsComplete(group, room))
                throw new InvalidOperationException($"INTERAC$12:$04 in {group:x1}:{room.Id:x2} requires complete enemy counting.");
            if (!owner.InteractionSlotAvailable) return null;
            return new EnemyClearStairsRoomEntity(record, room, Resources.DungeonMechanics,
                _saveData ?? throw new InvalidOperationException("INTERAC$12:$04 requires room flags."),
                () => owner.RoomEnemyCount, _animationTick, owner.OnSoundRequested, owner.TryCreatePuzzlePuff);
        }
        if (record.Id == InteractionId.DungeonStuff)
        {
            if (_saveData?.HasRoomFlag(
                    group, room.Id, OracleSaveData.RoomFlagItem) == true ||
                !record.CountSourceComplete ||
                !DungeonEnemyCountIsComplete(group, room))
            {
                return null;
            }
            return new EnemyClearChestRoomEntity(
                record, room, Resources.DungeonInteractions, () => owner.RoomEnemyCount,
                owner.OnSoundRequested, owner.OnRoomTileChanged, _animationTick);
        }
        if (record.Id == InteractionId.PushBlockTrigger && !enemyMechanicsSupported)
            return null;
        return record.Id switch
        {
            InteractionId.PushBlockTrigger => new PushBlockTriggerRoomEntity(
                record, room, Resources.DungeonMechanics,
                () => owner.RoomEnemyCount, _animationTick, owner.ClearRoomEnemyCount, owner.IsOutgoingEntity),
            InteractionId.DoorController => new DungeonDoorRoomEntity(
                record, room, Resources.DungeonMechanics, () => owner.RoomEnemyCount,
                owner.TriggerIsActive, owner.ToScreen, _animationTick,
                owner.OnSoundRequested, placementContext, enemyMechanicsSupported,
                (position,tile) => {
                    if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                        throw new InvalidOperationException("INTERAC$1e setTile requires its active room session.");
                    return rooms.TrySetTile(position,tile);
                },owner.UpdateBossShutterSignal,owner.IsScriptTextActive,owner.IsOutgoingEntity,() => owner.DoorPaletteFadeActive,
                record.SubId >= 0x14
                    ? () => (lightableTorchState ?? throw MissingLightableTorchState(record)).LitCount : null,
                () => _runtimeState.SetWramByte(WramAddress.wTmpcfc0,
                    (byte)(_runtimeState.ReadWramByte(WramAddress.wTmpcfc0)^Resources.DungeonMechanics.DoorEntryScratchMask))),
            _ => throw new InvalidOperationException(
                $"Unsupported dungeon interaction ${record.Id:x2}:" +
                $"${record.SubId:x2} in room {group:x1}:{room.Id:x2}.")
        };
    }

    private static InvalidOperationException MissingLightableTorchState(
        DungeonMechanicDatabaseRecord record) => new(
        $"Room {record.Group:x1}:{record.Room:x2} ${record.Id:x2}:" +
        $"${record.SubId:x2} is missing its room-local torch state.");

    private IRoomEntity CreateSharedDungeonInteraction(
        PlacementRecord record,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        switch (record.Kind)
        {
            case DungeonEntranceInteractionDatabaseObjectKind.Entry:
                return new DungeonEntranceRoomEntity(
                    new Vector2(record.X, record.Y),
                    Resources.DungeonEntrances.Entry(record.Dungeon),
                    Resources.DungeonEntrances,
                    _runtimeState,
                    placementContext.Kind == EnemyPlacementEntryKind.ScreenWarp,
                    dungeon => MinecartRuntimeState.Reset(
                        _runtimeState, Resources.StaticDungeonObjects.Minecarts(dungeon)),
                    owner.OnDungeonEntranceTriggered);

            case DungeonEntranceInteractionDatabaseObjectKind.EyeSpawner:
                return new StatueEyeballSpawnerRoomEntity(room, Resources.DungeonEntrances, owner.TryCreateStatueEyeball);

            case DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal:
                var portal = new MinibossPortal();
                portal.Initialize(Resources.DungeonEntrances);
                return new MinibossPortalRoomEntity(
                    portal, record, Resources.DungeonEntrances, _saveData,
                    owner.OnRoomWarpRequested, owner.OnSoundRequested);

            default:
                throw new InvalidOperationException(
                    $"Unsupported shared dungeon interaction kind in {record.Source}.");
        }
    }

    private IRoomEntity CreateSmogProjectile(SmogProjectileSpawn spawn, OracleRoomData room)
    {
        if (!owner.PartSlotAvailable)
            throw new NotSupportedException("smog.s ecom_spawnProjectile PART$4a: unchecked allocation into a full native PART pool is not represented.");
        int group = rooms?.ActiveGroup ?? 0;
        return new SmogProjectileRoomEntity(new SmogProjectilePart(Resources.SmogProjectile, room, spawn.Position, spawn.SubId),
            Resources.SmogProjectile, () => (Vector2I)(-owner.ToScreen(Vector2.Zero)).Floor(),
            () => _saveData?.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlag40) == true ? 0x40 : 0, () => owner.RoomEnemyCount, owner.OnSoundRequested);
    }

    private bool DungeonChestItemFlagSet()
    {
        // getThisRoomFlags reads the active room, including outgoing $21:$12.
        return owner.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem);
    }

    private void WriteDungeonChest(DungeonObjectRecord record)
    {
        if (rooms is null)
            throw new InvalidOperationException($"INTERAC ${record.Id:x2} chest has no room session at {record.Source}.");
        // objectGetTileAtPosition and setTile use the current CF/CE buffers.
        // Outgoing $21:$12 has no enabled$02 deletion gate; an incoming cube
        // signal can complete it while its original position is retained.
        OracleRoomData room = rooms.CurrentRoom;
        if (rooms.TrySetTile((byte)room.GetPackedPosition(record.Position),(byte)Resources.DungeonMechanics.ChestTile)) owner.OnRoomTileChanged();
    }

    private IRoomEntity CreateSmogSentinel(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        Vector2I direction = placementContext.BossEntryDirection;
        var entry = new BossEntryMovement(direction);
        return CreateSmogEnemy(new(record.Position,record.SubId),room,entry,scrolling =>
        {
            owner.EnableLinkCollisionsAndMenu();
            owner.OnSoundRequested(SoundId.SndCtrlStopMusic);
            owner.BeginBossEntrySignal();
            if (scrolling) entry.Arm();
            // Normal room mode returns A=1. Scrolling preload returns the
            // cardinal angle after requesting Link's $16-update forced walk.
            return !scrolling || direction == Vector2I.Zero ? 1 : CarriedObjectMotion.DirectionIndex(direction) * 8;
        });
    }

    internal SmogEncounterRoomEntity CreateSmogEncounter(Vector2I position, OracleRoomData room)
    {
        if (rooms is null || _saveData is null || !ReferenceEquals(rooms.CurrentRoom,room))
            throw new NotSupportedException("INTERAC$33 requires the active room session and save-byte owner.");
        int group = rooms.ActiveGroup;
        return new(Resources.SmogController,position,new SmogEncounterServices(
            () => _saveData.GetRoomFlags(group,room.Id),
            () => owner.BossEntrySignal != 0,() => owner.RoomEnemyCount,
            owner.LockSmogLinkAndMenu,owner.EnableLinkCollisionsAndMenu,
            enabled => _saveData.SetRoomFlag(group,room.Id,OracleSaveData.RoomFlag40,enabled),
            spawn =>
            {
                if (owner.TryAllocateEnemy(_ => CreateSmogEnemy(spawn,room)) is null)
                    ApplyFailedSmogControllerAllocation(spawn, merged: false);
            },
            puffPosition => { owner.TryCreatePuzzlePuff(puffPosition); },
            packed => room.GetTerrainInfo(new Vector2((packed & 15)*16+8,(packed >> 4)*16+8)).Collision,
            tilePosition => room.GetTerrainInfo(tilePosition).Tile,
            (packed,tile) =>
            {
                if (!ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("INTERAC$33 attempted a tile write after its room was replaced.");
                bool accepted = rooms.TrySetTile((byte)packed,(byte)tile);
                if (accepted) owner.OnRoomTileChanged();
                return accepted;
            },
            owner.MergeSmogClouds,() => owner.OnSoundRequested(SoundId.SndSplash),owner.ReleaseSmogSentinelCount));
    }

    private IRoomEntity CreateSmogEnemy(SmogEnemySpawn spawn, OracleRoomData room,
        BossEntryMovement? entry = null, Func<bool,int>? initializeBossRoom = null)
    {
        if (!owner.CanAllocateEnemies(1)) throw new NotSupportedException("smog.s unchecked ENEMY$7c allocation into a full native pool is not represented.");
        var actor = new SmogCharacter();
        var record = enemies.ImportedEnemy(EnemyId.Smog,0);
        byte Collision(int packed)
        {
            int x = packed & 15, y = packed >> 4;
            if (x < room.WidthInTiles && y < room.HeightInTiles)
                return room.GetTerrainInfo(new Vector2(x * 16 + 8,y * 16 + 8)).Collision;
            // bank0 loadRoomCollisions blanks only these native perimeter rows
            // and columns. Other bytes outside the room are not modeled RAM.
            if (y == 15 || y == room.HeightInTiles ||
                y < 11 && (x == 15 || room.WidthInTiles == 10 && x == 10)) return 0xff;
            throw new NotSupportedException($"smog.s collision read at packed${packed:x2} requires unrepresented off-room WRAM.");
        }
        if (spawn.SubId is 0 or 1) actor.InitializeIntro(record,spawn.SubId,spawn.Position);
        else if (spawn.SubId is 2 or 0x82) actor.InitializeSmallCloud(record,spawn.SubId,spawn.Phase,spawn.Position,spawn.Direction,Collision);
        else if (spawn.SubId is 3 or 0x83) actor.InitializeMergedCloud(record,spawn.SubId,spawn.Phase,spawn.Position,spawn.Direction,Collision);
        else if (spawn.SubId is 5 or 6) actor.InitializeRoomSentinel(record,spawn.SubId,spawn.Position);
        else { actor.Free(); throw new NotSupportedException($"Smog spawn subid${spawn.SubId:x2} is not represented."); }
        int group = rooms?.ActiveGroup ?? 0;
        var environment = new SmogRoomEnvironment(
            () => owner.IsTextActive() || owner.RoomEntityFreezeActive(),owner.IsTextActive,
            () => _saveData?.HasRoomFlag(group,room.Id,OracleSaveData.RoomFlag40) == true ? 0x40 : 0,() => owner.RoomEnemyCount,
            () => _random.Next().Value,
            () =>
            {
                // enemyBoss_beginBoss clears the controller's Link/menu lock,
                // not wDisableLinkCollisionsAndMenu. A separate broad freeze
                // still requires its own native ownership to be represented.
                if (owner.RoomEntityFreezeActive()) throw new NotSupportedException("Smog enemyBoss_beginBoss cannot clear an unrelated broad wDisabledObjects owner.");
                owner.ReleaseSmogLinkAndMenu();
                owner.OnSoundRequested(SoundId.MusBoss);
            },
            (id,position) => owner.OnRoomEntityDialogueRequested(id,enemies.SmogIntroText,position),
            child =>
            {
                if (owner.TryAllocateEnemy(_ => CreateSmogEnemy(child,room)) is not null) return;
                // Intro splitting ignores getFreeEnemySlot failure (HL=$e080).
                // Its ID/subid stores hit sound channel volumes3/4 via echo RAM;
                // route those bytes to the authoritative sound driver.
                owner.WriteNativeChannelVolume(3, 0x7c);
                owner.WriteNativeChannelVolume(4, (byte)child.SubId);
                _runtimeState.SetWramByte(0xc08b, (byte)(int)child.Position.Y);
                _runtimeState.SetWramByte(0xc08d, (byte)(int)child.Position.X);
                _runtimeState.SetWramByte(0xc08f, 0);
            },
            owner.TryCreatePuzzlePuff,() => owner.PartSlotAvailable,owner.WriteSmogInteractionCounter,
            (packed,tile) =>
            {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new NotSupportedException("smog.s setTile requires the active room's native changed-tile queue.");
                // Large-cloud initialization ignores setTile's failure result.
                if (rooms.TrySetTile((byte)packed,(byte)tile)) owner.OnRoomTileChanged();
            },
            owner.DisableLinkCollisionsAndMenu,() => owner.OnRoomMusicRequested(group,room.Id),
            initializeBossRoom ?? owner.InitializeActiveSmogBossRoom,entry,() => owner.FrameCounter,
            (position, subid) =>
            {
                // Failed getFreePartSlot leaves HL=$e0c0, an echo of the main
                // stack. Smog runs on the separate gameplay-thread stack and
                // still returns; no new PART or related-object link is created.
                if (subid != 0)
                    _runtimeState.SetWramByte(0xc0c2, (byte)(_runtimeState.ReadWramByte(0xc0c2) + 1));
                _runtimeState.SetWramByte(0xc0cb, (byte)(int)position.Y);
                _runtimeState.SetWramByte(0xc0cd, (byte)(int)position.X);
                _runtimeState.SetWramByte(0xc0cf, 0); // Smog's native Z high byte.
            });
        return new SmogRoomEntity(actor,Resources.SmogCollisions,owner.OnSoundRequested,environment);
    }

    internal void ApplyFailedSmogControllerAllocation(SmogEnemySpawn spawn, bool merged)
    {
        // INTERAC$33 keeps writing through HL=$e080 after allocation fails.
        // ID/subid/var03 alias channel volumes3/5/6; other fields are unused WRAM.
        owner.WriteNativeChannelVolume(3, 0x7c);
        owner.WriteNativeChannelVolume(5, (byte)spawn.SubId);
        owner.WriteNativeChannelVolume(6, (byte)spawn.Phase);
        if (merged) _runtimeState.SetWramByte(0xc087, 5);
        _runtimeState.SetWramByte(0xc088, (byte)spawn.Direction);
        _runtimeState.SetWramByte(0xc08b, (byte)(int)spawn.Position.Y);
        _runtimeState.SetWramByte(0xc08d, (byte)(int)spawn.Position.X);
    }

    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn switch
    {
        SmogEnemySpawn smog => CreateSmogEnemy(smog,room),
        DungeonSwitchSpawn switchPart => CreateDungeonSwitch(switchPart.Record,room),
        TimedSparkleSpawn sparkle => new TimedSparkleRoomEntity(sparkle),
        BridgeSpawnerSpawn bridge => new BridgeSpawnerRoomEntity(bridge, room,
            Resources.DungeonMechanics, owner.OnRoomTileChanged, owner.OnSoundRequested,(position,tile) => {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("PART$0c setTile requires its active room session.");
                return rooms.TrySetTile(position,tile);
            }),
        OctorokRockSpawn rock => CreateRock(rock, room),
        VolcanoHandlerSpawn volcano => CreateVolcanoController(volcano.Placement, room),
        VolcanoRockSpawn rock => new VolcanoRockRoomEntity(new VolcanoRock(
            rock.Position, Resources.Volcano, room, _random, owner.OnSoundRequested, owner.ToScreen)),
        ZoraFireSpawn fire => new ZoraFireRoomEntity(new ZoraFireProjectile(
            fire, Resources.ZoraFire, owner.ToScreen) { Name = "ZoraFire", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex }),
        MaskedMoblinSpawn moblin => CreateMaskedMoblin(moblin, room),
        GhiniSpawn ghini => CreateGhini(ghini, room),
        ArmosSpawn armos => CreateArmos(armos, room),
        FlyingTileSpawn tile => CreateFlyingTile(tile, room),
        ArmosSpawnerSpawn spawner => new ArmosSpawnerRoomEntity(
            room, spawner.SourceTile, spawner.ReplacementTile, _random, owner.CanAllocateEnemies),
        MoonlitGrottoOrbSpawn orb => new DungeonOrbRoomEntity(
            orb.Group,
            orb.Room,
            Resources.DungeonMechanics.MoonlitOrbPosition,
            Resources.DungeonMechanics.MoonlitOrbMask,
            Resources.DungeonMechanics,
            Resources.DungeonVisuals.Visual("grotto-orb"),
            room,
            _runtimeState,
            _animationTick,
            owner.OnSoundRequested),
        EnemyClearChestSpawn chest => new EnemyClearChestRoomEntity(
            chest.Group,
            chest.Room,
            chest.PackedPosition,
            room,
            Resources.DungeonInteractions,
            () => owner.RoomEnemyCount,
            owner.OnSoundRequested,
            owner.OnRoomTileChanged,
            _animationTick),
        EnemySmallKeyRewardSpawn key => CreateEnemySmallKeyReward(key.Request),
        EnemyArrowSpawn arrow => CreateEnemyArrow(arrow, room),
        StalfosBoneSpawn bone => CreateStalfosBone(bone, room),
        BurningEnemySpawn burn => CreateBurningEnemy(burn),
        KeeseFireSpawn fire => CreateKeeseFire(fire),
        EnemySwordSpawn sword => new EnemySwordRoomEntity(sword.Parent, sword.SoundRequested, sword.ParentCollisionAllowed),
        SpikedBallSpawn ball => new SpikedBallRoomEntity(ball.Part, ball.Sound),
        FountainFairyHeartSpawn heart => heart.Owner.CreateHeart(),
        FountainFairyPuffSpawn puff => new FixedEffectRoomEntityAdapter<PuzzlePuffEffect>(puff.Effect),
        MoblinBoomerangSpawn boomerang => CreateMoblinBoomerang(boomerang, room),
        GelSpawn gel => EnemyFactory.CreateGel(gel, room),
        BariChildSpawn bari => EnemyFactory.CreateBariChild(bari, room),
        CuccoAttackerSpawn attacker => CreateCuccoAttacker(attacker),
        BabyCuccoReplacementSpawn babyCucco =>
            CreateBabyCuccoReplacement(babyCucco, room),
        BlueEnergyBeadSpawn bead => new BlueEnergyBeadRoomEntity(bead.Index, bead.Center, bead.Duration,
            Resources.DungeonVisuals.Visual("energy-bead"), _random, _runtimeState),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        BossDeathExplosionSpawn explosion => CreateBossDeathExplosion(explosion),
        ArmosWarriorChildSpawn child => CreateArmosChild(child),
        DefeatedMoblinActorSpawn actor => new DefeatedMoblinActorRoomEntity(NpcCharacter.CreateFromRecord(actor.Record)),
        KingMoblinMinionSpawn child => CreateKingMoblinMinion(child),
        KingMoblinBombSpawn bomb => CreateKingMoblinBomb(bomb),
        KingMoblinExplosionSpawn explosion => CreateInteractionExplosion(new InteractionExplosionSpawn(explosion.Position,0,Resources.Patch.ExplosionVisual,Var03:0)),
        ExclamationMarkSpawn exclamation => CreateExclamationMark(exclamation),
        EyesoarChildSpawn child => new EyesoarRoomEntity(child.Spawner.CreateChild(child.Index)),
        MoldormChildSpawn child => child.Spawner.CreateChild(child.SubId),
        EyesoarSpawnEffectSpawn effect => new FixedEffectRoomEntityAdapter<EyesoarSpawnEffect>(effect.Effect),
        BossShadowSpawn shadow => CreateBossShadow(shadow),
        SubterrorDirtSpawn dirt => CreateSubterrorDirt(dirt),
        ShadowHagShadowSpawn shadow => CreateShadowHagShadow(shadow),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        ItemDropSpawn drop => CreateItemDrop(drop, room),
        BoomerangSpawn boomerang => new BoomerangRoomEntity(new BoomerangItem(room, boomerang.Position,
            boomerang.Angle, boomerang.ZHigh, () => RingEffects.BoomerangDamage(BoomerangDatabase.Shared.Damage,
                _inventory ?? throw new InvalidOperationException("ITEM_BOOMERANG $06 requires the live inventory ring owner.")),
            owner.OnSoundRequested, owner.TryCreateBoomerangClink) { Name = "Boomerang", ZIndex = ObjectDrawPriority.BehindLinkZIndex }),
        HeadThwompBombDropSpawn drop =>
            CreateHeadThwompBombDrop(drop, room),
        ShovelDebrisSpawn debris => CreateShovelDebris(debris),
        GrassDebrisSpawn debris => CreateGrassDebris(debris),
        RockDebrisSpawn debris => CreateRockDebris(debris),
        EmberSeedSpawn seed => CreateEmberSeed(seed, room),
        BombSpawn bomb => CreateBomb(bomb, room),
        BombchuSpawn bombchu => CreateBombchu(bombchu, room),
        OwlStatueSparkleSpawn sparkle => CreateInteractionSparkle(sparkle.Position, 0, sparkle.Visual),
        PuzzlePuffSpawn puff => CreatePuzzlePuff(puff),
        TingleKoolooSparkleSpawn sparkle =>
            CreateInteractionSparkle(sparkle.Position, sparkle.SourceAngle, sparkle.Visual),
        GoronCaveNpcSpawn goron => new GoronCaveRoomEntity(NpcCharacter.CreateFromRecord(goron.Record), goron.Interactive),
        GoronBombSpawn bomb => new GoronBombRoomEntity(NpcCharacter.CreateFromRecord(bomb.Owner.Database.BombRecord),bomb.Owner,bomb.SubId),
        TargetCartDebrisSpawn debris => new TargetCartDebrisRoomEntity(NpcCharacter.CreateFromRecord(debris.Record),debris.Position,debris.Direction),
        GoronMinecartSpawn cart => CreateMinecart(cart.Cart,room),
        InteractionExplosionSpawn explosion =>
            CreateInteractionExplosion(explosion),
        WildTokayMeatSpawn => CreateWildTokayMeat(),
        TokayEntranceEyeSpawn eye =>
            new TokayEntranceEyeRoomEntity(eye.Record),
        TokaySeedlingDecorationSpawn seedling =>
            new TokaySeedlingDecorationRoomEntity(seedling.Record),
        EnemySplashSpawn splash => CreateEnemySplash(splash),
        SideScrollBubbleSpawn bubble => new SideScrollBubbleRoomEntity(bubble.Position, room, _random),
        FallingDownHoleSpawn fall => CreateFallingDownHole(fall),
        DungeonKeyUseSpawn key => CreateDungeonKeyUse(key),
        OverworldKeyUseSpawn key => CreateOverworldKeyUse(key),
        EraInfoSpawn era => CreateEraInfo(era),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        GroundTreasureSpawn treasure => CreateGroundTreasure(treasure.Record),
        GroundTreasureGrantSpawn treasure =>
            CreateGroundTreasure(treasure.Request.Resolve(_treasures)),
        MapleDroppedItemSpawn item => CreateMapleDroppedItem(item, room),
        LightableTorchSpawn torch => CreateLightableTorch(torch, room),
        RespawnableBushSpawn bush => new RespawnableBushRoomEntity(
            bush.PackedPosition,
            bush.DropSubId,
            room,
            Resources.DungeonMechanics,
            _random,
            _animationTick,
            owner.OnRoomTileChanged),
        Room148DebrisSpawn debris => CreateRoom148Debris(debris),
        ShootingGalleryGameControllerSpawn controller =>
            CreateShootingGalleryController(controller, room),
        ShootingGalleryBallSpawn ball =>
            CreateShootingGalleryBall(ball, room),
        ShootingGalleryTargetDebrisSpawn debris =>
            CreateShootingGalleryTargetDebris(debris),
        SwordBeamSpawn beam => CreateSwordBeam(beam, room),
        FireballShooterChildSpawn shooter => shooter.Parent.CreateChild(shooter.Position, shooter.TimingIndex),
        BeamosBeamSpawn beam => new BeamosBeamRoomEntity(new BeamosBeamPart(beam, Resources.BeamosBeam, room)),
        SmogProjectileSpawn smog => CreateSmogProjectile(smog, room),
        OctogonPartSpawn shot => new OctogonPartRoomEntity(new OctogonPart(shot,room,_runtimeState,_random,
            slot => owner.ResolveEnemySlot(slot)?.Node.Visible == true,owner.TryCreatePart,owner.OnSoundRequested),owner.OnSoundRequested),
        VireProjectileSpawn shot => new VireProjectileRoomEntity(new VireProjectile(enemies.VireProjectile,
            room, _runtimeState, shot, owner.ReadNativeEnemyHealth, owner.TryCreateVireProjectile, owner.CanAllocateParts,
            owner.TryCreateTransformationPuff, owner.InteractionAnimationParameter, owner.TryCreatePuzzlePuff), owner.OnSoundRequested),
        WizzrobeProjectileSpawn shot => new WizzrobeProjectileRoomEntity(
            new WizzrobeProjectile(enemies.WizzrobeProjectile, room, _runtimeState,
                shot.Position, shot.Angle, shot.ZHigh), () => -owner.ToScreen(Vector2.Zero), owner.OnSoundRequested),
        SwordBeamClinkSpawn clink => CreateSwordBeamClink(clink),
        SwordWallClinkSpawn clink => CreateSwordWallClink(clink),
        EnemyClinkSpawn clink => CreateEnemyClink(clink),
        StatueEyeballSpawn eye => CreateStatueEyeball(eye),
        MovingPlatformSpawn platform =>
            CreateMovingPlatform(platform),
        MinibossPortalSpawn => CreateMinibossPortal(room),
        GiantGhiniChildSpawn child => CreateGiantGhiniChild(child, room),
        ShadowHagBugSpawn bug => CreateShadowHagBug(bug),
        PumpkinHeadProjectileSpawn projectile =>
            CreatePumpkinHeadProjectile(projectile, room),
        HeadThwompProjectileSpawn projectile =>
            CreateHeadThwompProjectile(projectile, room),
        HeadThwompBoulderSpawn => CreateHeadThwompBoulder(room),
        MinecartShutterOpenSpawn shutter => CreateMinecartShutter(
            shutter.PackedPosition,
            shutter.ClosedTile,
            oneShotOpener: true,
            room),
        RickyCompanionSpawn ricky => CreateRicky(ricky, room),
        RickyPunchAttackSpawn punch => CreateRickyPunch(punch, room),
        RickyTornadoSpawn tornado => CreateRickyTornado(tornado, room),
        RickyTileBreakSpawn tileBreak => CreateRickyTileBreak(tileBreak, room),
        MooshCompanionSpawn moosh => CreateMoosh(moosh, room),
        DimitriCompanionSpawn dimitri => CreateDimitri(dimitri, room),
        TokayRescueEmberSpawn ember => new TokayRescueEmberRoomEntity(ember),
        TokayAttachedVisualSpawn accessory => new TokayAttachedVisualRoomEntity(
            accessory.Parent, accessory.Record, accessory.Offset),
        DimitriMouthSpawn mouth => new DimitriMouthRoomEntity(mouth,
            CreateCompanionTileBreaker(mouth.Group, mouth.Room, room)),
        MooshHoverExclamationSpawn exclamation =>
            CreateMooshHoverExclamation(exclamation),
        MooshStompAttackSpawn stomp => CreateMooshStomp(stomp, room),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

    private RickyCompanionRoomEntity CreateRicky(
        RickyCompanionSpawn spawn,
        OracleRoomData room) => new(
            spawn,
            room,
            Resources.Ricky,
            _saveData,
            _runtimeState,
            _random,
            Resources.LedgeJumps,
            owner.OnSoundRequested,
            owner.OnRoomEntityDialogueRequested,
            owner.IsTextActive);

    private RickyPunchAttackRoomEntity CreateRickyPunch(
        RickyPunchAttackSpawn spawn,
        OracleRoomData room) => new(
            spawn,
            Resources.Ricky.Behavior,
            CreateCompanionTileBreaker(spawn.Group, spawn.Room, room));

    private RickyTornadoRoomEntity CreateRickyTornado(
        RickyTornadoSpawn spawn,
        OracleRoomData room) => new(
            spawn,
            Resources.Ricky.Behavior,
            room,
            CreateCompanionTileBreaker(spawn.Group, spawn.Room, room));

    private RickyTileBreakRoomEntity CreateRickyTileBreak(
        RickyTileBreakSpawn spawn,
        OracleRoomData room) => new(
            spawn,
            CreateCompanionTileBreaker(spawn.Group, spawn.Room, room));

    private CompanionAttackTileBreaker CreateCompanionTileBreaker(
        int group,
        int roomId,
        OracleRoomData room)
    {
        int? LinkedNeighbor(Vector2I direction)
        {
            if (rooms is not null && rooms.TryGetNeighbor(
                    group, roomId, direction, out int neighbor))
            {
                return neighbor;
            }
            return null;
        }

        return new CompanionAttackTileBreaker(
            group,
            room,
            Resources.Breakables,
            _saveData,
            LinkedNeighbor,
            owner.OnRoomTileChanged,
            _animationTick,
            owner.OnSoundRequested,
            drop => _itemDrops.DecideBreakableDrop(
                drop, _random, _inventory, _saveData));
    }

    private DimitriCompanionRoomEntity CreateDimitri(DimitriCompanionSpawn spawn, OracleRoomData room) =>
        new(spawn, room, Resources.Dimitri, _saveData ?? throw new InvalidOperationException("Dimitri requires live save state."),
            _runtimeState, owner.OnSoundRequested, owner.OnRoomEntityDialogueRequested, owner.IsTextActive,
            destination => CreateCompanionTileBreaker(spawn.Group, destination.Id, destination));

    private MooshCompanionRoomEntity CreateMoosh(
        MooshCompanionSpawn spawn,
        OracleRoomData room) => new(
            spawn,
            room,
            Resources.Moosh,
            _saveData,
            _runtimeState,
            owner.OnSoundRequested,
            owner.OnRoomEntityDialogueRequested,
            owner.IsTextActive,
            owner.SetVerticalScreenShake,
            destination => CreateCompanionTileBreaker(spawn.Group, destination.Id, destination));

    private MooshHoverExclamationRoomEntity CreateMooshHoverExclamation(
        MooshHoverExclamationSpawn spawn)
    {
        MooshCompanionVisualRecord visual = Resources.Moosh.Visual;
        Vector2 position = spawn.Position + new Vector2(
            0,
            (spawn.ZFixed >> 8) + visual.WaterExclamationZOffset);
        NpcRecord record = Resources.Moosh.CreateExclamationRecord(
            Mathf.RoundToInt(position.Y),
            Mathf.RoundToInt(position.X));
        var npc = new NpcCharacter
        {
            Name = "MooshHoverExclamation",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        npc.Initialize(record);
        // mooshState8Substate1 allocates INTERAC_EXCLAMATION_MARK directly;
        // it does not call the sound-producing objectCreateExclamationMark.
        return new MooshHoverExclamationRoomEntity(
            npc, visual.WaterHoverFrames);
    }

    private MooshStompAttackRoomEntity CreateMooshStomp(
        MooshStompAttackSpawn spawn,
        OracleRoomData room)
    {
        int? LinkedNeighbor(Vector2I direction)
        {
            if (rooms is not null && rooms.TryGetNeighbor(
                    spawn.Group, spawn.Room, direction, out int neighbor))
            {
                return neighbor;
            }
            return null;
        }

        return new MooshStompAttackRoomEntity(
            spawn,
            room,
            Resources.Breakables,
            _saveData,
            LinkedNeighbor,
            owner.OnRoomTileChanged,
            _animationTick,
            owner.OnSoundRequested,
            drop => _itemDrops.DecideBreakableDrop(
                drop, _random, _inventory, _saveData));
    }

    private IRoomEntity CreateSeedOnTree(
        SeedTreeController controller,
        SeedTreeTypeRecord type,
        Vector2 position,
        int index)
    {
        var seed = new SeedOnTree
        {
            Name = $"SeedOnTree_{index}",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        seed.Initialize(
            Resources.SeedTrees,
            controller,
            type,
            position,
            index,
            _inventory,
            owner.OnSeedTreeMessageRequested,
            owner.IsTextActive,
            owner.OnSoundRequested);
        return new SeedOnTreeRoomEntity(seed);
    }

    private IEnumerable<IRoomEntity> CreateSeedTreeEntities(
        SeedTreePlacementRecord record,
        OracleRoomData room)
    {
        var tree = new SeedTreeController
        {
            Name = $"SeedTree_{record.RefillIndex:x2}"
        };
        tree.Initialize(Resources.SeedTrees, record, room, _runtimeState);
        if (!tree.HasActiveSeeds)
        {
            tree.Free();
            yield break;
        }

        yield return new SeedTreeControllerRoomEntity(tree);
        SeedTreeTypeRecord type = Resources.SeedTrees.Type(record.SeedType);
        for (int index = 0; index < Resources.SeedTrees.SeedCount; index++)
        {
            yield return CreateSeedOnTree(
                tree,
                type,
                tree.SeedPosition(index),
                index);
        }
    }

    private IRoomEntity CreateMovingPlatform(
        MovingPlatformSpawn spawn) =>
        CreateMovingPlatform(spawn.Position, spawn.SubId, rooms?.CurrentDungeonIndex ?? -1);

    private IRoomEntity CreateMovingPlatform(Vector2 position, int subid, int dungeon) =>
        new MovingPlatformRoomEntity(
            Resources.DungeonVisuals.Visual($"platform-{subid & 7:x2}"), position, subid,
            Resources.DungeonInteractions.MovingPlatformCollisionRadii(subid), Resources.DungeonInteractions,
            Resources.MovingPlatforms.Script(dungeon, subid >> 3), _platformRiding, owner.ReadPlayingInstrument);

    private IRoomEntity CreateGiantGhiniChild(
        GiantGhiniChildSpawn spawn,
        OracleRoomData room)
    {
        var child = new GiantGhiniChild
        {
            Name = $"GiantGhiniChild_{spawn.Index}",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        child.Initialize(
            Resources.DungeonBosses.Enemy(EnemyId.GiantGhiniChild), spawn.Owner, room, spawn.Index);
        return new GiantGhiniChildRoomEntity(
            child, owner.OnSoundRequested);
    }

    private IRoomEntity CreateShadowHagBug(ShadowHagBugSpawn spawn)
    {
        var bug = new ShadowHagBug();
        bug.Initialize(
            Resources.DungeonBosses.Enemy(EnemyId.ShadowHagBug),
            spawn.Owner,
            _random,
            spawn.Position);
        return new ShadowHagBugRoomEntity(bug, owner.OnSoundRequested);
    }

    private IRoomEntity CreateShadowHagShadow(ShadowHagShadowSpawn spawn)
    {
        var shadow = new ShadowHagShadowEffect
        {
            Name = $"ShadowHagShadow_{spawn.AngleIndex}"
        };
        shadow.Initialize(
            spawn.Owner,
            spawn.AngleIndex,
            Resources.DungeonVisuals.Visual("shadow-hag-shadow"));
        return new ShadowHagShadowRoomEntity(shadow);
    }

    private IRoomEntity CreatePumpkinHeadProjectile(
        PumpkinHeadProjectileSpawn spawn,
        OracleRoomData room) =>
        new PumpkinHeadProjectileRoomEntity(
            new PumpkinHeadProjectile(
                Resources.DungeonVisuals.Visual("pumpkin-projectile"),
                room,
                spawn.Position,
                spawn.Angle));

    private IRoomEntity CreateHeadThwompProjectile(
        HeadThwompProjectileSpawn spawn,
        OracleRoomData room)
    {
        DungeonInteractionVisual visual = Resources.DungeonVisuals.Visual(
            spawn.Kind == HeadThwompProjectileKind.Fireball
                ? "head-thwomp-fireball"
                : "head-thwomp-circular-projectile");
        DungeonInteractionVisual? impactVisual =
            spawn.Kind == HeadThwompProjectileKind.Fireball
                ? Resources.DungeonVisuals.Visual("head-thwomp-fireball-impact")
                : null;
        return new HostileProjectileRoomEntity<HeadThwompProjectile>(
            new HeadThwompProjectile(
                spawn, visual, impactVisual, room, _random, owner.OnSoundRequested));
    }

    private VolcanoRoomEntity CreateVolcanoController(VolcanoPlacement record, OracleRoomData room) =>
        new(record, Resources.Volcano, room, _saveData, _random, owner.OnSoundRequested, owner.SetScreenShake,
            () => owner.ScreenIsShaking, owner.SetScreenShakeMagnitude, () => owner.PartSlotAvailable);

    private IRoomEntity CreateHeadThwompBoulder(OracleRoomData room) =>
        new HostileProjectileRoomEntity<HeadThwompBoulder>(
            new HeadThwompBoulder(
                room,
                _random,
                Resources.DungeonVisuals.Visual("head-thwomp-boulder"),
                Resources.DungeonVisuals.Visual("head-thwomp-boulder-impact"),
                owner.OnSoundRequested));

    private IRoomEntity CreateMinibossPortal(OracleRoomData room)
    {
        foreach (PlacementRecord record in
            Resources.DungeonEntrances.GetRoomRecords(room.Group, room.Id))
        {
            if (record.Kind ==
                DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal)
            {
                return CreateSharedDungeonInteraction(
                    record, room, EnemyPlacementContext.Unrestricted);
            }
        }
        throw new InvalidOperationException(
            $"Room ${room.Group:x1}:${room.Id:x2} has no imported miniboss portal placement.");
    }

    private StatueEyeballRoomEntity CreateStatueEyeball(StatueEyeballSpawn spawn)
    {
        var eye = new StatueEyeball();
        eye.Initialize(spawn.Position, Resources.DungeonEntrances);
        return new StatueEyeballRoomEntity(eye);
    }

    private MoblinBoomerangRoomEntity CreateMoblinBoomerang(
        MoblinBoomerangSpawn spawn,
        OracleRoomData room)
    {
        var boomerang = new MoblinBoomerangProjectile(
            spawn.Owner,
            room,
            spawn.Position,
            spawn.Angle,
            enemies.MoblinBoomerang)
        {
            Name = "MoblinBoomerang",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        return new MoblinBoomerangRoomEntity(boomerang);
    }

    private WildTokayMeatRoomEntity CreateWildTokayMeat()
    {
        var meat = new WildTokayMeat
        {
            Name = "WildTokayMeat",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        meat.Initialize(
            Resources.WildTokayMeat,
            rooms?.CurrentRoom ?? throw new InvalidOperationException(
                "Wild Tokay meat requires an active room."),
            Resources.Bomb.Data,
            owner.OnSoundRequested);
        return new WildTokayMeatRoomEntity(meat);
    }

    private IRoomEntity CreateSpecializedNpc(
        NpcRecord record,
        OracleRoomData room)
    {
        RequireNpcImplementation(
            record, NpcImplementationClassification.SpecializedNative);

        if (record is { Id: InteractionId.Miscellaneous1, SubId: 0x13 or 0x14 })
            return new GoronBombStatueRoomEntity(
                NpcCharacter.CreateFromRecord(record), room, _animationTick);

        if (record.Id == InteractionId.KnowItAllBird)
        {
            var bird = new KnowItAllBirdCharacter { Name = $"Npc_e3_{record.SubId:x2}" };
            bird.InitializeBird(record, Resources.KnowItAllBirds, _random, owner.IsTextActive);
            return new KnowItAllBirdRoomEntity(bird);
        }

        if (record is { Id: InteractionId.ForestFairy, SubId: >= 0x0e and <= 0x10 })
            return new ForestHintFairyRoomEntity(NpcCharacter.CreateFromRecord(record));

        if (record is { Group: 1, Room: 0xba, Id: InteractionId.Pirate, SubId: 0x04 })
        {
            if (_saveData is null || _inventory is null)
            {
                throw new InvalidOperationException(
                    "The Tokay Eyeball socket requires live save and inventory state.");
            }
            return new TokayEyeballSlotRoomEntity(
                record,
                Resources.TokayEntranceEyes,
                room,
                _saveData,
                _inventory,
                owner.OnRoomEntityDialogueRequested,
                owner.OnSoundRequested,
                owner.BeginScreenShake,
                owner.OnRoomMusicRequested,
                owner.OnRoomTileChanged,
                _animationTick);
        }

        if (Resources.Tingle.Matches(record))
        {
            if (_inventory is null)
                throw new InvalidOperationException("Tingle requires live inventory state.");
            return new TingleRoomEntity(record, Resources.Tingle, _inventory);
        }

        if (record is { Id: InteractionId.Tokay, SubId: >= 0x06 and <= 0x0a })
        {
            if (_saveData is null)
            {
                throw new InvalidOperationException(
                    "Tokay item holders require live room-flag state.");
            }
            TokayHeldItemRecord item = Resources.TokayInteractions.HeldItem(record.SubId);
            var holder = new TokayHoldingItemCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            holder.InitializeHoldingItem(
                record,
                item,
                _saveData.HasRoomFlag(
                    record.Group, record.Room, OracleSaveData.RoomFlag40));
            ConfigureTokayActor(holder);
            bool returned = _saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag40);
            holder.ReturnedItemDialogue = returned && _inventory is not null &&
                new TokayTheftEventDatabase().Record.StolenItems
                    .Where(treasure => treasure != TreasureId.Shield)
                    .All(_inventory.HasTreasure) ? 0x0a0d : 0x0a0c;
            if (returned)
            {
                holder.SetFacingDirection(Vector2I.Down);
                holder.NativeAnimation = TokayAnimationMode.FaceLink;
            }
            return new TokayNpcRoomEntity(holder);
        }

        if (record.Id == InteractionId.Tokay)
        {
            var tokay = new TokayCharacter { Name = $"Npc_48_{record.SubId:x2}" };
            tokay.Initialize(record);
            ConfigureTokayActor(tokay);
            if (record.SubId == 0x0b && tokay.Active)
                _runtimeState.SetWramByte(WramAddress.wDiggingUpEnemiesForbidden, 1);
            if (record.SubId == 0x1e)
            {
                room.SetPositionTileAndCollision(tokay.Position, 0, 0x0f,
                    _animationTick(), preserveRenderedTile: true);
                owner.OnRoomTileChanged();
            }
            tokay.NativeAnimation = record.SubId switch
            {
                0x05 => TokayAnimationMode.FaceLink,
                >= 0x1a and <= 0x1c => TokayAnimationMode.Still,
                _ => TokayAnimationMode.Animate
            };
            if (record.SubId is 0x0f or 0x10)
                tokay.SetFacingDirection(record.SubId == 0x0f ? Vector2I.Right : Vector2I.Up);
            if (record.SubId == 0x1a)
            {
                tokay.SetBasePalette(2);
                tokay.ForceNextAnimationFrame();
            }
            if (record.SubId == 0x1c)
            {
                tokay.SetScriptAnimation(Resources.TokayInteractions.Animation(9));
                tokay.Accessory = new TokayAttachedVisualRoomEntity(tokay,
                    Resources.TokayNative.Visual("museum-meat", record.Group, record.Room), new Vector2(0, -12));
            }
            if (record.SubId == 0x1d && _saveData is not null && _inventory is not null)
            {
                bool returned = _saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag40);
                tokay.SetScriptAnimation(Resources.TokayInteractions.Animation(returned ? 2 : 6));
                if (!returned)
                    tokay.Accessory = new TokayAttachedVisualRoomEntity(tokay,
                        Resources.TokayNative.Visual(_inventory.ShieldLevel < 2 ? "shield1" : "shield2", record.Group, record.Room),
                        new Vector2(0, -12));
            }
            return new TokayNpcRoomEntity(tokay);
        }

        if (record is { Group: 1, Room: 0xcb, Id: InteractionId.Rosa, SubId: 0x00 })
        {
            NpcCharacter tokayActor = NpcCharacter.CreateFromRecord(record);
            ConfigureTokayActor(tokayActor);
            TokayAttachedVisualRoomEntity? shovel = null;
            if (tokayActor.Active && _saveData is not null &&
                !_saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag40))
                shovel = new TokayAttachedVisualRoomEntity(tokayActor,
                    Resources.TokayNative.Visual("rosa-shovel", record.Group, record.Room), new Vector2(0x48, 0x38))
                    { FollowParent = false, ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
            return new RosaNpcRoomEntity(tokayActor, _inventory!, shovel);
        }

        if (record.Group == Resources.ShootingGallery.Record.Group &&
            record.Room == Resources.ShootingGallery.Record.Room &&
            record.Id == Resources.ShootingGallery.Record.InteractionId &&
            record.SubId == Resources.ShootingGallery.Record.SubId)
        {
            var keeper = new ShootingGalleryCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            keeper.InitializeShootingGallery(record);
            return new ShootingGalleryNpcRoomEntity(keeper);
        }
        if (record.Group == Resources.Comedian.Record.Group &&
            record.Room == Resources.Comedian.Record.Room &&
            record.Id == Resources.Comedian.Record.InteractionId &&
            record.SubId == Resources.Comedian.Record.SubId)
        {
            var comedian = new ComedianCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            comedian.InitializeComedian(record, Resources.Comedian.Record);
            return new ComedianRoomEntity(comedian);
        }
        if (record.Id == InteractionId.Tokkey && record.SubId == 0)
            return new TokkeyRoomEntity(NpcCharacter.CreateFromRecord(record));
        if (record.Group == Resources.DumbbellMan.Record.Group &&
            record.Room == Resources.DumbbellMan.Record.Room &&
            record.Id == Resources.DumbbellMan.Record.InteractionId &&
            record.SubId == Resources.DumbbellMan.Record.SubId)
        {
            var man = new DumbbellManCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            man.InitializeDumbbellMan(record, Resources.DumbbellMan.Record);
            return new DumbbellManRoomEntity(man);
        }
        if (record.Id == InteractionId.OldZora && record.SubId == 0)
        {
            var zora = new OldZoraCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            zora.InitializeOldZora(record);
            return new OldZoraRoomEntity(zora);
        }
        if (record.Group == Resources.MaskSalesman.Record.Group &&
            record.Room == Resources.MaskSalesman.Record.Room &&
            record.Id == Resources.MaskSalesman.Record.InteractionId &&
            record.SubId == Resources.MaskSalesman.Record.SubId)
        {
            var salesman = new MaskSalesmanCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            salesman.InitializeMaskSalesman(record, Resources.MaskSalesman.Record);
            return new MaskSalesmanRoomEntity(salesman);
        }
        if (Resources.Cheval.Matches(record))
        {
            var cheval = new ChevalCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            cheval.InitializeCheval(record, Resources.Cheval.Record);
            return new ChevalRoomEntity(cheval, Resources.Cheval.Record);
        }
        if (Resources.RalphAfterCheval.Matches(record))
        {
            var ralph = new RalphAfterChevalCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            ralph.InitializeRalph(record, Resources.RalphAfterCheval.Record);
            return new RalphAfterChevalRoomEntity(ralph);
        }
        if (Resources.RalphAfterRafton.Matches(record))
        {
            var ralph = new RalphAfterRaftonCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            ralph.InitializeRalph(record, Resources.RalphAfterRafton.Record);
            return new RalphAfterRaftonRoomEntity(ralph);
        }
        if (Resources.Rafton.Matches(record))
        {
            var rafton = new RaftonCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            rafton.InitializeRafton(record, Resources.Rafton.Record);
            return new RaftonRoomEntity(rafton);
        }
        if (record.Group == Resources.DepressedBoy.Record.Group &&
            record.Room == Resources.DepressedBoy.Record.Room &&
            record.Id == Resources.DepressedBoy.Record.InteractionId &&
            record.SubId == Resources.DepressedBoy.Record.SubId)
        {
            var boy = new DepressedBoyCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            boy.InitializeDepressedBoy(record);
            return new DepressedBoyRoomEntity(boy);
        }

        bool isOverworldPoe =
            record.Group == Resources.Poe.Record.Group &&
            record.Room == Resources.Poe.Record.Room &&
            record.Var03 is 0x00 or 0x02;
        bool isTombPoe =
            record.Group == Resources.Poe.Record.TombGroup &&
            record.Room == Resources.Poe.Record.TombRoom &&
            record.Var03 == 0x01;
        if ((isOverworldPoe || isTombPoe) &&
            record.Id == Resources.Poe.Record.InteractionId &&
            record.SubId == Resources.Poe.Record.SubId)
        {
            var poe = new PoeCharacter
            {
                Name =
                    $"Npc_{record.Id:x2}_{record.SubId:x2}_{record.Var03:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            poe.InitializePoe(record, Resources.Poe.Record);
            return new PoeRoomEntity(poe, Resources.Poe.Record, _saveData);
        }
        if (record is
            {
                Group: 2,
                Room: 0x2f,
                Id: InteractionId.Postman,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            var postman = new PostmanCharacter
            {
                Name = "Npc_55_00",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            postman.InitializePostman(record);
            return new PostmanRoomEntity(postman);
        }
        if (record is
            {
                Group: 2,
                Room: 0x3e,
                Id: InteractionId.ToiletHand,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            var toiletHand = new ToiletHandCharacter
            {
                Name = "Npc_5b_00",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            toiletHand.InitializeToiletHand(
                record, Resources.ToiletHand.Record);
            return new ToiletHandRoomEntity(toiletHand);
        }

        NpcCharacter npc = NpcCharacter.CreateFromRecord(record);
        if (record.Id == InteractionId.MamamuYan && record.SubId == 0)
        {
            npc.SetAnimationRate(0);
            npc.SetScriptButtonSensitive(false);
            return new MamamuRoomEntity(npc);
        }
        if (record.Id == InteractionId.MamamuDog && record.SubId == 0)
        {
            if (_saveData is not null)
                npc.SetActive(new NpcVisibilityRuleDatabase().ShouldShow(record, _saveData, _runtimeState));
            return new MamamuDogRoomEntity(npc, () => _random.Next().Value, owner.IsTextActive);
        }
        if (record is { Id: InteractionId.OldManWithRupees, SubId: 0x01 })
            return new OldManRupeesRoomEntity(npc);
        if (Resources.StoneRabbit.Matches(record))
        {
            return new StoneRabbitRoomEntity(npc, Resources.StoneRabbit);
        }
        if (Resources.BusinessScrub.Matches(record))
        {
            return new BusinessScrubRoomEntity(
                npc,
                Resources.BusinessScrub,
                room,
                _animationTick(),
                owner.OnRoomTileChanged,
                _inventory?.ShieldLevel ?? 0);
        }
        if (Resources.Room20e.Matches(record))
        {
            return new Room20eNpcRoomEntity(
                npc, Resources.Room20e, _saveData);
        }
        if (Resources.TroyHouse.Matches(record))
        {
            if (_saveData is null)
            {
                throw new InvalidOperationException(
                    $"{NpcSource(record)} requires save data for its " +
                    "specialized Troy interaction.");
            }
            return new TroyHouseRoomEntity(
                npc, Resources.TroyHouse, _saveData, _random);
        }
        if (record is { Id: InteractionId.Bipin, SubId: 0x00 })
            return new RunningBipinRoomEntity(
                npc, _familyState.RunningBipin);
        if (record is { Id: InteractionId.Bipin, SubId: 0x0a })
            return new PastBipinRoomEntity(npc);
        if (record is
            {
                Group: 0,
                Room: 0x83,
                Id: InteractionId.GreatFairy,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            return new GreatFairyRoomEntity(npc, owner.OnSoundRequested);
        }
        if (record is
            {
                Group: 0,
                Room: 0x5d,
                Id: InteractionId.LinkedGameGhini,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            return new SpecializedNpcRoomEntity(npc);
        }
        if (record.Group == 2 &&
            record.Room is 0xea or 0xeb &&
            record.Id is InteractionId.Bipin or InteractionId.Blossom or InteractionId.Child)
        {
            return new SpecializedNpcRoomEntity(npc);
        }

        throw new InvalidOperationException(
            $"{NpcSource(record)} is classified specialized-native but has " +
            "no native room-entity dispatch.");
    }

    private IRoomEntity CreateShootingGalleryController(
        ShootingGalleryGameControllerSpawn spawn,
        OracleRoomData room)
    {
        var controller = new ShootingGalleryGameController
        {
            Name = "ShootingGalleryController"
        };
        controller.Initialize(
            spawn.Session.VariantDatabase ?? Resources.ShootingGallery,
            spawn.Session,
            room,
            _random,
            owner.OnSoundRequested,
            _animationTick);
        return new ShootingGalleryGameControllerRoomEntity(controller);
    }

    private IRoomEntity CreateShootingGalleryBall(
        ShootingGalleryBallSpawn spawn,
        OracleRoomData room)
    {
        var ball = new ShootingGalleryBall
        {
            Name = "ShootingGalleryBall",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        ball.Initialize(
            spawn.Session.VariantDatabase ?? Resources.ShootingGallery,
            spawn.Session,
            room,
            _random,
            spawn.Position,
            owner.OnSoundRequested,
            _animationTick);
        return new ShootingGalleryBallRoomEntity(ball);
    }

    private IRoomEntity CreateShootingGalleryTargetDebris(
        ShootingGalleryTargetDebrisSpawn spawn)
    {
        var debris = new ShootingGalleryTargetDebris
        {
            Name = "ShootingGalleryTargetDebris",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        debris.Initialize(Resources.ShootingGallery.Debris, spawn);
        return new ShootingGalleryTargetDebrisRoomEntity(debris);
    }

    private IEnumerable<IRoomEntity> CreateNayruHouseNpcs(
        OracleRoomData room,
        IReadOnlyList<NpcRecord> roomNpcs)
    {
        List<NpcRecord> impas = new();
        NpcRecord? nayru = null;
        NpcRecord? zelda = null;
        foreach (NpcRecord record in roomNpcs)
        {
            RequireNpcImplementation(
                record, NpcImplementationClassification.SpecializedNative);
            if (!Resources.NayruHouse.Matches(record))
            {
                throw new InvalidOperationException(
                    $"{NpcSource(record)} is not part of room 3:9e's " +
                    "imported native interaction set.");
            }
            if (record is { Id: InteractionId.ImpaNpc, SubId: 0x00 })
                impas.Add(record);
            else if (record is { Id: InteractionId.Nayru, SubId: 0x0b })
                nayru = record;
            else if (record is { Id: InteractionId.Zelda, SubId: 0x07 })
                zelda = record;
        }

        int[] expectedImpaVariants =
            [0x00, 0x01, 0x02, 0x05, 0x09, 0x0a, 0x0b, 0x0d, 0x0e];
        impas.Sort(static (left, right) => left.Var03.CompareTo(right.Var03));
        if (impas.Count != expectedImpaVariants.Length ||
            nayru is null ||
            zelda is null)
        {
            throw new InvalidOperationException(
                "Room 3:9e requires nine Impa states, Nayru $36:$0b, and " +
                "Zelda $ad:$07.");
        }
        for (int index = 0; index < expectedImpaVariants.Length; index++)
        {
            if (impas[index].Var03 != expectedImpaVariants[index])
            {
                throw new InvalidOperationException(
                    $"Room 3:9e Impa variant {index} should be " +
                    $"${expectedImpaVariants[index]:x2}, got " +
                    $"${impas[index].Var03:x2}.");
            }
            yield return CreateNayruHouseNpc(impas[index], room);
        }
        yield return CreateNayruHouseNpc(nayru.Value, room);
        yield return CreateNayruHouseNpc(zelda.Value, room);

        if (_saveData is null)
            yield break;
        IReadOnlyList<RoomTileChangeWatcherDatabaseRecord> watchers =
            Resources.TileChangeWatchers.GetRoomRecords(
                Resources.NayruHouse.Record.Group, Resources.NayruHouse.Record.Room);
        if (watchers is not [{ Order: 3 }])
        {
            throw new InvalidOperationException(
                "Room 3:9e requires one source-order-3 tile-change watcher.");
        }
        yield return new RoomTileChangeWatcherRoomEntity(
            watchers[0], room, _saveData);
    }

    private NayruHouseNpcRoomEntity CreateNayruHouseNpc(
        NpcRecord record,
        OracleRoomData room) => new(
            NpcCharacter.CreateFromRecord(record),
            Resources.NayruHouse,
            room,
            _animationTick);

    private static void RequireNpcImplementation(
        NpcRecord record,
        NpcImplementationClassification expected)
    {
        if (record.Implementation != expected)
        {
            throw new InvalidOperationException(
                $"{NpcSource(record)} must be classified {expected}, got " +
                $"{record.Implementation}.");
        }
    }

    private static InvalidOperationException UnsupportedNpcClassification(
        NpcRecord record) =>
        new(
            $"{NpcSource(record)} has invalid implementation classification " +
            $"{record.Implementation}.");

    private static string NpcSource(NpcRecord record) =>
        $"NPC {record.Group}:{record.Room:x2} " +
        $"${record.Id:x2}:${record.SubId:x2} var03=${record.Var03:x2}";

    private IEnumerable<IRoomEntity> CreateMakuSproutRoomEntities(
        OracleRoomData room,
        IReadOnlyList<NpcRecord> records)
    {
        if (_saveData is null)
        {
            throw new InvalidOperationException(
                "Room 1:38 Maku Sprout interactions require save data.");
        }
        if (records.Count != 1 ||
            !Resources.MakuSproutRoom.MatchesSprout(records[0]))
        {
            throw new InvalidOperationException(
                "Room 1:38 requires exactly one imported placed " +
                "INTERAC_MAKU_SPROUT $88:$00 before its conditional statue.");
        }

        // Preserve group1Map38ObjectData order: the sprout is first, then the
        // conditional $6b:$15 Link statue.
        yield return new MakuSproutRoomEntity(
            NpcCharacter.CreateFromRecord(records[0]),
            Resources.MakuSproutRoom,
            _saveData);
        if (!_saveData.HasGlobalFlag(Resources.MakuSproutRoom.Record.FinishedFlag))
            yield break;

        yield return new MakuLinkStatueRoomEntity(
            NpcCharacter.CreateFromRecord(
                Resources.MakuSproutRoom.Record.CreateStatueNpcRecord()),
            Resources.MakuSproutRoom,
            room,
            _animationTick);
    }

    private IEnumerable<IRoomEntity> CreateRoom148Npcs(
        IReadOnlyList<NpcRecord> records)
    {
        bool foundWorker = false;
        foreach (NpcRecord record in records)
        {
            if (record is { Id: InteractionId.PickaxeWorker, SubId: 0x00 })
            {
                RequireNpcImplementation(
                    record,
                    NpcImplementationClassification.SpecializedNative);
                if (foundWorker)
                    throw new InvalidOperationException(
                        "Room 1:48 contains more than one pickaxe worker $57:$00.");
                foundWorker = true;
                NpcCharacter npc = NpcCharacter.CreateFromRecord(record);
                PickaxeRecord pickaxe = Resources.Room148.Record;
                npc.SetDialogue(
                    pickaxe.TextId, pickaxe.Message, canFace: false);
                npc.SetScriptAnimation(pickaxe.WorkAnimation);
                yield return new Room148PickaxeWorkerRoomEntity(
                    npc, pickaxe, owner.OnSoundRequested);
            }
            else
            {
                RequireNpcImplementation(
                    record,
                    NpcImplementationClassification.OrdinaryGeneric);
                yield return new NpcRoomEntity(
                    NpcCharacter.CreateFromRecord(record));
            }
        }

        if (!foundWorker)
            throw new InvalidOperationException(
                "Room 1:48 is missing interaction $57:$00.");
    }

    private IEnumerable<IRoomEntity> CreateVasuShopNpcs(
        IReadOnlyList<NpcRecord> records)
    {
        if (records.Count != 5)
        {
            throw new InvalidOperationException(
                $"Room 2:ee must contain five Vasu Jewelers actors, got {records.Count}.");
        }

        foreach (NpcRecord record in records)
        {
            RequireNpcImplementation(
                record,
                NpcImplementationClassification.SpecializedNative);
            bool supported = record.Id == InteractionId.Vasu && record.SubId is 0x00 or 0x01 or 0x06 ||
                record.Id == InteractionId.RingHelpBook && record.SubId is 0x00 or 0x01;
            if (!supported)
            {
                throw new InvalidOperationException(
                    $"Unsupported Vasu Jewelers interaction ${record.Id:x2}:${record.SubId:x2}.");
            }
            NpcCharacter npc = NpcCharacter.CreateFromRecord(record);
            yield return new VasuShopNpcRoomEntity(npc, Resources.VasuShop);
        }
    }

    private IEnumerable<IRoomEntity> CreateLynnaShop(
        OracleRoomData room,
        IReadOnlyList<NpcRecord> records)
    {
        LynnaShopDatabase database = room.Id == Resources.HiddenShop.Room ? Resources.HiddenShop : Resources.LynnaShop;
        if (records.Count != 1 || records[0].Id != InteractionId.Shopkeeper ||
            records[0].SubId != database.ShopkeeperSubId)
        {
            throw new InvalidOperationException(
                $"Room 2:{room.Id:x2} must contain shopkeeper $46:${database.ShopkeeperSubId:x2}, got {records.Count} NPC records.");
        }

        // The three $47 placements precede $46:$00 in mainData.s. Stock
        // replacement can delete a placement, but surviving objects retain
        // that source order.
        foreach (StockRecord stock in
            database.ResolveStock(_saveData))
        {
            var item = new LynnaShopItem
            {
                Name = $"ShopItem_{stock.Order}_{stock.Item.SubId:x2}",
                ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
            };
            item.Initialize(stock, room);
            yield return new LynnaShopItemRoomEntity(item);
        }

        NpcRecord record = records[0];
        RequireNpcImplementation(
            record,
            NpcImplementationClassification.SpecializedNative);
        NpcCharacter shopkeeper = NpcCharacter.CreateFromRecord(record);
        yield return new LynnaShopkeeperRoomEntity(shopkeeper, database);

        // The final $71:$0c object is invisible and deletes itself after this
        // one entry-side effect.
        if (!database.Hidden)
            database.ApplyCompanionEntryState(_saveData);
    }

    private IEnumerable<IRoomEntity> CreateTokayShop(
        IReadOnlyList<NpcRecord> records)
    {
        if (_inventory is null || _saveData is null)
        {
            throw new InvalidOperationException(
                "Room 2:e4 Tokay trading hut requires live inventory and save state.");
        }
        if (records.Count != 1 || records[0] is not { Id: InteractionId.Tokay, SubId: 0x0e })
        {
            throw new InvalidOperationException(
                $"Room 2:e4 must contain Tokay shopkeeper $48:$0e, got {records.Count} NPC records.");
        }

        foreach (TokayShopPlacementRecord placement in Resources.TokayShop.Placements)
        {
            int subId;
            int treasure;
            if (placement.PlacedSubId == 0)
            {
                if (_saveData.HasGlobalFlag(Resources.TokayShop.BoughtFeatherFlag))
                    continue;
                bool replace = _inventory.HasTreasure(TreasureId.Feather);
                subId = replace ? 2 : 0;
                treasure = replace
                    ? TreasureId.Shovel
                    : TreasureId.Feather;
            }
            else if (placement.PlacedSubId == 1)
            {
                if (_saveData.HasGlobalFlag(Resources.TokayShop.BoughtBraceletFlag))
                    continue;
                bool replace = _inventory.HasTreasure(TreasureId.Bracelet);
                subId = replace ? 3 : 1;
                treasure = replace
                    ? TreasureId.Shovel
                    : TreasureId.Bracelet;
            }
            else
            {
                if (!_saveData.HasGlobalFlag(Resources.TokayShop.BoughtBraceletFlag))
                    continue;
                subId = Math.Clamp(4 + Math.Max(0, _inventory.ShieldLevel - 1), 4, 6);
                treasure = TreasureId.Shield;
            }

            TokayShopPlacementRecord visual = Resources.TokayShop.Visual(subId) with
            {
                Order = placement.Order,
                Y = placement.Y,
                X = placement.X
            };
            var item = new TokayShopItem
            {
                Name = $"TokayShopItem_{placement.Order}_{subId:x2}",
                ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
            };
            item.Initialize(
                visual, placement.PlacedSubId, subId, treasure,
                Resources.TokayShop.ItemCollisionRadius);
            yield return new TokayShopItemRoomEntity(item);
        }

        NpcRecord shopkeeper = records[0];
        RequireNpcImplementation(
            shopkeeper, NpcImplementationClassification.SpecializedNative);
        var npc = new TokayCharacter { Name = "TokayShopkeeper" };
        npc.Initialize(shopkeeper);
        ConfigureTokayActor(npc);
        yield return new TokayNpcRoomEntity(npc);
    }

    private void ConfigureTokayActor(NpcCharacter npc)
    {
        if (_saveData is null || _inventory is null)
            return;

        NpcRecord record = npc.Record;
        if (record.Id == InteractionId.Rosa)
        {
            bool visible = _saveData.IsLinkedGame && (_inventory.Essences & 0x04) == 0;
            npc.SetActive(visible);
            return;
        }
        if (record.Id != InteractionId.Tokay)
            return;

        if (record.SubId is 0x0f or 0x10)
        {
            byte dimitri = _saveData.ReadWramByte(
                Resources.TokayInteractions.DimitriStateAddress);
            npc.SetActive((dimitri & 0x02) == 0 && (_inventory.Essences & 0x04) != 0);
        }
        else if (record.SubId == 0x0b)
        {
            npc.SetActive(
                _saveData.IsLinkedGame &&
                !_inventory.HasTreasure(TreasureId.Shovel) &&
                !_saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80));
        }
        else if (record.SubId == 0x07 && _saveData.IsLinkedGame)
        {
            npc.SetActive(false);
        }
        else if (Resources.TokaySeedlingPlot.MatchesNpc(record) &&
            _saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80))
        {
            npc.SetStatePosition(npc.Position + new Vector2(
                Resources.TokaySeedlingPlot.Record.PlantedXOffset, 0));
        }

        if (record.SubId is >= 0x06 and <= 0x0a)
        {
            string animation = _saveData.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlag40)
                ? Resources.TokayInteractions.Animation(0x02)
                : Resources.TokayInteractions.Animation(0x06);
            npc.SetScriptAnimation(animation);
        }
    }

    private IEnumerable<IRoomEntity> CreateBlackTowerNpcs(
        OracleRoomData roomData,
        IReadOnlyList<NpcRecord> records,
        EnemyPlacementContext placementContext)
    {
        int room = roomData.Id;
        for (int index = 0; index < records.Count; index++)
        {
            NpcRecord record = records[index];
            RequireNpcImplementation(
                record,
                NpcImplementationClassification.SpecializedNative);
            NpcCharacter npc = NpcCharacter.CreateFromRecord(record);

            IRoomEntity entity = record switch
            {
                { Id: InteractionId.MaleVillager, SubId: 0x02 } =>
                    new BlackTowerBlockingVillagerRoomEntity(npc, Resources.BlackTower),
                { Id: InteractionId.Soldier, SubId: 0x0c } =>
                    new BlackTowerSoldierRoomEntity(npc, Resources.BlackTower, _random),
                { Id: InteractionId.PickaxeWorker, SubId: 0x03 } =>
                    new BlackTowerPickaxeWorkerRoomEntity(
                        npc, Resources.Room148.Record, Resources.BlackTower, _random, owner.OnSoundRequested),
                { Id: InteractionId.HardhatWorker, SubId: 0x00 } =>
                    new BlackTowerShovelWorkerRoomEntity(npc, Resources.BlackTower),
                { Id: InteractionId.HardhatWorker, SubId: 0x03 } =>
                    new BlackTowerPatrollingWorkerRoomEntity(
                        npc, Resources.BlackTower, _random),
                _ => throw new InvalidOperationException(
                    $"Unsupported placed Black Tower interaction " +
                    $"${record.Id:x2}:${record.SubId:x2} in room 4:${room:x2}.")
            };
            yield return entity;

            // INTERAC_DUNGEON_STUFF is the second source object in $e7 but is
            // intentionally absent from the ordinary visible-NPC table.
            if (room == 0xe7 && index == 0)
            {
                PlacementRecord entrance = default;
                bool foundEntrance = false;
                foreach (PlacementRecord candidate in
                    Resources.DungeonEntrances.GetRoomRecords(4, 0xe7))
                {
                    if (candidate.Kind !=
                        DungeonEntranceInteractionDatabaseObjectKind.Entry)
                    {
                        continue;
                    }
                    entrance = candidate;
                    foundEntrance = true;
                    break;
                }
                if (!foundEntrance)
                {
                    throw new InvalidOperationException(
                        "Room 4:e7 is missing INTERAC_DUNGEON_STUFF $12:$00.");
                }
                yield return CreateSharedDungeonInteraction(
                    entrance, roomData, placementContext);
            }
        }
    }

    private IEnumerable<IRoomEntity> CreateRoom149Family(
        IReadOnlyList<NpcRecord> records)
    {
        foreach (NpcRecord record in records)
        {
            RequireNpcImplementation(
                record,
                NpcImplementationClassification.SpecializedNative);
        }

        NpcRecord Find(int id, int subId)
        {
            foreach (NpcRecord record in records)
            {
                if (record.Id == id && record.SubId == subId)
                    return record;
            }
            throw new InvalidOperationException(
                $"Room 1:49 is missing interaction ${id:x2}:${subId:x2}.");
        }

        NpcCharacter CreateNpc(NpcRecord record)
            => NpcCharacter.CreateFromRecord(record);

        NpcCharacter boy = CreateNpc(Find(0x3c, 0x0e));
        NpcCharacter father = CreateNpc(Find(0x3a, 0x0c));
        NpcCharacter observer = CreateNpc(Find(0x43, 0x06));
        var ball = new Room149Ball
        {
            Name = "Room149Ball",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        ball.Initialize(Resources.Room149.Visual("ball"));
        var family = new Room149FamilyInteraction(
            _saveData, Resources.Room149, boy, father, observer, ball);

        // Preserve object-table update order; the ball created by the boy's
        // state-0 handler occupies a later interaction slot.
        yield return new Room149NpcRoomEntity(
            boy, family, family.UpdateBoy);
        yield return new Room149NpcRoomEntity(
            father, family, family.UpdateFather);
        yield return new Room149NpcRoomEntity(
            observer, family, family.UpdateObserver);
        yield return new Room149BallRoomEntity(ball, family);
    }

    private bool StartsActive(PortalRecord record, int group, int room)
    {
        // timeportalSpawner.s sets bit 7 for subtype $01 until the Maku Tree
        // is saved and for subtype $02 until the Seed Satchel is obtained.
        // Bit 7 in object data is already-active unconditionally. Ordinary
        // subtype $00 portals wait for a fresh Tune of Echoes and must remain
        // inactive until instrument playback supplies that activation.
        int subId = record.SubId;
        int type = subId & 0x0f;
        if ((subId & 0x80) != 0)
            return true;
        if ((subId & 0x40) != 0 &&
            _saveData?.HasRoomFlag(group, room, 0x02) != true)
        {
            return true;
        }
        return type switch
        {
            0 => false,
            1 => _saveData is null ||
                !_saveData.HasGlobalFlag(GlobalFlag.MakuTreeSaved),
            2 => _saveData is null ||
                !_saveData.HasTreasure(TreasureId.SeedSatchel),
            _ => false
        };
    }

    private IRoomEntity CreateRock(OctorokRockSpawn spawn, OracleRoomData room)
    {
        var rock = new OctorokRockProjectile { Name = "OctorokRock", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        rock.Initialize(enemies.OctorokProjectile, room, spawn.Position, spawn.Angle);
        return new PostObjectHostileProjectileRoomEntity<OctorokRockProjectile>(rock);
    }

    private IRoomEntity CreateMaskedMoblin(
        MaskedMoblinSpawn spawn, OracleRoomData room)
    {
        var moblin = new MaskedMoblinCharacter
        {
            Name = "MaskedMoblin",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        moblin.Initialize(enemies.MaskedMoblin, room, spawn.Position, _random);
        EnemyHandlerDescriptor handler = enemies.EnemyHandlers.ResolveHandler(
            enemies.MaskedMoblin.Id,
            enemies.MaskedMoblin.SubId,
            "scripts/ages/scriptHelper.s:moblin_spawnEnemyHere");
        return new MaskedMoblinRoomEntity(
            moblin,
            handler.CombatSource(
                objectFlags: 0,
                killableEnemyIndex: 0,
                source:
                    "scripts/ages/scriptHelper.s:moblin_spawnEnemyHere"),
            owner.OnSoundRequested);
    }

    private IRoomEntity CreateGhini(GhiniSpawn spawn, OracleRoomData room)
    {
        ImportedEnemyDefinition record = enemies.ImportedEnemy(EnemyId.Ghini, 0x00);
        var ghini = new GhiniCharacter { Name = spawn.Name, ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        ghini.Initialize(record, room, spawn.Position, _random);
        const string source =
            "scripts/ages/scripts.s:ghiniHarassingMoosh_*:spawnenemyhere";
        EnemyHandlerDescriptor handler = enemies.EnemyHandlers.ResolveHandler(
            record.Id, record.SubId, source);
        return new GhiniRoomEntity(
            ghini,
            handler.CombatSource(
                objectFlags: 0,
                killableEnemyIndex: 0,
                source),
            owner.OnSoundRequested);
    }

    private IRoomEntity CreateArmos(ArmosSpawn spawn, OracleRoomData room)
    {
        var armos = new ArmosCharacter
        {
            Name = "RedArmos",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        armos.Initialize(
            enemies.ImportedEnemy(EnemyId.Armos),
            room,
            spawn.Position,
            spawn.ReplacementTile,
            _runtimeState,
            _random,
            owner.OnRoomTileChanged);
        return new ArmosRoomEntity(armos, owner.OnSoundRequested);
    }

    private IRoomEntity CreateFlyingTile(
        FlyingTileSpawn spawn,
        OracleRoomData room)
    {
        var tile = new FlyingTileCharacter
        {
            Name = "FlyingTile",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        tile.Initialize(
            spawn.Definition,
            room,
            spawn.Position,
            owner.OnRoomTileChanged,
            _animationTick);
        return new FlyingTileRoomEntity(tile, spawn.CountsAsEnemy);
    }

    private IRoomEntity CreateCuccoAttacker(CuccoAttackerSpawn spawn)
    {
        var attacker = new CuccoAttackerCharacter
        {
            Name = "CuccoAttacker",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        attacker.Initialize(enemies.ImportedEnemy(EnemyId.Cucco), _random, spawn.HitCount);
        return new CuccoAttackerRoomEntity(attacker);
    }

    private IRoomEntity CreateBabyCuccoReplacement(
        BabyCuccoReplacementSpawn spawn,
        OracleRoomData room)
    {
        var babyCucco = new BabyCuccoCharacter
        {
            Name = "TransformedBabyCucco",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        babyCucco.Initialize(
            enemies.ImportedEnemy(
                EnemyBehaviorTables.Shared.Cucco.BabyReplacementId),
            room,
            spawn.Position,
            _random,
            Resources.Bracelet.Data,
            Resources.Bomb.Data,
            owner.OnSoundRequested,
            owner.ApplyThrownObjectHit);
        return new BabyCuccoRoomEntity(babyCucco);
    }

    private IRoomEntity CreateEnemyArrow(EnemyArrowSpawn spawn, OracleRoomData room)
    {
        var arrow = new EnemyArrowProjectile { Name = "EnemyArrow", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        arrow.Initialize(enemies.EnemyArrow, room, spawn.Position, spawn.Angle, spawn.SubId);
        return new EnemyArrowRoomEntity(arrow);
    }

    private IRoomEntity CreateStalfosBone(StalfosBoneSpawn spawn, OracleRoomData room)
    {
        var bone = new StalfosBoneProjectile { Name = "StalfosBone", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        bone.Initialize(StalfosBoneRecord.Load(), room, spawn.Position, owner.ToScreen, spawn.ZHigh);
        return new StalfosBoneRoomEntity(bone);
    }

    private static IRoomEntity CreateBurningEnemy(BurningEnemySpawn spawn)
    {
        var part = new BurningEnemyPart { Name = "BurningEnemy", ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        part.Initialize(spawn.Target);
        return new BurningEnemyRoomEntity(part);
    }

    private IRoomEntity CreateKeeseFire(KeeseFireSpawn spawn)
    {
        var fire = new KeeseFirePart { Name = "KeeseFire", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        fire.Initialize(spawn.Position, spawn.ZHigh, owner.OnSoundRequested);
        return new KeeseFireRoomEntity(fire);
    }

    private bool WasKilledDuringPlacement(int index) =>
        index != 0 && (_runtimeState.ReadWramByte(EnemyPlacementMemory.KilledEnemies) & (1 << index)) != 0;

    private int NextKillableEnemyIndex(int flags)
    {
        // checkEnemyKilled is bypassed by object flag bit $01. Only the first
        // seven checked objects receive an index in Enemy.enabled.
        int count = _runtimeState.ReadWramByte(EnemyPlacementMemory.KillableCount);
        if ((flags & 0x01) != 0 || count >= 7)
            return 0;
        count++;
        _runtimeState.SetWramByte(EnemyPlacementMemory.KillableCount, (byte)count);
        return count;
    }

    private IRoomEntity CreateRoom148Debris(Room148DebrisSpawn spawn)
    {
        var debris = new Room148PickaxeDebris
        {
            Name = "Room148PickaxeDebris"
        };
        debris.Initialize(Resources.Room148.Record, spawn);
        return new DialogueFixedEffectRoomEntityAdapter<Room148PickaxeDebris>(
            debris);
    }

    private static IRoomEntity CreateShovelDebris(ShovelDebrisSpawn spawn)
    {
        var debris = new ShovelDebrisEffect
        {
            Name = "ShovelDebris",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        debris.Initialize(spawn.Position, spawn.Direction);
        return new DialogueFixedEffectRoomEntityAdapter<ShovelDebrisEffect>(
            debris);
    }

    private IRoomEntity CreateGrassDebris(GrassDebrisSpawn spawn)
    {
        var debris = new GrassDebrisEffect
        {
            Name = spawn.InteractionId == InteractionId.RedGrassDebris
                ? "RedGrassDebris"
                : "GrassDebris",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        debris.Initialize(
            spawn.Position,
            spawn.InteractionId,
            spawn.Flickers,
            spawn.Underwater,
            owner.OnSoundRequested);
        return new GrassDebrisRoomEntity(debris);
    }

    private IRoomEntity CreateRockDebris(RockDebrisSpawn spawn)
    {
        var debris = new RockDebrisEffect
        {
            Name = spawn.InteractionId == InteractionId.RockDebris2
                ? "RockDebris2"
                : "RockDebris",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        debris.Initialize(
            spawn.Position, spawn.InteractionId, owner.OnSoundRequested);
        return new DialogueFixedEffectRoomEntityAdapter<RockDebrisEffect>(
            debris);
    }

    private IRoomEntity CreateEmberSeed(EmberSeedSpawn spawn, OracleRoomData room)
    {
        var seed = new EmberSeedEffect
        {
            Name = spawn.Record.SeedItem switch
            {
                ItemId.EmberSeed => "EmberSeed",
                ItemId.ScentSeed => "ScentSeed",
                TreasureId.MysterySeeds => "MysterySeed",
                _ => $"Seed_{spawn.Record.SeedItem:x2}"
            },
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        int mysteryEffect =
            spawn.Record.SeedItem == TreasureId.MysterySeeds
                ? _random.Next().Value & 0x03
                : 0;
        seed.Initialize(
            spawn.Record, room, Resources.Breakables, spawn.LinkPosition, spawn.Direction,
            owner.OnSoundRequested, owner.OnItemDropEnteredHazard, owner.OnRoomTileChanged, _animationTick,
            drop => _itemDrops.DecideBreakableDrop(
                drop, _random, _inventory, _saveData), _saveData,
            spawn.Group,
            rooms is null
                ? null
                : direction => rooms.TryGetNeighbor(
                    spawn.Group, room.Id, direction, out int neighbor)
                    ? neighbor
                    : null,
            mysteryEffect,
            owner.OnObjectFellInHole,
              spawn.LaunchKind,
              spawn.Angle,
              spawn.LinkZFixed);
        return new EmberSeedRoomEntity(seed);
    }

    private IRoomEntity CreateBomb(BombSpawn spawn, OracleRoomData room)
    {
        if (_inventory is null)
        {
            throw new InvalidOperationException(
                "ITEM_BOMB cannot be allocated without live inventory state.");
        }
        var bomb = new BombEffect
        {
            Name = "Bomb",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        bomb.Initialize(
            spawn.Record,
            room,
            Resources.Breakables,
            spawn.Player,
            spawn.Group,
            spawn.HeldExplosion,
            owner.OnSoundRequested,
            (position, hazard, kind) =>
            {
                // bombs.s:bombUpdateThrowingVerticallyAndCheckDelete writes
                // this signal for any landed hazard, after the room-boundary check.
                if (owner.ActiveRoom.Group == 0 && owner.ActiveRoom.Id == 0x50)
                    _runtimeState.SetWramByte(WramAddress.wTmpcfc0, 1);
                if (hazard is HazardType.Water or HazardType.Lava)
                    owner.OnItemDropEnteredHazard(position, hazard);
                else if (hazard == HazardType.Hole)
                    owner.OnObjectFellInHole(kind);
            },
            owner.OnRoomTileChanged,
            _animationTick,
            drop => _itemDrops.DecideBreakableDrop(
                drop, _random, _inventory, _saveData),
            _saveData,
            rooms is null
                ? null
                : direction => rooms.TryGetNeighbor(
                    owner.ActiveRoom.Group, owner.ActiveRoom.Id, direction, out int neighbor)
                    ? neighbor
                    : null);
        return new BombRoomEntity(bomb);
    }

    private IRoomEntity CreateBombchu(BombchuSpawn spawn, OracleRoomData room)
    {
        var item = new BombchuItem { Name = "Bombchu", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        item.Initialize(item.ExplosionRecord, room, Resources.Breakables, spawn.Player, spawn.Group,
            _ => throw new InvalidOperationException("ITEM$0d cannot enter a held-Bomb explosion path."),
            owner.OnSoundRequested, (_, _, _) => throw new InvalidOperationException("ITEM$0d uses its common item-hazard caller."),
            owner.OnRoomTileChanged, _animationTick,
            drop => _itemDrops.DecideBreakableDrop(drop, _random,
                _inventory ?? throw new InvalidOperationException("ITEM$0d requires live inventory."), _saveData),
            _saveData, rooms is null ? null : direction => rooms.TryGetNeighbor(spawn.Group, room.Id, direction, out int neighbor) ? neighbor : null);
        item.Configure(room, spawn.Player, owner, spawn.Group, spawn.Position, spawn.ZFixed);
        return new BombchuRoomEntity(item);
    }

    private IRoomEntity CreateSwordBeam(
        SwordBeamSpawn spawn, OracleRoomData room)
    {
        var beam = new SwordBeamEffect
        {
            Name = "SwordBeam",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        beam.Initialize(
            Resources.SwordBeam, room, spawn.LinkPosition, spawn.Direction,
            owner.ToScreen, owner.OnSoundRequested);
        return new SwordBeamRoomEntity(beam);
    }

    private static IRoomEntity CreateSwordBeamClink(SwordBeamClinkSpawn spawn)
    {
        var clink = new ClinkEffect
        {
            Name = "SwordBeamClink",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        // Subid $81 requests the flickering variant; unlike sword-on-wall
        // clinks, the beam collision does not play a second sound.
        clink.Initialize(spawn.Position, flickers: true);
        clink.SetPhysicsProcess(false);
        return new SwordBeamClinkRoomEntity(clink);
    }

    private IRoomEntity CreateSwordWallClink(SwordWallClinkSpawn spawn)
    {
        var clink = new ClinkEffect { Name = "SwordWallClink", ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        clink.Initialize(spawn.Position, spawn.Flickers);
        // Ordinary subid$01 plays its sound in state0; bombable subid$80
        // suppresses it because the item caller already played SND_CLINK2.
        return new SwordBeamClinkRoomEntity(clink, spawn.Flickers
            ? () => owner.OnSoundRequested(SoundId.SndClink) : null);
    }

    private IRoomEntity CreateEnemyClink(EnemyClinkSpawn spawn)
    {
        var clink = new ClinkEffect
        {
            Name = "EnemyClink",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        clink.Initialize(spawn.Position, flickers: false, spawn.ZHigh);
        clink.SetPhysicsProcess(false);
        if (!spawn.InitializeOnUpdate) owner.OnSoundRequested(SoundId.SndClink);
        else clink.Visible = false;
        return new SwordBeamClinkRoomEntity(clink, spawn.InitializeOnUpdate
            ? () => owner.OnSoundRequested(SoundId.SndClink) : null);
    }

    private IRoomEntity CreatePuzzlePuff(PuzzlePuffSpawn spawn)
    {
        var puff = new PuzzlePuffEffect
        {
            Name = "PuzzlePuff",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        puff.Initialize(
            spawn.Position,
            spawn.Sound,
            spawn.Flickers,
            owner.OnSoundRequested,
            spawn.ZHigh,
            spawn.AlwaysUpdates);
        return new PuzzlePuffRoomEntity(puff);
    }

    private IRoomEntity CreateInteractionExplosion(
        InteractionExplosionSpawn spawn)
    {
        var explosion = new InteractionExplosionEffect
        {
            Name = "InteractionExplosion",
            // var03 bit 0 selects objectSetVisible81 or objectSetVisible82.
            ZIndex = (spawn.Var03 & 1) != 0
                ? ObjectDrawPriority.InFrontOfLinkZIndex : ObjectDrawPriority.BehindLinkZIndex
        };
        explosion.Initialize(
            spawn.Position,
            spawn.ZOffset,
            spawn.Visual,
            owner.OnSoundRequested);
        return new FixedEffectRoomEntityAdapter<
            InteractionExplosionEffect>(explosion);
    }

    private static IRoomEntity CreateInteractionSparkle(
        Vector2 position, int sourceAngle, InteractionSparkleVisual visual)
    {
        var sparkle = new InteractionSparkleEffect
        {
            Name = "InteractionSparkle_84_00",
            // sparkle.s selects visible81 for any nonzero source angle.
            ZIndex = sourceAngle == 0
                ? ObjectDrawPriority.BehindLinkZIndex
                : ObjectDrawPriority.InFrontOfLinkZIndex
        };
        sparkle.Initialize(position, sourceAngle, visual);
        return new InteractionSparkleRoomEntity(sparkle);
    }

    private IRoomEntity CreateDungeonSwitch(DungeonMechanicDatabaseRecord record,OracleRoomData room) =>
        new DungeonSwitchRoomEntity(record,room,Resources.DungeonMechanics,_runtimeState,
            _animationTick,owner.OnRoomTileChanged,owner.OnSoundRequested,(position,tile) => {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("PART$05 setTile requires its active room session.");
                return rooms.TrySetTile(position,tile);
            },_saveData);

    private IRoomEntity CreateSwitchTileToggler(DungeonObjectRecord record,OracleRoomData room) =>
        new SwitchTileTogglerRoomEntity(record,Resources.DungeonInteractions,_runtimeState,
            owner.OnRoomTileChanged,(position,tile) => {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("INTERAC$78 setTile requires its active room session.");
                return rooms.TrySetTile(position,tile);
            });

    private IRoomEntity CreateEnemySplash(EnemySplashSpawn spawn)
    {
        if (spawn.Hazard is not (HazardType.Water or HazardType.Lava))
        {
            throw new InvalidOperationException(
                $"Enemy splash cannot represent hazard {spawn.Hazard}.");
        }
        var effect = new SplashEffect
        {
            Name = spawn.Hazard == HazardType.Lava
                ? "EnemyLavaSplash"
                : "EnemyWaterSplash",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex // breakTileDebris.s: $03/$04 -> priority3.
        };
        effect.InitializeNative(
            spawn.Position,
            spawn.Hazard,
            owner.OnSoundRequested);
        effect.SetPhysicsProcess(false);
        return new NativeSplashRoomEntity(effect);
    }

    private IRoomEntity CreateFallingDownHole(FallingDownHoleSpawn spawn)
    {
        var effect = new FallingDownHoleEffect
        {
            Name = "FallingDownHole",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex // fallDownHole.s: visible83.
        };
        effect.Initialize(spawn.Position, owner.OnSoundRequested, spawn.Silent);
        return new FallingDownHoleRoomEntity(effect);
    }

    private IRoomEntity CreateDungeonKeyUse(DungeonKeyUseSpawn spawn)
    {
        var effect = new DungeonKeyUseEffect
        {
            Name = "DungeonKeyUse",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex // dungeonKeySprite.s: visible80.
        };
        effect.Initialize(spawn.Position, spawn.Visual,
            () => owner.OnSoundRequested(SoundId.SndGetSeed));
        return new DungeonKeyUseRoomEntity(effect);
    }

    private static IRoomEntity CreateOverworldKeyUse(OverworldKeyUseSpawn spawn)
    {
        var effect = new OverworldKeyUseEffect
        {
            Name = "OverworldKeyUse",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex // overworldKeySprite.s: visible80.
        };
        effect.Initialize(spawn.Position, spawn.Visual, spawn.Constants);
        return new DialogueFixedEffectRoomEntityAdapter<OverworldKeyUseEffect>(
            effect);
    }

    private static IRoomEntity CreateEraInfo(EraInfoSpawn spawn)
    {
        var display = new EraInfoDisplay();
        display.Initialize(spawn.Record);
        return new EraInfoRoomEntity(display);
    }

    private IRoomEntity CreateDeathPuff(EnemyDeathPuffSpawn spawn)
    {
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        puff.Initialize(spawn.Position, spawn.HighKnockback, spawn.EnemyId);
        owner.OnSoundRequested(SoundId.SndKillEnemy);
        return new DeathPuffRoomEntity(
            puff, _itemDrops, _random, _inventory, _saveData,
            spawn.DecrementsRoomCount, spawn.DropsItem);
    }

    private IRoomEntity CreateBossDeathExplosion(BossDeathExplosionSpawn spawn)
    {
        var explosion = new BossDeathExplosionEffect
        {
            Name = "BossDeathExplosion",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        explosion.Initialize(spawn.Position, spawn.BossId, owner.OnSoundRequested, spawn.ZHigh);
        return new BossDeathExplosionRoomEntity(
            explosion, _itemDrops, _random, _inventory, _saveData, () => owner.RoomEnemyCount);
    }

    private IRoomEntity CreateSubterrorDirt(SubterrorDirtSpawn spawn)
    {
        var dirt = new SubterrorDirtEffect
        {
            Name = "SubterrorDirt",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        dirt.Initialize(
            spawn.Position,
            Resources.DungeonVisuals.Visual("subterror-dirt"),
            Resources.DungeonBosses.SubterrorPalettes,
            owner.OnSoundRequested);
        return new DialogueFixedEffectRoomEntityAdapter<SubterrorDirtEffect>(
            dirt);
    }

    private static IRoomEntity CreateBossShadow(BossShadowSpawn spawn)
    {
        var shadow = new BossShadowEffect
        {
            Name = "BossShadow",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
        };
        shadow.Initialize(
            spawn.ParentPosition,
            spawn.ParentZ,
            spawn.ParentExists,
            spawn.Size,
            spawn.YOffset);
        return new BossShadowRoomEntity(shadow);
    }

    private IRoomEntity CreateKillPuff(KillEnemyPuffSpawn spawn)
    {
        var puff = new KillEnemyPuffEffect { Name = "KillEnemyPuff", ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex };
        puff.Initialize(spawn.Position, () => owner.OnSoundRequested(SoundId.SndKillEnemy), spawn.ZHigh);
        return new KillPuffRoomEntity(puff);
    }

    private IRoomEntity CreateItemDrop(ItemDropSpawn spawn, OracleRoomData room)
    {
        ItemDropEffect drop = CreateItemDropEffect(spawn, room);
        return new ItemDropRoomEntity(drop, owner.OnItemDropEnteredHazard);
    }

    private IRoomEntity CreateHeadThwompBombDrop(
        HeadThwompBombDropSpawn spawn,
        OracleRoomData room)
    {
        ItemDropEffect drop = CreateItemDropEffect(
            new ItemDropSpawn(ItemDropDatabase.Bombs, spawn.Position), room);
        return new HeadThwompBombDropRoomEntity(
            drop, room, _random, owner.OnItemDropEnteredHazard);
    }

    private ItemDropEffect CreateItemDropEffect(
        ItemDropSpawn spawn,
        OracleRoomData room)
    {
        var drop = new ItemDropEffect
        {
            Name = $"ItemDrop_{spawn.SubId:x2}",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        int treasure = ItemDropDatabase.TreasureForDrop(spawn.SubId);
        int collectionSound = treasure == TreasureId.None
            ? SoundId.MusNone
            : _treasures.GetBehaviour(treasure).Sound;
        drop.Initialize(
            spawn.SubId, spawn.Position, room, _itemDrops.GetVisual(spawn.SubId),
            spawn.Angle, spawn.DugUp, owner.OnSoundRequested, collectionSound,
            _itemDrops, _random, owner.MaplePresent, owner.SpawnDiggingEnemy, spawn.ZHigh);
        return drop;
    }

    internal IRoomEntity CreateDiggingEnemy(EnemyCombatSourceDescriptor source,
        Vector2 position, OracleRoomData room, byte[] beetleCounters)
    {
        ImportedEnemyDefinition record = enemies.ImportedEnemy(source.Id, source.SubId);
        if (source.Id == EnemyId.Rope)
        {
            var rope = new RopeCharacter { Name = $"DugRope_{source.SubId:x2}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            rope.Initialize(record, room, position, _random, owner.OnSoundRequested);
            return new RopeRoomEntity(rope, source, owner.OnSoundRequested);
        }
        var beetle = new BeetleCharacter { Name = $"DugBeetle_{source.SubId:x2}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        beetle.Initialize(record, room, position, _random, beetleCounters, owner.OnSoundRequested);
        return new BeetleRoomEntity(beetle, source, owner.OnSoundRequested);
    }

    private static IRoomEntity CreateCutsceneNpc(CutsceneNpcSpawn spawn)
    {
        RequireNpcImplementation(
            spawn.Record,
            NpcImplementationClassification.EventOwned);
        var npc = new NpcCharacter
        {
            Name = spawn.Name,
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        npc.Initialize(spawn.Record);
        return new CutsceneNpcRoomEntity(npc, spawn.Talkable, spawn.Solid);
    }

    private IRoomEntity CreateGroundTreasure(GroundTreasureDatabaseRecord record)
    {
        var treasure = new GroundTreasurePickup
        {
            Name = $"GroundTreasure_{record.TreasureObject}",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        treasure.Initialize(record, owner.OnSoundRequested, owner.ToScreen, tryCreatePuff: owner.TryCreatePuzzlePuff);
        return new GroundTreasureRoomEntity(
            treasure, owner.CanCollectGroundTreasure,
            owner.OnGroundTreasureCollected);
    }

    private IRoomEntity? CreateRoom5b6Interaction()
    {
        Room5b6InteractionRecord record = Resources.Room5b6.Record;
        if (_saveData?.HasRoomFlag(
                record.Group,
                record.Room,
                checked((byte)record.ItemRoomFlag)) == true)
        {
            return null;
        }

        var request = new GroundTreasureGrantRequest(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            record.TreasureObject,
            record.Source)
        {
            VisualOverride = new GroundTreasureVisualOverride(
                record.Sprite,
                record.TileBase,
                record.Palette,
                record.Animation),
            ExpectedTreasureId = record.TreasureId,
            ExpectedSubId = record.TreasureSubId,
            ExpectedObjectParameter = record.TreasureParameter
        };
        var pickup = new GroundTreasurePickup
        {
            Name = "Room5b6ChevalRope",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        pickup.Initialize(
            request.Resolve(_treasures),
            owner.OnSoundRequested,
            owner.ToScreen);
        return new MiscellaneousTreasureRoomEntity(
            pickup,
            owner.CanCollectGroundTreasure,
            owner.OnGroundTreasureCollected,
            record.PostGrantWait,
            record.PickupDistance,
            () => CompanionRuntimeState.ForgetRemembered(_runtimeState));
    }

    private IRoomEntity? CreateRoom2e3Interaction()
    {
        Room2e3InteractionRecord record = Resources.Room2e3.Record;
        if (_saveData?.HasRoomFlag(
                record.Group,
                record.Room,
                checked((byte)record.ItemRoomFlag)) == true)
        {
            return null;
        }

        var request = new GroundTreasureGrantRequest(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            record.TreasureObject,
            record.Source)
        {
            VisualOverride = new GroundTreasureVisualOverride(
                record.Sprite,
                record.TileBase,
                record.Palette,
                record.Animation),
            ExpectedTreasureId = record.TreasureId,
            ExpectedSubId = record.TreasureSubId,
            ExpectedObjectParameter = record.TreasureParameter
        };
        var pickup = new GroundTreasurePickup
        {
            Name = "Room2e3Bombs",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        pickup.Initialize(
            request.Resolve(_treasures),
            owner.OnSoundRequested,
            owner.ToScreen);
        return new MiscellaneousTreasureRoomEntity(
            pickup,
            owner.CanCollectGroundTreasure,
            owner.OnGroundTreasureCollected,
            record.PostGrantWait,
            record.PickupDistance,
            preGrant: () =>
                (_inventory ?? throw new InvalidOperationException(
                    $"{record.Source} cannot refill Bombs without live inventory."))
                .RefillBombs());
    }

    private IEnumerable<IRoomEntity> CreateRoom5bfInteractions()
    {
        var state = new LeverState(_runtimeState, WramAddress.wLever1PullDistance);
        LeverRoomEntity? lever = null;
        foreach (Room5bfInteractionRecord record in Resources.Room5bf.Records)
        {
            switch (record.Kind)
            {
                case Room5bfInteractionKind.Flippers:
                    if (_saveData?.HasRoomFlag(
                            Resources.Room5bf.Constants.Group,
                            Resources.Room5bf.Constants.Room,
                            checked((byte)Resources.Room5bf.Constants.ItemRoomFlag)) == true)
                    {
                        continue;
                    }
                    var request = new GroundTreasureGrantRequest(
                        Resources.Room5bf.Constants.Group,
                        Resources.Room5bf.Constants.Room,
                        record.Order,
                        record.Y,
                        record.X,
                        "TREASURE_OBJECT_FLIPPERS_00",
                        record.Source)
                    {
                        VisualOverride = new GroundTreasureVisualOverride(
                            record.Sprite,
                            record.TileBase,
                            record.Palette,
                            record.Animations[0]),
                        ExpectedTreasureId = Resources.Room5bf.Constants.TreasureId,
                        ExpectedSubId = Resources.Room5bf.Constants.TreasureSubId,
                        ExpectedObjectParameter =
                            Resources.Room5bf.Constants.TreasureParameter
                    };
                    var pickup = new GroundTreasurePickup
                    {
                        Name = "Room5bfFlippers",
                        ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
                    };
                    pickup.Initialize(
                        request.Resolve(_treasures),
                        owner.OnSoundRequested,
                        owner.ToScreen);
                    yield return new MiscellaneousTreasureRoomEntity(
                        pickup,
                        owner.CanCollectGroundTreasure,
                        owner.OnGroundTreasureCollected,
                        Resources.Room5bf.Constants.PostGrantWait,
                        Resources.Room5bf.Constants.PickupDistance);
                    break;

                case Room5bfInteractionKind.SlidingBlock:
                    yield return new Room5bfSlidingBlock(
                        record,
                        state,
                        Resources.Room5bf.Constants,
                        Resources.Room5bf.BlockPalette);
                    break;

                case Room5bfInteractionKind.Lever:
                    lever = new LeverRoomEntity(
                        record.ToNpcRecord(),
                        state,
                        new LeverBehavior(Resources.Room5bf.Constants.LeverLength, Resources.Room5bf.Constants.PullSpeed,
                            Resources.Room5bf.Constants.LeverRadiusY, Resources.Room5bf.Constants.LeverRadiusX,
                            Resources.Room5bf.Constants.LinkYOffset, Resources.Room5bf.Constants.ConnectionStep,
                            Resources.Room5bf.Constants.MoveSound, Resources.Room5bf.Constants.FullSound),
                        owner.OnSoundRequested);
                    yield return lever;
                    break;

                case Room5bfInteractionKind.LeverConnection:
                    if (lever is null)
                    {
                        throw new InvalidOperationException(
                            $"Room 5:bf lever connection from {record.Source} " +
                            "precedes its parent lever.");
                    }
                    yield return new LeverConnectionRoomEntity(
                        record.ToNpcRecord(), record.Animations, lever, Resources.Room5bf.Constants.ConnectionStep);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported room 5:bf interaction kind " +
                        $"{record.Kind} from {record.Source}.");
            }
        }
    }

    private IRoomEntity CreateMapleDroppedItem(
        MapleDroppedItemSpawn spawn,
        OracleRoomData room)
    {
        var item = new MapleDroppedItem
        {
            Name = $"MapleItem_{spawn.Slot}_{spawn.Record.Index:x2}",
            ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex // itemFromMaple.s: visiblec3.
        };
        item.Initialize(
            spawn.Record,
            spawn.Encounter,
            room,
            _random,
            spawn.Slot,
            spawn.SourcePosition,
            spawn.SourceZFixed,
            owner.OnMapleItemCollected);
        spawn.Encounter.Register(item);
        return new MapleDroppedItemRoomEntity(item);
    }

    private IRoomEntity CreateLightableTorch(
        LightableTorchSpawn spawn,
        OracleRoomData room) => new LightableTorchRoomEntity(
            spawn.State, spawn.PackedPosition, Resources.DarkRooms,
            owner.OnSoundRequested,(position,tile) =>
            {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("PART$06 setTile requires its active room session.");
                // partCode06 ignores setTile failure and still deletes itself.
                if (rooms.TrySetTile(position,tile)) owner.OnRoomTileChanged();
            });

    internal IEnumerable<IRoomEntity> CreateTimePortals(int group, OracleRoomData room)
    {
        foreach (PortalRecord record in timePortals.GetRoomPortals(group, room.Id))
        {
            var portal = new TimePortal { Name = $"TimePortal_{record.SubId:x2}", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
            portal.InitializePlaced(
                record,
                room,
                StartsActive(record, group, room.Id),
                _saveData,
                owner.ReadPlayingInstrument,
                owner.OnSoundRequested);
            yield return new TimePortalRoomEntity(portal, owner.OnTimePortalEntered);
        }
        if (_saveData is not null &&
            _saveData.TimePortalGroup == group &&
            _saveData.TimePortalRoom == room.Id)
        {
            timePortals.ApplyEntryTileReplacement(
                room, _saveData.TimePortalPosition, _animationTick());
            yield return CreateTemporaryTimePortal(
                room, PointForPackedPosition(_saveData.TimePortalPosition));
        }
    }

    internal IRoomEntity CreateTemporaryTimePortal(
        OracleRoomData room,
        Vector2 position)
    {
        var portal = new TimePortal
        {
            Name = "TemporaryTimePortal",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        portal.InitializeTemporary(timePortals.TemporaryVisual, room, position, _saveData,
            () => _runtimeState.ReadWramByte(WramAddress.wcde0) != 0);
        return new TimePortalRoomEntity(portal, owner.OnTimePortalEntered);
    }

    private bool RoomObjectConditionMet(
        RoomObjectRecord record,
        int group,
        OracleRoomData room)
    {
        int stateModifier = (room.TilesetFlags & (int)TilesetFlags.Underwater) != 0 ? 1 : 0;
        if (_saveData?.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagLayoutSwap) == true)
            stateModifier++;
        // roomInitialization.s:calculateRoomStateModifier uses the selected
        // companion for pack $7f, with the standard branch for value $00.
        int companion = _saveData?.ReadWramByte(WramAddress.wAnimalCompanion) ?? 0;
        if (room.IsCompanionRegion && companion != 0) stateModifier = companion - 0x0b;
        return (record.ConditionMask & (1 << stateModifier)) != 0;
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private bool DungeonEnemyCountIsComplete(int group, OracleRoomData room)
    {
        foreach (RoomObjectRecord source in
            enemies.GetRoomObjects(group, room.Id))
        {
            if (!RoomObjectConditionMet(source, group, room))
                continue;
            // objectData flags bit $02 calls decEnemyCounterIfApplicable
            // immediately after allocation, so an omitted count-exempt enemy
            // cannot make the shutter's live count incomplete.
            if ((source.Flags & 0x02) != 0)
                continue;
            EnemyObjectHandlerResolution resolution =
                enemies.EnemyHandlers.Resolve(source);
            switch (resolution.SlotPolicy)
            {
                case EnemyObjectSlotPolicy.RandomEnemy:
                case EnemyObjectSlotPolicy.FixedEnemy:
                case EnemyObjectSlotPolicy.ParameterEnemy:
                    if (!resolution
                        .RequireEnemyHandler(source)
                        .CompletesDungeonEnemyCount)
                    {
                        return false;
                    }
                    break;

            }
        }
        return true;
    }

    private bool DungeonEnemyMechanicsAreSupported(
        IReadOnlyList<DungeonMechanicDatabaseRecord> records,
        int group,
        OracleRoomData room)
    {
        bool hasSupportedNativeBossRecord = false;
        foreach (DungeonObjectRecord native in
            Resources.SpiritsGrave.GetRoomRecords(group, room.Id))
        {
            if (native.Kind is DungeonObjectKind.GiantGhini or
                DungeonObjectKind.PumpkinHead)
            {
                // A completed boss's BeforeEvent record is suppressed by
                // ROOMFLAG_80. That is still a complete zero-enemy stream:
                // the original shutter script sees wNumEnemies == 0 and
                // opens every enemy shutter while the room initializes.
                hasSupportedNativeBossRecord = true;
                break;
            }
        }
        foreach (DungeonObjectRecord native in
            Resources.WingDungeon.GetRoomRecords(group, room.Id))
        {
            if (native.Kind is DungeonObjectKind.HeadThwomp or DungeonObjectKind.Swoop)
            {
                hasSupportedNativeBossRecord = true;
                break;
            }
        }
        foreach (DungeonObjectRecord native in
            Resources.MoonlitGrotto.GetRoomRecords(group, room.Id))
        {
            if (native.Kind is DungeonObjectKind.Subterror or
                DungeonObjectKind.ShadowHag)
            {
                hasSupportedNativeBossRecord = true;
                break;
            }
        }
        foreach (DungeonObjectRecord native in Resources.SkullDungeon.GetRoomRecords(group, room.Id))
            if (native.Kind is DungeonObjectKind.ArmosWarrior or DungeonObjectKind.Eyesoar) hasSupportedNativeBossRecord = true;
        foreach (DungeonObjectRecord native in Resources.CrownDungeon.GetRoomRecords(group, room.Id))
            if (native.Kind is DungeonObjectKind.Smasher or DungeonObjectKind.SmogSentinel) hasSupportedNativeBossRecord = true;
        foreach (DungeonObjectRecord native in Resources.MermaidDungeon.GetRoomRecords(group,room.Id))
            if (native.Kind is DungeonObjectKind.Vire or DungeonObjectKind.Octogon) hasSupportedNativeBossRecord = true;
        bool hasEnemyCountConsumer = false;
        foreach (DungeonMechanicDatabaseRecord record in records)
        {
            if (record.Id == InteractionId.SnowDebris)
                continue;
            if (record.Id == InteractionId.DoorController || record.Id == InteractionId.PushBlockTrigger ||
                record is { Id: InteractionId.DungeonStuff, SubId: 0x01 })
            {
                hasEnemyCountConsumer = true;
            }
            if (record.Id == InteractionId.DoorController && record.SubId <= 0x07)
                continue;
            if (!record.CountSourceComplete && !hasSupportedNativeBossRecord)
                return false;
        }
        return hasEnemyCountConsumer && DungeonEnemyCountIsComplete(group, room);
    }

    internal IEnumerable<IRoomEntity> ParseRandomEnemies(RoomObjectRecord source,OracleRoomData room,
        EnemyPlacementContext placementContext,EnemyPlacementReservations? reservations = null)
    {
        if (source.Kind != RoomObjectKind.RandomEnemy)
            throw new InvalidOperationException($"{source.Source}: expected a random-enemy row.");
        reservations ??= new EnemyPlacementReservations(_runtimeState);
        var handler = enemies.EnemyHandlers.Resolve(source).RequireEnemyHandler(source);
        for (int instance = 0; instance < source.Count; instance++)
        {
            int index = NextKillableEnemyIndex(source.Flags);
            if (WasKilledDuringPlacement(index)) continue;
            int slot = owner.FindFreeEnemySlot();
            if (slot < 0) yield break;
            if (!TryChooseRandomEnemyPosition(room,source.Flags,reservations,placementContext,out var position))
            {
                // US objectDataOp6 leaves the counted allocation behind.
                owner.RetainFailedPlacementCount(source.Flags);
                continue;
            }
            var entity = EnemyFactory.CreateOrderedEnemy(handler,source,room,position,instance,index,placementContext);
            owner.RegisterEnemySlot(entity,slot);
            if (entity is not null) yield return entity;
        }
    }

    private bool TryChooseRandomEnemyPosition(
        OracleRoomData room,
        int flags,
        EnemyPlacementReservations reservations,
        EnemyPlacementContext placementContext,
        out Vector2 position)
    {
        _runtimeState.SetWramByte(EnemyPlacementMemory.AttemptsRemaining, 0x40);
        while (true)
        {
            // getRandomPositionForEnemy decrements before asking its inner
            // candidate loop. Boundary/reservation retries consume cursor
            // bytes, not additional attempts. The final zero update gives up.
            byte attempts = unchecked((byte)(_runtimeState.ReadWramByte(EnemyPlacementMemory.AttemptsRemaining) - 1));
            _runtimeState.SetWramByte(EnemyPlacementMemory.AttemptsRemaining, attempts);
            if (attempts == 0) break;
            int packed, tileY, tileX;
            while (true)
            {
                packed = _random.NextPlacementValue();
                tileY = packed >> 4;
                tileX = packed & 0x0f;
                bool validBoundary = room.Group < 4
                    ? tileY < OracleRoomData.ViewportHeight / OracleRoomData.MetatileSize &&
                        tileX < OracleRoomData.ViewportWidth / OracleRoomData.MetatileSize
                    : tileY > 0 && tileY < room.HeightInTiles - 1 &&
                        tileX > 0 && tileX < room.WidthInTiles - 1;
                if (validBoundary && !reservations.Contains(packed)) break;
            }
            _runtimeState.SetWramByte(EnemyPlacementMemory.Position, (byte)packed);
            if (!placementContext.Allows(room, packed))
                continue;

            position = new Vector2(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            if ((flags & 0x04) == 0 && !Resources.EnemySpawnTiles.IsValid(
                room.ActiveCollisions, room.GetTerrainInfo(position)))
                continue;
            reservations.Add(packed);
            return true;
        }
        position = Vector2.Zero;
        return false;
    }
}

internal enum EnemyPlacementEntryKind
{
    Unrestricted,
    Scrolling,
    Warp,
    ScreenWarp
}

/// <summary>
/// Inputs consumed by checkPositionValidForEnemySpawn. Ordinary scrolling
/// excludes the three metatile rows or columns at Link's incoming edge; a
/// packed warp destination excludes the surrounding 5x5-metatile square.
/// Scrolling also retains Link's final packed position for the destination
/// room's replaceShutterForLinkEntering pass.
/// </summary>
internal readonly record struct EnemyPlacementContext(
    EnemyPlacementEntryKind Kind,
    Vector2I ScrollDirection,
    int WarpDestination,
    int EntryPackedPosition)
{
    internal Vector2I BossEntryDirection => Kind == EnemyPlacementEntryKind.Scrolling
        ? ScrollDirection : Vector2I.Zero;

    internal static EnemyPlacementContext Unrestricted => new(
        EnemyPlacementEntryKind.Unrestricted, Vector2I.Zero, -1, -1);

    internal static EnemyPlacementContext Scrolling(
        Vector2I direction,
        int entryPackedPosition = -1)
    {
        if (direction != Vector2I.Up && direction != Vector2I.Right &&
            direction != Vector2I.Down && direction != Vector2I.Left)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction), direction, "Scroll direction must be cardinal.");
        }
        if (entryPackedPosition is < -1 or >= 0xf0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryPackedPosition), entryPackedPosition,
                "A scrolling entry position must be a packed position below `$f0.");
        }
        return new EnemyPlacementContext(
            EnemyPlacementEntryKind.Scrolling, direction, -1, entryPackedPosition);
    }

    internal static EnemyPlacementContext Warp(int packedDestination)
    {
        if (packedDestination is < 0 or >= 0xf0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packedDestination), packedDestination,
                "A direct warp destination must be a packed position below `$f0.");
        }
        return new EnemyPlacementContext(
            EnemyPlacementEntryKind.Warp, Vector2I.Zero, packedDestination, -1);
    }

    internal static EnemyPlacementContext FromWarpDestination(int packedDestination) =>
        packedDestination >= 0xf0
            ? new EnemyPlacementContext(
                EnemyPlacementEntryKind.ScreenWarp, Vector2I.Up, packedDestination, -1)
            : Warp(packedDestination);

    internal bool Allows(OracleRoomData room, int packedPosition)
    {
        int tileY = packedPosition >> 4;
        int tileX = packedPosition & 0x0f;
        return Kind switch
        {
            EnemyPlacementEntryKind.Unrestricted => true,
            EnemyPlacementEntryKind.Warp =>
                Math.Abs(tileY - (WarpDestination >> 4)) >= 3 ||
                Math.Abs(tileX - (WarpDestination & 0x0f)) >= 3,
            EnemyPlacementEntryKind.Scrolling or EnemyPlacementEntryKind.ScreenWarp =>
                AllowsScrolling(room, tileX, tileY),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null)
        };
    }

    private bool AllowsScrolling(OracleRoomData room, int tileX, int tileY)
    {
        bool small = room.Group < 4;
        int minimumY = small ? 0 : 1;
        int maximumY = small ? room.HeightInTiles : room.HeightInTiles - 1;
        int minimumX = small ? 0 : 1;
        int maximumX = small ? room.WidthInTiles : room.WidthInTiles - 1;

        if (ScrollDirection == Vector2I.Up)
            maximumY = room.HeightInTiles - 3;
        else if (ScrollDirection == Vector2I.Right)
            minimumX = 3;
        else if (ScrollDirection == Vector2I.Down)
            minimumY = 3;
        else
            maximumX = small ? room.WidthInTiles - 3 : room.WidthInTiles - 4;

        return tileY >= minimumY && tileY < maximumY &&
            tileX >= minimumX && tileX < maximumX;
    }
}

internal sealed record BossDeathExplosionSpawn(Vector2 Position, int BossId, int ZHigh = 0)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record BossShadowSpawn(
    Func<Vector2> ParentPosition,
    Func<int> ParentZ,
    Func<bool> ParentExists,
    int Size,
    int YOffset) : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record CutsceneNpcSpawn(
    NpcRecord Record,
    string Name,
    bool Talkable = false,
    bool Solid = false)
    : RoomEntitySpawn;

internal sealed record DungeonKeyUseSpawn(
    Vector2 Position,
    TreasureObjectVisualRecord Visual) : RoomEntitySpawn;

internal sealed record EmberSeedSpawn(
    Vector2 LinkPosition,
    Vector2I Direction,
    SeedRecord Record,
    int Group,
    SeedLaunchKind LaunchKind = SeedLaunchKind.Satchel,
    int Angle = ObjectAngle.Up,
    int LinkZFixed = 0)
    : RoomEntitySpawn;

internal enum SeedLaunchKind
{
    Satchel,
    Shooter
}

internal sealed record BombSpawn(
    Player Player,
    BombRecord Record,
    int Group,
    Action<BombEffect> HeldExplosion)
    : RoomEntitySpawn;

internal sealed record BombchuSpawn(Player Player, int Group, Vector2 Position, int ZFixed) : RoomEntitySpawn;

internal sealed record SwordBeamClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;

internal sealed record SwordWallClinkSpawn(Vector2 Position, bool Flickers)
    : RoomEntitySpawn;

internal sealed record EnemyClinkSpawn(Vector2 Position, bool InitializeOnUpdate = false, int ZHigh = 0)
    : RoomEntitySpawn;

internal sealed record CuccoAttackerSpawn(int HitCount)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record BabyCuccoReplacementSpawn(Vector2 Position)
    : RoomEntitySpawn;

internal sealed record StatueEyeballSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MovingPlatformSpawn(Vector2 Position, int SubId)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MinibossPortalSpawn : RoomEntitySpawn;

internal sealed record SubterrorDirtSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record GrassDebrisSpawn(
    Vector2 Position,
    int InteractionId = InteractionId.GrassDebris,
    bool Flickers = false,
    bool Underwater = false)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record RockDebrisSpawn(
    Vector2 Position,
    int InteractionId = InteractionId.RockDebris)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record PuzzlePuffSpawn(
    Vector2 Position,
    int Sound,
    bool Flickers = false,
    int ZHigh = 0,
    bool AlwaysUpdates = true)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record BoomerangSpawn(Vector2 Position, int Angle, int ZHigh = 0) : RoomEntitySpawn;

internal sealed record PumpkinHeadProjectileSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn;

internal sealed record HeadThwompProjectileSpawn(
    Vector2 Position,
    HeadThwompProjectileKind Kind,
    int Angle,
    int Speed,
    bool RandomizeLaunch = false) : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record HeadThwompBoulderSpawn()
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record HeadThwompBombDropSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabaseRecord Visual,
    ConstantsRecord Constants) : RoomEntitySpawn;

internal sealed record EraInfoSpawn(EraInfoDatabaseRecord Record)
    : RoomEntitySpawn;

internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record GhiniSpawn(Vector2 Position, string Name)
    : RoomEntitySpawn;

internal sealed record ArmosSpawn(Vector2 Position, int ReplacementTile)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record FlyingTileSpawn(
    ImportedEnemyDefinition Definition,
    Vector2 Position,
    bool CountsAsEnemy)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record ArmosSpawnerSpawn(int SourceTile, int ReplacementTile)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MoonlitGrottoOrbSpawn(int Group, int Room)
    : RoomEntitySpawn;

internal sealed record EnemyClearChestSpawn(
    int Group,
    int Room,
    int PackedPosition)
    : RoomEntitySpawn;

internal sealed record EnemySmallKeyRewardSpawn(
    GroundTreasureGrantRequest Request)
    : RoomEntitySpawn;

internal sealed record LightableTorchSpawn(
    LightableTorchState State,
    int PackedPosition)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record RespawnableBushSpawn(
    int PackedPosition,
    int DropSubId)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record GroundTreasureSpawn(GroundTreasureDatabaseRecord Record)
    : RoomEntitySpawn;

internal sealed record GroundTreasureGrantSpawn(
    GroundTreasureGrantRequest Request)
    : RoomEntitySpawn;

internal sealed record MapleDroppedItemSpawn(
    MapleItemRecord Record,
    MapleEncounterState Encounter,
    int Slot,
    Vector2 SourcePosition,
    int SourceZFixed,
    bool UpdateThisFrame = false) : RoomEntitySpawn(UpdateThisFrame);

internal sealed record GiantGhiniChildSpawn(GiantGhiniBoss Owner, int Index)
    : RoomEntitySpawn(UpdateThisFrame: true);
