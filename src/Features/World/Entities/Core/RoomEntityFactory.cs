using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RoomEntityFactory(
    BipinBlossomFamilyStateResolver familyState,
    EnemyDatabase enemies,
    ItemDropDatabase itemDrops,
    TimePortalDatabase timePortals,
    OracleRandom random,
    OracleSaveData? saveData,
    OracleRuntimeState runtimeState,
    Action<TimePortal> portalEntered,
    Func<int> playingInstrument,
    Func<bool> groundTreasureCollectionAllowed,
    Action<GroundTreasurePickup, Player> groundTreasureCollected,
    Action<int, string> dungeonEntranceTriggered,
    Action<Warp> roomWarpRequested,
    Action<GashaSpotInteraction, Player> gashaInteractionRequested,
    Action<GashaSpotInteraction, Player> gashaNutCaught,
    InventoryState? inventory,
    TreasureDatabase treasures,
    Action<Vector2, HazardType> itemDropEnteredHazard,
    Action<ObjectFellInHoleKind> objectFellInHole,
    Action<int> soundRequested,
    Action<Rect2, int, int, int> applyThrownObjectHit,
    Func<int> roomEnemyCount,
    Func<int, bool> enemyWasKilled,
    Func<int, bool> triggerActive,
    Func<int> triggerState,
    Action<int, bool> setTrigger,
    Action roomTileChanged,
    Action<DungeonEssence, Player> dungeonEssenceTriggered,
    Func<bool> bossShuttersClosed,
    Action<int> screenShakeRequested,
    Action disableLinkCollisionsAndMenu,
    Action enableLinkCollisionsAndMenu,
    Action<int, int> roomMusicRequested,
    Action<int, string, Vector2> nativeBossDialogueRequested,
    Action<int, string, Player> mapleDialogueRequested,
    Action<int, string, Vector2> seedTreeMessageRequested,
    Action<int, string, Vector2> owlStatueMessageRequested,
    Func<bool> dialogueOpen,
    Action<MapleItemRecord, Player> mapleItemCollected,
    Action<int> horizontalScreenShakeRequested,
    Func<Vector2, Vector2> worldToScreen,
    Func<long> animationTick,
    RoomSession? rooms)
{
    private readonly Room148PickaxeDatabase _room148 = new();
    private readonly Room149FamilyDatabase _room149 = new();
    private readonly MakuSproutRoomDatabase _makuSproutRoom = new();
    private readonly Room20eNpcDatabase _room20e = new();
    private readonly StoneRabbitDatabase _stoneRabbit = new();
    private readonly BusinessScrubDatabase _businessScrub = new();
    private readonly NayruHouseDatabase _nayruHouse = new();
    private readonly VasuShopDatabase _vasuShop = new();
    private readonly LynnaShopDatabase _lynnaShop = new();
    private readonly BlackTowerWorkerDatabase _blackTower = new();
    private readonly ShootingGalleryEventDatabase _shootingGallery = new();
    private readonly ComedianEventDatabase _comedian = new();
    private readonly MaskSalesmanEventDatabase _maskSalesman = new();
    private readonly ToiletHandEventDatabase _toiletHand = new();
    private readonly PoeEventDatabase _poe = new();
    private readonly TroyHouseDatabase _troyHouse = new();
    private readonly DungeonEntranceInteractionDatabase _dungeonEntrances = new();
    private readonly DungeonInteractionDatabase _dungeonInteractions = new();
    private readonly DungeonInteractionVisualDatabase _dungeonVisuals = new();
    private readonly DungeonBossDatabase _dungeonBosses = new();
    private readonly SpiritsGraveDatabase _spiritsGrave = new();
    private readonly WingDungeonDatabase _wingDungeon = new();
    private readonly EnemySpawnTileDatabase _enemySpawnTiles = new();
    private readonly GroundTreasureDatabase _groundTreasures = new();
    private readonly DungeonMechanicDatabase _dungeonMechanics = new();
    private readonly RoomTileChangeWatcherDatabase _tileChangeWatchers = new();
    private readonly BreakableTileDatabase _breakables = new();
    private readonly SwordBeamDatabase _swordBeam = new();
    private readonly GashaSpotDatabase _gashaSpots = new();
    private readonly DarkRoomDatabase _darkRooms = new();
    private readonly MapleEventDatabase _maple = new();
    private readonly SeedTreeDatabase _seedTrees = new();
    private readonly OwlStatueDatabase _owlStatues = new();
    private readonly DungeonMapDatabase _dungeonMaps =
        rooms?.DungeonMaps ?? new DungeonMapDatabase();

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
                _dungeonMechanics.OpenTile,
                out int replacement))
        {
            return;
        }

        room.SetPositionTileAndCollision(
            position, checked((byte)replacement), null, animationTick());
    }

    internal void UpdateSeedTreeRefillState(
        int activeGroup,
        int activeRoom) =>
        _seedTrees.UpdateRefillState(
            runtimeState, activeGroup, activeRoom);

    public IEnumerable<IRoomEntity> CreateRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        int activeGroup = group;
        bool spawnMaple =
            saveData is not null &&
            inventory is not null &&
            _maple.IsEligibleLocation(
                activeGroup, room.Id, inventory.AnimalCompanion) &&
            saveData.MapleKillCounter >=
                RingEffects.MapleKillThreshold(inventory);
        if (spawnMaple)
            saveData!.SetMapleKillCounter(0);

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
        foreach (IRoomEntity controller in
            CreateMinecartShutterControllers(room))
        {
            yield return controller;
        }
        if (group == 4 && room.Id is >= 0x27 and <= 0x48)
        {
            MinecartRuntimeState.EnsureInitialized(
                runtimeState, _wingDungeon.Minecarts);
            foreach (ActiveMinecart cart in
                MinecartRuntimeState.StationaryInRoom(
                    runtimeState, room.Id))
            {
                yield return CreateMinecart(cart, room);
            }
            if (MinecartRuntimeState.TryGetRide(
                    runtimeState, room.Id, out ActiveMinecart ride))
            {
                yield return CreateMinecart(ride, room);
            }
        }
        IReadOnlyList<DarkRoomDatabaseRecord> darkRoomRecords =
            _darkRooms.GetRoomRecords(group, room.Id);
        if (darkRoomRecords.Count > 0)
        {
            var darkRoomState = new DarkRoomState(room, _darkRooms);
            foreach (DarkRoomDatabaseRecord record in darkRoomRecords)
            {
                yield return record.Kind switch
                {
                    DarkRoomDatabaseObjectKind.Reward =>
                        new DarkRoomRewardRoomEntity(
                            record, _darkRooms, darkRoomState, saveData),
                    DarkRoomDatabaseObjectKind.Handler =>
                        new DarkRoomHandlerRoomEntity(
                            record, room, _darkRooms, darkRoomState),
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
            _dungeonMechanics.GetRoomRecords(group, room.Id);
        IReadOnlyList<PlacementRecord>
            sharedDungeonRecords = _dungeonEntrances.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> spiritsGraveRecords =
            _spiritsGrave.GetRoomRecords(group, room.Id);
        IReadOnlyList<DungeonObjectRecord> wingDungeonRecords =
            _wingDungeon.GetRoomRecords(group, room.Id);
        ColoredCubePuzzleState? spiritsGravePuzzle =
            group == 4 && room.Id == 0x20 ? new ColoredCubePuzzleState() : null;
        ColoredCubePuzzleState? wingDungeonPuzzle =
            wingDungeonRecords.Count > 0 &&
            HasColoredCubeState(wingDungeonRecords)
                ? new ColoredCubePuzzleState()
                : null;
        bool enemyMechanicsSupported = DungeonEnemyMechanicsAreSupported(
            dungeonRecords, group, room);
        int mechanicIndex = 0;
        int sharedIndex = 0;
        int spiritsGraveIndex = 0;
        int wingDungeonIndex = 0;
        while (mechanicIndex < dungeonRecords.Count ||
               sharedIndex < sharedDungeonRecords.Count ||
               spiritsGraveIndex < spiritsGraveRecords.Count ||
               wingDungeonIndex < wingDungeonRecords.Count)
        {
            int mechanicOrder = mechanicIndex < dungeonRecords.Count
                ? dungeonRecords[mechanicIndex].Order : int.MaxValue;
            int sharedOrder = sharedIndex < sharedDungeonRecords.Count
                ? sharedDungeonRecords[sharedIndex].Order : int.MaxValue;
            int spiritsGraveOrder = spiritsGraveIndex < spiritsGraveRecords.Count
                ? spiritsGraveRecords[spiritsGraveIndex].Order : int.MaxValue;
            int wingDungeonOrder = wingDungeonIndex < wingDungeonRecords.Count
                ? wingDungeonRecords[wingDungeonIndex].Order : int.MaxValue;
            bool useShared = sharedOrder < mechanicOrder &&
                sharedOrder < spiritsGraveOrder &&
                sharedOrder < wingDungeonOrder;
            if (useShared)
            {
                PlacementRecord record =
                    sharedDungeonRecords[sharedIndex++];
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
                spiritsGraveOrder < wingDungeonOrder)
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

            if (wingDungeonOrder < mechanicOrder)
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

            DungeonMechanicDatabaseRecord mechanic = dungeonRecords[mechanicIndex++];
            IRoomEntity? mechanicEntity = CreateDungeonMechanic(
                mechanic, room, group, enemyMechanicsSupported, placementContext);
            if (mechanicEntity is not null)
                yield return mechanicEntity;
        }

        if (saveData is not null)
        {
            // Room 3:9e places its watcher after Impa, Nayru, and Zelda.
            // CreateNayruHouseNpcs emits it at that exact source position.
            if (group != _nayruHouse.Record.Group ||
                room.Id != _nayruHouse.Record.Room)
            {
                foreach (RoomTileChangeWatcherDatabaseRecord record in
                    _tileChangeWatchers.GetRoomRecords(group, room.Id))
                {
                    yield return new RoomTileChangeWatcherRoomEntity(
                        record, room, saveData);
                }
            }
        }

        // ENEMY_SEEDS_ON_TREE is a main object. In room 0:78 it precedes the
        // old-lady interaction, and every controller creates its parts before
        // the remaining placed interactions receive their first update.
        foreach (SeedTreePlacementRecord record in
            _seedTrees.GetRoomRecords(group, room.Id))
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

        // Rooms 2:ea/2:eb place only the $ac family spawner at this point in
        // their source object streams. Execute its state writes and expansion
        // here, before any of the spawned actors receive their first update.
        IReadOnlyList<NpcRecord> roomNpcs =
            familyState.ResolveRoomNpcs(
                group, room.Id, saveData, runtimeState);
        if (group == 4 && room.Id is 0xe0 or 0xe1 or 0xe2 or 0xe7 or 0xe8)
        {
            foreach (IRoomEntity entity in CreateBlackTowerNpcs(
                room, roomNpcs, placementContext))
            {
                yield return entity;
            }
        }
        else if (_makuSproutRoom.MatchesRoom(group, room.Id))
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
        else if (group == _nayruHouse.Record.Group &&
            room.Id == _nayruHouse.Record.Room)
        {
            foreach (IRoomEntity entity in
                CreateNayruHouseNpcs(room, roomNpcs))
            {
                yield return entity;
            }
        }
        else if (group == _lynnaShop.Group && room.Id == _lynnaShop.Room)
        {
            foreach (IRoomEntity entity in CreateLynnaShop(room, roomNpcs))
                yield return entity;
        }
        else if (group == _vasuShop.Group && room.Id == _vasuShop.Room)
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
                            CreateNpcCharacter(record));
                        break;
                    case NpcImplementationClassification.SpecializedNative:
                        yield return CreateSpecializedNpc(record, room);
                        break;
                    case NpcImplementationClassification.EventOwned:
                        yield return new EventOwnedNpcRoomEntity(
                            CreateNpcCharacter(record));
                        break;
                    case NpcImplementationClassification.DeliberatelyUnsupported:
                        break;
                    default:
                        throw UnsupportedNpcClassification(record);
                }
            }
        }

        // Every Ages Gasha placement precedes the room's enemy pointer. In
        // 0:7b it follows all three child interactions, so emit it after the
        // placed NPC/interaction set and before parts/enemies.
        if (saveData is not null &&
            _gashaSpots.TryGetSpot(group, room.Id, out SpotRecord spot) &&
            (!saveData.IsGashaSpotPlanted(spot.SubId) ||
             saveData.GetGashaSpotKillCounter(spot.SubId) >= _gashaSpots.NutKills))
        {
            var gasha = new GashaSpotInteraction
            {
                Name = $"GashaSpot_{spot.SubId:x2}",
                ZIndex = 12
            };
            gasha.Initialize(
                _gashaSpots, spot, room, saveData, inventory,
                gashaInteractionRequested, gashaNutCaught,
                soundRequested, roomTileChanged, animationTick);
            yield return new GashaSpotRoomEntity(gasha);
        }

        foreach (GroundTreasureDatabaseRecord record in
            _groundTreasures.GetRoomRecords(group, room.Id))
        {
            if (saveData?.HasRoomFlag(
                group, room.Id, OracleSaveData.RoomFlagItem) == true)
            {
                continue;
            }
            var treasure = new GroundTreasurePickup
            {
                Name = $"GroundTreasure_{record.Order}",
                ZIndex = 12
            };
            treasure.Initialize(record, soundRequested, worldToScreen);
            yield return new GroundTreasureRoomEntity(
                treasure, groundTreasureCollectionAllowed,
                groundTreasureCollected);
        }

        foreach (IRoomEntity portal in CreateTimePortals(group, room))
            yield return portal;

        // The two non-leading Ages placements follow their room's supported
        // actors/portals and still precede the enemy pointer: 0:13 order 6
        // follows four portals plus two native interactions, while 1:25 order
        // 1 follows its construction soldier.
        foreach (SeedTreePlacementRecord record in
            _seedTrees.GetRoomRecords(group, room.Id))
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
                ZIndex = 11
            };
            maple.Initialize(
                activeGroup,
                _maple,
                encounterState,
                room,
                random,
                saveData!,
                inventory!,
                treasures,
                mapleDialogueRequested,
                dialogueOpen,
                mapleItemCollected,
                soundRequested,
                horizontalScreenShakeRequested,
                roomMusicRequested);
            yield return new MapleEncounterRoomEntity(maple);

            // checkAndSpawnMaple writes wcc85=$01. checkSkipPointer then
            // suppresses this room's entire enemy/item-drop pointer while
            // preserving every ordinary interaction emitted above.
            yield break;
        }

        var reservations = new EnemyPlacementReservations();
        int enemySlots = 0;
        int partSlots = 0;
        int killableEnemies = 0;
        foreach (RoomObjectRecord source in enemies.GetRoomObjects(group, room.Id))
        {
            if (!RoomObjectConditionMet(source, group, room))
                continue;

            EnemyObjectHandlerResolution resolution =
                enemies.EnemyHandlers.Resolve(source);
            switch (resolution.SlotPolicy)
            {
                case EnemyObjectSlotPolicy.RandomEnemy:
                    EnemyHandlerDescriptor randomHandler =
                        resolution.RequireEnemyHandler(source);
                    for (int instance = 0; instance < source.Count; instance++)
                    {
                        int killableEnemyIndex = NextKillableEnemyIndex(
                            source.Flags, ref killableEnemies);
                        if (enemyWasKilled(killableEnemyIndex))
                            continue;
                        if (enemySlots >= 16)
                            break;
                        enemySlots++;
                        if (!TryChooseRandomEnemyPosition(
                            room, source.Flags, reservations, placementContext,
                            out Vector2 position))
                        {
                            continue;
                        }
                        IRoomEntity? entity = CreateOrderedEnemy(
                            randomHandler, source, room, position, instance,
                            killableEnemyIndex);
                        if (entity is not null)
                            yield return entity;
                    }
                    break;

                case EnemyObjectSlotPolicy.FixedEnemy:
                    EnemyHandlerDescriptor fixedHandler =
                        resolution.RequireEnemyHandler(source);
                    int fixedKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags, ref killableEnemies);
                    if (enemyWasKilled(fixedKillableEnemyIndex))
                        break;
                    if (enemySlots >= 16)
                        break;
                    enemySlots++;
                    reservations.Add(source.PackedPosition);
                    IRoomEntity? fixedEntity = CreateOrderedEnemy(
                        fixedHandler, source, room,
                        new Vector2(source.X, source.Y), 0,
                        fixedKillableEnemyIndex);
                    if (fixedEntity is not null)
                        yield return fixedEntity;
                    break;

                case EnemyObjectSlotPolicy.ParameterEnemy:
                    _ = resolution.RequireEnemyHandler(source);
                    if (enemySlots < 16)
                        enemySlots++;
                    break;

                case EnemyObjectSlotPolicy.ItemDrop:
                    int itemKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags, ref killableEnemies);
                    if (enemyWasKilled(itemKillableEnemyIndex))
                        break;
                    if (enemySlots >= 16)
                        break;
                    enemySlots++;
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
                            inventory,
                            saveData);
                        yield return new ItemDropProducerRoomEntity(
                            producer, itemKillableEnemyIndex);
                    }
                    break;

                case EnemyObjectSlotPolicy.ReservingPart:
                    if (partSlots >= 16)
                        break;
                    partSlots++;
                    reservations.Add(source.PackedPosition);
                    if (source.Id == OwlStatueDatabase.PartId)
                    {
                        yield return new OwlStatueRoomEntity(
                            source,
                            _owlStatues.Record(source.SubId),
                            room,
                            owlStatueMessageRequested,
                            animationTick);
                    }
                    break;

                case EnemyObjectSlotPolicy.ParameterPart:
                    if (partSlots < 16)
                        partSlots++;
                    break;
            }
        }
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
                    record, _dungeonInteractions, saveData,
                    roomEnemyCount, treasure: null,
                    enableLinkCollisionsAndMenu);
            case DungeonObjectKind.MovingPlatform:
                return new MovingPlatformRoomEntity(
                    _dungeonVisuals.Visual("platform-05"),
                    record.Position,
                    record.SubId,
                    _dungeonInteractions.MovingPlatformCollisionRadii(
                        record.SubId),
                    _dungeonInteractions);
            case DungeonObjectKind.SpawnMovingPlatform:
                return new SpiritsGraveMovingPlatformSpawner(
                    triggerActive,
                    soundRequested,
                    _spiritsGrave.Constant("moving-platform-spawn-wait"));
            case DungeonObjectKind.TorchStairs:
                return new SpiritsGraveTorchStairs(
                    record, room, saveData, soundRequested,
                    roomTileChanged, animationTick,
                    _spiritsGrave.Constant("torch-count"),
                    _spiritsGrave.Constant("torch-tile"),
                    _spiritsGrave.Constant("solve-sound"),
                    _spiritsGrave.Constant("light-torch-sound"));
            case DungeonObjectKind.ColoredCube:
                return new ColoredCubeRoomEntity(
                    record, _dungeonVisuals.Visual("colored-cube"), room,
                    _dungeonInteractions,
                    RequireColoredCubePuzzle(puzzle, record),
                    _dungeonVisuals.CubePalettes,
                    soundRequested, roomTileChanged, animationTick);
            case DungeonObjectKind.CubeFlame:
                return new ColoredCubeFlameRoomEntity(
                    record, _dungeonVisuals.Visual("cube-flame"),
                    RequireColoredCubePuzzle(puzzle, record));
            case DungeonObjectKind.CubeLightSensor:
            case DungeonObjectKind.CubeTriggerSensor:
                return new ColoredCubeSensorRoomEntity(
                    record, room, RequireColoredCubePuzzle(puzzle, record),
                    setTrigger, soundRequested);
            case DungeonObjectKind.GiantGhini:
                var giantGhini = new GiantGhiniBoss
                {
                    Name = "GiantGhini",
                    ZIndex = 10
                };
                giantGhini.Initialize(
                    _dungeonBosses.Enemy(0x70), room, record.Position, random,
                    soundRequested, bossShuttersClosed,
                    disableLinkCollisionsAndMenu,
                    () => roomMusicRequested(record.Group, record.Room));
                return new GiantGhiniBossRoomEntity(
                    giantGhini, BossEntryDirection(placementContext));
            case DungeonObjectKind.PumpkinHead:
                ImportedEnemyDefinition pumpkin = _dungeonBosses.Enemy(0x78);
                return new PumpkinHeadBossRoomEntity(
                    new PumpkinHeadBoss(
                        pumpkin, room, record.Position, random, soundRequested,
                        bossShuttersClosed, screenShakeRequested,
                        disableLinkCollisionsAndMenu,
                        () => roomMusicRequested(record.Group, record.Room),
                        _dungeonBosses.Constant("pumpkin-body-palette"),
                        _dungeonBosses.Constant("pumpkin-ghost-palette")),
                    pumpkin.DamageQuarters,
                    BossEntryDirection(placementContext));
            case DungeonObjectKind.Essence:
                return new DungeonEssence(
                    record,
                    _dungeonVisuals.Visual("eternal-spirit"),
                    _dungeonVisuals.Visual("essence-pedestal"),
                    _dungeonVisuals.Visual("essence-glow"),
                    _dungeonVisuals.Visual("energy-bead"),
                    room,
                    saveData?.HasRoomFlag(
                        record.Group, record.Room, OracleSaveData.RoomFlagItem) == true,
                    animationTick,
                    random,
                    dungeonEssenceTriggered,
                    new DungeonEssenceDefinition(
                        0,
                        _spiritsGrave.EssenceMessage,
                        new Warp(
                            4, 0x11, -1, 0, 0, 0,
                            0x8d, 0x26, 0, 1)));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record), record, "Unsupported Spirit's Grave object.");
        }
    }

    private MinecartRoomEntity CreateMinecart(
        ActiveMinecart cart,
        OracleRoomData room) =>
        new(
            cart,
            room,
            _dungeonInteractions,
            runtimeState,
            _dungeonVisuals.Visual("minecart"),
            soundRequested);

    private IEnumerable<IRoomEntity> CreateMinecartShutterControllers(
        OracleRoomData room)
    {
        // replaceShutterForLinkEntering creates these subids only when
        // TILESETFLAG_DUNGEON is set in the Ages engine.
        if ((room.TilesetFlags & 0x08) == 0)
            yield break;

        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            int packedPosition = (y << 4) | x;
            Vector2 point = PointForPackedPosition(packedPosition);
            int originalTile = room.GetOriginalMetatile(point);
            if (originalTile is < DungeonShutterEntry.FirstMinecartShutterTile or
                > DungeonShutterEntry.LastMinecartShutterTile)
            {
                continue;
            }
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
            _dungeonMechanics,
            worldToScreen,
            animationTick,
            soundRequested);

    private IRoomEntity? CreateWingDungeonInteraction(
        DungeonObjectRecord record,
        OracleRoomData room,
        ColoredCubePuzzleState? puzzle,
        EnemyPlacementContext placementContext)
    {
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
                    room,
                    _dungeonInteractions.Constant(
                        record.Kind == DungeonObjectKind.FloorPatternKey
                            ? "red-toggle-floor"
                            : "red-pushable-block"),
                    [
                        _wingDungeon.Pattern(record.Kind, 0),
                        _wingDungeon.Pattern(record.Kind, 1),
                        _wingDungeon.Pattern(record.Kind, 2)
                    ],
                    CreateFallingSmallKeyRequest(record));
            case DungeonObjectKind.ToggleFloor:
                return new ToggleFloorRoomEntity(
                    room, _dungeonInteractions, soundRequested,
                    roomTileChanged, animationTick);
            case DungeonObjectKind.ColoredCube:
                return new ColoredCubeRoomEntity(
                    record, _dungeonVisuals.Visual("colored-cube"), room,
                    _dungeonInteractions,
                    RequireColoredCubePuzzle(puzzle, record),
                    _dungeonVisuals.CubePalettes,
                    soundRequested, roomTileChanged, animationTick);
            case DungeonObjectKind.CubeFlame:
                return new ColoredCubeFlameRoomEntity(
                    record, _dungeonVisuals.Visual("cube-flame"),
                    RequireColoredCubePuzzle(puzzle, record));
            case DungeonObjectKind.CubeLightSensor:
                return new ColoredCubeSensorRoomEntity(
                    record, room, RequireColoredCubePuzzle(puzzle, record),
                    setTrigger, soundRequested);
            case DungeonObjectKind.CubeSwitchSensor:
            case DungeonObjectKind.RedFloorTrigger:
            case DungeonObjectKind.FloorSwitchBit:
            case DungeonObjectKind.CubeColorSource:
            case DungeonObjectKind.RedFlameTrigger:
                return new WingDungeonStateController(
                    record, room, _dungeonInteractions,
                    puzzle ?? new ColoredCubePuzzleState(),
                    runtimeState, setTrigger);
            case DungeonObjectKind.SwitchTileToggler:
                return new SwitchTileTogglerRoomEntity(
                    record, room, _dungeonInteractions, runtimeState,
                    roomTileChanged, animationTick);
            case DungeonObjectKind.MinecartGate:
                return new MinecartGateRoomEntity(
                    record, room, runtimeState,
                    _dungeonVisuals.Visual("minecart-gate"), soundRequested,
                    roomTileChanged, animationTick);
            case DungeonObjectKind.EnemyChest:
                return new EnemyClearChestRoomEntity(
                    record, room, _dungeonInteractions, roomEnemyCount,
                    soundRequested, roomTileChanged, animationTick);
            case DungeonObjectKind.EnemySmallKey:
                return new DungeonRewardRoomEntity(
                    record, _dungeonInteractions, saveData, roomEnemyCount,
                    CreateFallingSmallKeyRequest(record),
                    enableLinkCollisionsAndMenu);
            case DungeonObjectKind.MinibossReward:
                return new DungeonRewardRoomEntity(
                    record, _dungeonInteractions, saveData,
                    roomEnemyCount, treasure: null,
                    enableLinkCollisionsAndMenu);
            case DungeonObjectKind.FloorColorChanger:
                return new FloorColorChangerRoomEntity(
                    record, room, _dungeonInteractions, random,
                    roomTileChanged, animationTick);
            case DungeonObjectKind.SidePlatform:
                return new MovingSideScrollPlatformRoomEntity(
                    record,
                    _dungeonInteractions.SidePlatform(record.SubId),
                    _dungeonVisuals.Visual("moving-side-platform"));
            case DungeonObjectKind.CircularSidePlatform:
                return new CircularSideScrollPlatformRoomEntity(
                    record,
                    _dungeonVisuals.Visual("circular-side-platform"));
            case DungeonObjectKind.HeadThwomp:
                ImportedEnemyDefinition headThwomp =
                    _dungeonBosses.Enemy(0x79);
                var headThwompBoss = new HeadThwompBoss();
                headThwompBoss.Initialize(
                    headThwomp,
                    room,
                    record.Position,
                    random,
                    _dungeonBosses.HeadThwompPalettes,
                    soundRequested,
                    screenShakeRequested,
                    disableLinkCollisionsAndMenu,
                    () => roomMusicRequested(record.Group, record.Room),
                    animationTick);
                return new HeadThwompBossRoomEntity(headThwompBoss);
            case DungeonObjectKind.Swoop:
                ImportedEnemyDefinition swoopRecord =
                    _dungeonBosses.Enemy(0x71);
                var swoop = new SwoopBoss();
                swoop.Initialize(
                    swoopRecord,
                    room,
                    record.Position,
                    random,
                    soundRequested,
                    bossShuttersClosed,
                    screenShakeRequested,
                    disableLinkCollisionsAndMenu,
                    enableLinkCollisionsAndMenu,
                    () => roomMusicRequested(record.Group, record.Room),
                    nativeBossDialogueRequested,
                    dialogueOpen,
                    animationTick,
                    _wingDungeon.SwoopMessage);
                return new SwoopBossRoomEntity(
                    swoop, BossEntryDirection(placementContext));
            case DungeonObjectKind.BossReward:
                return new DungeonRewardRoomEntity(
                    record, _dungeonInteractions, saveData, roomEnemyCount,
                    CreateDungeonBossRewardRequest(record),
                    enableLinkCollisionsAndMenu);
            case DungeonObjectKind.Essence:
                return new DungeonEssence(
                    record,
                    _dungeonVisuals.Visual("ancient-wood"),
                    _dungeonVisuals.Visual("essence-pedestal"),
                    _dungeonVisuals.Visual("essence-glow"),
                    _dungeonVisuals.Visual("energy-bead"),
                    room,
                    saveData?.HasRoomFlag(
                        record.Group,
                        record.Room,
                        OracleSaveData.RoomFlagItem) == true,
                    animationTick,
                    random,
                    dungeonEssenceTriggered,
                    new DungeonEssenceDefinition(
                        1,
                        _wingDungeon.EssenceMessage,
                        new Warp(
                            4, 0x38, -1, 0, 0, 1,
                            0x83, 0x25, 0, 1)));
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
            SpawnMode = 0,
            GrabMode = 2
        };
        return new ImmediateDungeonRewardRoomEntity(record, request);
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
            SpawnMode = 2,
            GrabMode = 2,
            SpawnDelayFrames =
                _dungeonInteractions.Constant("falling-key-spawn-delay"),
            BounceCount = 2,
            Gravity = 0x10,
            BounceSpeed = -0xaa,
            SpawnSound = OracleSoundEngine.SndSolvePuzzle,
            LandingSound = OracleSoundEngine.SndDropEssence,
            InitialZAboveScreen = true
        };

    private static GroundTreasureGrantRequest CreateDungeonBossRewardRequest(
        DungeonObjectRecord record) =>
        new(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            "TREASURE_OBJECT_HEART_CONTAINER_00",
            record.Source)
        {
            SpawnMode = 0,
            GrabMode = 2
        };

    private static bool HasColoredCubeState(
        IReadOnlyList<DungeonObjectRecord> records)
    {
        foreach (DungeonObjectRecord record in records)
        {
            if (record.Kind is DungeonObjectKind.ColoredCube or
                DungeonObjectKind.CubeFlame or
                DungeonObjectKind.CubeLightSensor or
                DungeonObjectKind.CubeSwitchSensor or
                DungeonObjectKind.CubeColorSource or
                DungeonObjectKind.RedFlameTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private static Vector2I BossEntryDirection(
        EnemyPlacementContext placementContext) =>
        placementContext.Kind == EnemyPlacementEntryKind.Scrolling
            ? placementContext.ScrollDirection
            : Vector2I.Zero;

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
            SpawnMode = falling ? 2 : 0,
            GrabMode = 2,
            SpawnDelayFrames = falling ? 40 : 0,
            BounceCount = falling ? 2 : 0,
            Gravity = falling ? 0x10 : 0,
            BounceSpeed = falling ? -0xaa : 0,
            SpawnSound = falling ? OracleSoundEngine.SndSolvePuzzle : 0,
            LandingSound = falling ? OracleSoundEngine.SndDropEssence : 0,
            InitialZAboveScreen = falling
        };
        return new DungeonRewardRoomEntity(
            record, _dungeonInteractions, saveData, roomEnemyCount, request,
            enableLinkCollisionsAndMenu);
    }

    private static ColoredCubePuzzleState RequireColoredCubePuzzle(
        ColoredCubePuzzleState? puzzle,
        DungeonObjectRecord record) =>
        puzzle ?? throw new InvalidOperationException(
            $"{record.Source} is missing its room-local rotating-cube state.");

    private bool DungeonObjectConditionMet(
        DungeonObjectRecord record) => record.Predicate switch
    {
        DungeonObjectCondition.Always => true,
        DungeonObjectCondition.ItemClear =>
            saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlagItem) != true,
        DungeonObjectCondition.Flag80Clear =>
            saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlag80) != true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(record), record, "Unknown dungeon-object predicate.")
    };

    private IRoomEntity? CreateDungeonMechanic(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        int group,
        bool enemyMechanicsSupported,
        EnemyPlacementContext placementContext)
    {
        if (record.Id == 0x05)
        {
            return new DungeonSwitchRoomEntity(
                record, room, _dungeonMechanics, runtimeState,
                animationTick, roomTileChanged, soundRequested);
        }
        if (record.Id == 0x09)
        {
            return new GroundButtonRoomEntity(
                record, room, _dungeonMechanics, setTrigger,
                animationTick, soundRequested);
        }
        if (record.Id is 0x20 or 0x21)
        {
            return new TriggerChestRoomEntity(
                record, room, _dungeonMechanics, triggerState,
                () => saveData?.HasRoomFlag(
                    group, room.Id, OracleSaveData.RoomFlagItem) == true,
                animationTick, soundRequested);
        }
        if (record.Id == 0x13 && !enemyMechanicsSupported)
            return null;
        return record.Id switch
        {
            0x13 => new PushBlockTriggerRoomEntity(
                record, room, _dungeonMechanics,
                roomEnemyCount, animationTick),
            0x1e => new DungeonDoorRoomEntity(
                record, room, _dungeonMechanics, roomEnemyCount,
                triggerActive, worldToScreen, animationTick,
                soundRequested, placementContext, enemyMechanicsSupported),
            _ => throw new InvalidOperationException(
                $"Unsupported dungeon interaction ${record.Id:x2}:" +
                $"${record.SubId:x2} in room {group:x1}:{room.Id:x2}.")
        };
    }

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
                    _dungeonEntrances.Entry(record.Dungeon),
                    _dungeonEntrances,
                    runtimeState,
                    placementContext.Kind == EnemyPlacementEntryKind.ScreenWarp,
                    dungeon =>
                    {
                        if (dungeon == 2)
                        {
                            MinecartRuntimeState.Reset(
                                runtimeState, _wingDungeon.Minecarts);
                        }
                    },
                    dungeonEntranceTriggered);

            case DungeonEntranceInteractionDatabaseObjectKind.EyeSpawner:
                return new StatueEyeballSpawnerRoomEntity(room, _dungeonEntrances);

            case DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal:
                var portal = new MinibossPortal();
                portal.Initialize(_dungeonEntrances);
                return new MinibossPortalRoomEntity(
                    portal, record, _dungeonEntrances, saveData,
                    roomWarpRequested, soundRequested);

            default:
                throw new InvalidOperationException(
                    $"Unsupported shared dungeon interaction kind in {record.Source}.");
        }
    }

    private IRoomEntity? CreateOrderedEnemy(
        EnemyHandlerDescriptor handler,
        RoomObjectRecord source,
        OracleRoomData room,
        Vector2 position,
        int instance,
        int killableEnemyIndex)
    {
        if (!handler.SupportsOrderedConstruction)
            return null;

        EnemyCombatSourceDescriptor combatSource =
            handler.CombatSource(source, killableEnemyIndex);

        switch (handler.Handler)
        {
            case EnemyHandlerKind.Keese:
                if (!enemies.TryGetKeeseDefinition(
                    source, out EnemyDatabaseEnemyRecord keeseRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var keese = new KeeseCharacter
                {
                    Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                keese.Initialize(keeseRecord, room, position, random);
                return new KeeseRoomEntity(
                    keese, combatSource, soundRequested);

            case EnemyHandlerKind.Crow:
                if (!enemies.TryGetCrowDefinition(
                    source, out CrowRecord crowRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var crow = new CrowCharacter
                {
                    Name = $"Crow_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                crow.Initialize(crowRecord, room, position, random);
                return new CrowRoomEntity(
                    crow, combatSource, soundRequested);

            case EnemyHandlerKind.Octorok:
                if (!enemies.TryGetOctorokDefinition(
                    source, out OctorokRecord octorokRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var octorok = new OctorokCharacter
                {
                    Name = $"Octorok_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                octorok.Initialize(octorokRecord, room, position, random);
                return new OctorokRoomEntity(
                    octorok, combatSource, soundRequested);

            case EnemyHandlerKind.Stalfos:
                if (!enemies.TryGetStalfosDefinition(
                    source, out StalfosRecord stalfosRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var stalfos = new StalfosCharacter
                {
                    Name = $"Stalfos_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                stalfos.Initialize(stalfosRecord, room, position, random);
                return new StalfosRoomEntity(
                    stalfos, combatSource, soundRequested);

            case EnemyHandlerKind.Zol:
                if (!enemies.TryGetZolDefinition(
                    source, out ZolRecord zolRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var zol = new ZolCharacter
                {
                    Name = $"Zol_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                zol.Initialize(zolRecord, room, position, random);
                return new ZolRoomEntity(
                    zol, combatSource, soundRequested);

            case EnemyHandlerKind.BoomerangMoblin:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition moblinRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var moblin = new BoomerangMoblinCharacter
                {
                    Name = $"BoomerangMoblin_{source.Order}_{instance}",
                    ZIndex = 10
                };
                moblin.Initialize(moblinRecord, room, position, random);
                return new BoomerangMoblinRoomEntity(
                    moblin, combatSource, soundRequested);

            case EnemyHandlerKind.ArrowMoblin:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition arrowMoblinRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var arrowMoblin = new ArrowMoblinCharacter
                {
                    Name = $"ArrowMoblin_{source.Order}_{instance}",
                    ZIndex = 10
                };
                arrowMoblin.Initialize(
                    arrowMoblinRecord, room, position, random);
                return new ArrowMoblinRoomEntity(
                    arrowMoblin, combatSource, soundRequested);

            case EnemyHandlerKind.MaskedMoblin:
                var maskedMoblin = new MaskedMoblinCharacter
                {
                    Name =
                        $"MaskedMoblin_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                maskedMoblin.Initialize(
                    enemies.MaskedMoblin, room, position, random);
                return new MaskedMoblinRoomEntity(
                    maskedMoblin, combatSource, soundRequested);

            case EnemyHandlerKind.Rope:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition ropeRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var rope = new RopeCharacter
                {
                    Name = $"Rope_{source.Order}_{instance}",
                    ZIndex = 10
                };
                rope.Initialize(ropeRecord, room, position, random);
                return new RopeRoomEntity(
                    rope, combatSource, soundRequested);

            case EnemyHandlerKind.Spark:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition sparkRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spark = new SparkCharacter
                {
                    Name = $"Spark_{source.Order}_{instance}",
                    ZIndex = 10
                };
                spark.Initialize(sparkRecord, room, position);
                return new SparkRoomEntity(
                    spark, combatSource, soundRequested);

            case EnemyHandlerKind.Whisp:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition whispRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var whisp = new WhispCharacter
                {
                    Name = $"Whisp_{source.Order}_{instance}",
                    ZIndex = 10
                };
                whisp.Initialize(whispRecord, room, position, random);
                return new WhispRoomEntity(
                    whisp, combatSource, soundRequested);

            case EnemyHandlerKind.Thwomp:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition thwompRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var thwomp = new ThwompCharacter
                {
                    Name = $"Thwomp_{source.Order}_{instance}",
                    ZIndex = 10
                };
                thwomp.Initialize(thwompRecord, room, position);
                return new ThwompRoomEntity(
                    thwomp, combatSource, soundRequested);

            case EnemyHandlerKind.Peahat:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition peahatRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var peahat = new PeahatCharacter
                {
                    Name = $"Peahat_{source.Order}_{instance}",
                    ZIndex = 10
                };
                peahat.Initialize(peahatRecord, room, position, random);
                return new PeahatRoomEntity(
                    peahat, combatSource, soundRequested);

            case EnemyHandlerKind.ColorChangingGel:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition colorGelRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var colorGel = new ColorChangingGelCharacter
                {
                    Name =
                        $"ColorChangingGel_{source.Order}_{instance}",
                    ZIndex = 10
                };
                colorGel.Initialize(
                    colorGelRecord, room, position, random);
                return new ColorChangingGelRoomEntity(
                    colorGel, combatSource, soundRequested);

            case EnemyHandlerKind.SwordEnemy:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition swordEnemyRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var swordEnemy = new SwordEnemyCharacter
                {
                    Name =
                        $"SwordEnemy_{source.Id:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                swordEnemy.Initialize(
                    swordEnemyRecord, room, position, random);
                return new SwordEnemyRoomEntity(
                    swordEnemy, combatSource, soundRequested);

            case EnemyHandlerKind.Ghini:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition ghiniRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var ghini = new GhiniCharacter
                {
                    Name = $"Ghini_{source.Order}_{instance}",
                    ZIndex = 10
                };
                ghini.Initialize(ghiniRecord, room, position, random);
                return new GhiniRoomEntity(
                    ghini, combatSource, soundRequested);

            case EnemyHandlerKind.SpikedBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition spikedBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spikedBeetle = new SpikedBeetleCharacter
                {
                    Name =
                        $"SpikedBeetle_{source.Order}_{instance}",
                    ZIndex = 10
                };
                spikedBeetle.Initialize(
                    spikedBeetleRecord,
                    room,
                    position,
                    random,
                    soundRequested);
                return new SpikedBeetleRoomEntity(
                    spikedBeetle, combatSource, soundRequested);

            case EnemyHandlerKind.SpinyBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition spinyBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var spinyBeetle = new SpinyBeetleCharacter
                {
                    Name =
                        $"SpinyBeetle_{source.SubId:x2}_{source.Order}_{instance}",
                    ZIndex = 10
                };
                spinyBeetle.Initialize(
                    spinyBeetleRecord,
                    room,
                    position,
                    random,
                    applyThrownObjectHit);
                return new SpinyBeetleRoomEntity(
                    spinyBeetle, combatSource, soundRequested);

            case EnemyHandlerKind.Wallmaster:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition wallmasterRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var wallmaster = new WallmasterCharacter
                {
                    Name = $"Wallmaster_{source.Order}_{instance}",
                    ZIndex = 10
                };
                wallmaster.Initialize(
                    wallmasterRecord, room, position, source.Y);
                (int destinationGroup, int destinationRoom) =
                    ResolveWallmasterDestination(source);
                return new WallmasterRoomEntity(
                    wallmaster, soundRequested, roomWarpRequested,
                    source.Group, source.Room,
                    destinationGroup, destinationRoom,
                    combatSource);

            case EnemyHandlerKind.HardhatBeetle:
                if (!enemies.TryGetImportedEnemyDefinition(
                    source, out ImportedEnemyDefinition hardhatBeetleRecord))
                {
                    throw MissingEnemyDefinition(handler, source);
                }
                var hardhatBeetle = new HardhatBeetleCharacter
                {
                    Name =
                        $"HardhatBeetle_{source.Order}_{instance}",
                    ZIndex = 10
                };
                hardhatBeetle.Initialize(
                    hardhatBeetleRecord, room, position);
                return new HardhatBeetleRoomEntity(
                    hardhatBeetle, combatSource, soundRequested);

            case EnemyHandlerKind.Gel:
                return CreateGel(
                    new GelSpawn(
                        position, $"RoomGel_{source.Order}_{instance}"),
                    room, combatSource);

            default:
                throw new InvalidOperationException(
                    $"{handler.Source} classified {handler.EnemyName} " +
                    $"${handler.Id:x2}:${handler.SubId:x2} as an ordered " +
                    $"handler, but '{handler.Handler}' has no factory path.");
        }
    }

    private static InvalidOperationException MissingEnemyDefinition(
        EnemyHandlerDescriptor handler,
        RoomObjectRecord source) => new(
            $"{source.Source} resolves through {handler.Source} to " +
            $"'{handler.Handler}', but its typed definition is unavailable.");

    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn switch
    {
        OctorokRockSpawn rock => CreateRock(rock, room),
        MaskedMoblinSpawn moblin => CreateMaskedMoblin(moblin, room),
        EnemyArrowSpawn arrow => CreateEnemyArrow(arrow, room),
        MoblinBoomerangSpawn boomerang => CreateMoblinBoomerang(boomerang, room),
        GelSpawn gel => CreateGel(gel, room),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        BossDeathExplosionSpawn explosion => CreateBossDeathExplosion(explosion),
        BossShadowSpawn shadow => CreateBossShadow(shadow),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        ItemDropSpawn drop => CreateItemDrop(drop, room),
        ShovelDebrisSpawn debris => CreateShovelDebris(debris),
        GrassDebrisSpawn debris => CreateGrassDebris(debris),
        RockDebrisSpawn debris => CreateRockDebris(debris),
        EmberSeedSpawn seed => CreateEmberSeed(seed, room),
        BombSpawn bomb => CreateBomb(bomb, room),
        OwlStatueSparkleSpawn sparkle => CreateOwlStatueSparkle(sparkle),
        PuzzlePuffSpawn puff => CreatePuzzlePuff(puff),
        EnemySplashSpawn splash => CreateEnemySplash(splash),
        FallingDownHoleSpawn fall => CreateFallingDownHole(fall),
        DungeonKeyUseSpawn key => CreateDungeonKeyUse(key),
        OverworldKeyUseSpawn key => CreateOverworldKeyUse(key),
        EraInfoSpawn era => CreateEraInfo(era),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        GroundTreasureSpawn treasure => CreateGroundTreasure(treasure.Record),
        GroundTreasureGrantSpawn treasure =>
            CreateGroundTreasure(treasure.Request.Resolve(treasures)),
        MapleDroppedItemSpawn item => CreateMapleDroppedItem(item, room),
        LightableTorchSpawn torch => CreateLightableTorch(torch, room),
        Room148DebrisSpawn debris => CreateRoom148Debris(debris),
        ShootingGalleryGameControllerSpawn controller =>
            CreateShootingGalleryController(controller, room),
        ShootingGalleryBallSpawn ball =>
            CreateShootingGalleryBall(ball, room),
        ShootingGalleryTargetDebrisSpawn debris =>
            CreateShootingGalleryTargetDebris(debris),
        SwordBeamSpawn beam => CreateSwordBeam(beam, room),
        SwordBeamClinkSpawn clink => CreateSwordBeamClink(clink),
        EnemyClinkSpawn clink => CreateEnemyClink(clink),
        StatueEyeballSpawn eye => CreateStatueEyeball(eye),
        MovingPlatformSpawn platform =>
            CreateMovingPlatform(platform),
        MinibossPortalSpawn => CreateMinibossPortal(room),
        GiantGhiniChildSpawn child => CreateGiantGhiniChild(child, room),
        PumpkinHeadProjectileSpawn projectile =>
            CreatePumpkinHeadProjectile(projectile, room),
        HeadThwompProjectileSpawn projectile =>
            CreateHeadThwompProjectile(projectile, room),
        MinecartShutterOpenSpawn shutter => CreateMinecartShutter(
            shutter.PackedPosition,
            shutter.ClosedTile,
            oneShotOpener: true,
            room),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

    private IRoomEntity CreateSeedOnTree(
        SeedTreeController controller,
        SeedTreeTypeRecord type,
        Vector2 position,
        int index)
    {
        var seed = new SeedOnTree
        {
            Name = $"SeedOnTree_{index}",
            ZIndex = 10
        };
        seed.Initialize(
            _seedTrees,
            controller,
            type,
            position,
            index,
            inventory,
            seedTreeMessageRequested,
            dialogueOpen,
            soundRequested);
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
        tree.Initialize(_seedTrees, record, room, runtimeState);
        if (!tree.HasActiveSeeds)
        {
            tree.Free();
            yield break;
        }

        yield return new SeedTreeControllerRoomEntity(tree);
        SeedTreeTypeRecord type = _seedTrees.Type(record.SeedType);
        for (int index = 0; index < _seedTrees.SeedCount; index++)
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
        new MovingPlatformRoomEntity(
            _dungeonVisuals.Visual(
                (spawn.SubId >> 3) == 0 ? "platform-05" : "platform-09"),
            spawn.Position,
            spawn.SubId,
            _dungeonInteractions.MovingPlatformCollisionRadii(spawn.SubId),
            _dungeonInteractions);

    private IRoomEntity CreateGiantGhiniChild(
        GiantGhiniChildSpawn spawn,
        OracleRoomData room)
    {
        var child = new GiantGhiniChild
        {
            Name = $"GiantGhiniChild_{spawn.Index}",
            ZIndex = 10
        };
        child.Initialize(
            _dungeonBosses.Enemy(0x3f), spawn.Owner, room, spawn.Index);
        return new GiantGhiniChildRoomEntity(
            child, soundRequested);
    }

    private IRoomEntity CreatePumpkinHeadProjectile(
        PumpkinHeadProjectileSpawn spawn,
        OracleRoomData room) =>
        new PumpkinHeadProjectileRoomEntity(
            new PumpkinHeadProjectile(
                _dungeonVisuals.Visual("pumpkin-projectile"),
                room,
                spawn.Position,
                spawn.Angle));

    private IRoomEntity CreateHeadThwompProjectile(
        HeadThwompProjectileSpawn spawn,
        OracleRoomData room)
    {
        DungeonInteractionVisual visual = _dungeonVisuals.Visual(
            spawn.Kind == HeadThwompProjectileKind.Fireball
                ? "head-thwomp-fireball"
                : "head-thwomp-circular-projectile");
        return new HostileProjectileRoomEntity<HeadThwompProjectile>(
            new HeadThwompProjectile(spawn, visual, room));
    }

    private IRoomEntity CreateMinibossPortal(OracleRoomData room)
    {
        foreach (PlacementRecord record in
            _dungeonEntrances.GetRoomRecords(4, room.Id))
        {
            if (record.Kind ==
                DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal)
            {
                return CreateSharedDungeonInteraction(
                    record, room, EnemyPlacementContext.Unrestricted);
            }
        }
        throw new InvalidOperationException(
            $"Spirit's Grave room 4:{room.Id:x2} has no miniboss portal placement.");
    }

    private StatueEyeballRoomEntity CreateStatueEyeball(StatueEyeballSpawn spawn)
    {
        var eye = new StatueEyeball();
        eye.Initialize(spawn.Position, _dungeonEntrances);
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
            ZIndex = 11
        };
        return new MoblinBoomerangRoomEntity(boomerang);
    }

    private IRoomEntity CreateSpecializedNpc(
        NpcRecord record,
        OracleRoomData room)
    {
        RequireNpcImplementation(
            record, NpcImplementationClassification.SpecializedNative);

        if (record.Group == _shootingGallery.Record.Group &&
            record.Room == _shootingGallery.Record.Room &&
            record.Id == _shootingGallery.Record.InteractionId &&
            record.SubId == _shootingGallery.Record.SubId)
        {
            var keeper = new ShootingGalleryCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            keeper.InitializeShootingGallery(record);
            return new ShootingGalleryNpcRoomEntity(keeper);
        }
        if (record.Group == _comedian.Record.Group &&
            record.Room == _comedian.Record.Room &&
            record.Id == _comedian.Record.InteractionId &&
            record.SubId == _comedian.Record.SubId)
        {
            var comedian = new ComedianCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            comedian.InitializeComedian(record, _comedian.Record);
            return new ComedianRoomEntity(comedian);
        }
        if (record.Group == _maskSalesman.Record.Group &&
            record.Room == _maskSalesman.Record.Room &&
            record.Id == _maskSalesman.Record.InteractionId &&
            record.SubId == _maskSalesman.Record.SubId)
        {
            var salesman = new MaskSalesmanCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            salesman.InitializeMaskSalesman(record, _maskSalesman.Record);
            return new MaskSalesmanRoomEntity(salesman);
        }

        bool isOverworldPoe =
            record.Group == _poe.Record.Group &&
            record.Room == _poe.Record.Room &&
            record.Var03 is 0x00 or 0x02;
        bool isTombPoe =
            record.Group == _poe.Record.TombGroup &&
            record.Room == _poe.Record.TombRoom &&
            record.Var03 == 0x01;
        if ((isOverworldPoe || isTombPoe) &&
            record.Id == _poe.Record.InteractionId &&
            record.SubId == _poe.Record.SubId)
        {
            var poe = new PoeCharacter
            {
                Name =
                    $"Npc_{record.Id:x2}_{record.SubId:x2}_{record.Var03:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            poe.InitializePoe(record, _poe.Record);
            return new PoeRoomEntity(poe);
        }
        if (record is
            {
                Group: 2,
                Room: 0x2f,
                Id: 0x55,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            var postman = new PostmanCharacter
            {
                Name = "Npc_55_00",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            postman.InitializePostman(record);
            return new PostmanRoomEntity(postman);
        }
        if (record is
            {
                Group: 2,
                Room: 0x3e,
                Id: 0x5b,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            var toiletHand = new ToiletHandCharacter
            {
                Name = "Npc_5b_00",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            toiletHand.InitializeToiletHand(
                record, _toiletHand.Record);
            return new ToiletHandRoomEntity(toiletHand);
        }

        NpcCharacter npc = CreateNpcCharacter(record);
        if (_stoneRabbit.Matches(record))
        {
            return new StoneRabbitRoomEntity(npc, _stoneRabbit);
        }
        if (_businessScrub.Matches(record))
        {
            return new BusinessScrubRoomEntity(
                npc,
                _businessScrub,
                room,
                animationTick(),
                roomTileChanged);
        }
        if (_room20e.Matches(record))
        {
            return new Room20eNpcRoomEntity(
                npc, _room20e, saveData);
        }
        if (_troyHouse.Matches(record))
        {
            if (saveData is null)
            {
                throw new InvalidOperationException(
                    $"{NpcSource(record)} requires save data for its " +
                    "specialized Troy interaction.");
            }
            return new TroyHouseRoomEntity(
                npc, _troyHouse, saveData, random);
        }
        if (record is { Id: 0x28, SubId: 0x00 })
            return new RunningBipinRoomEntity(
                npc, familyState.RunningBipin);
        if (record is { Id: 0x28, SubId: 0x0a })
            return new PastBipinRoomEntity(npc);
        if (record is
            {
                Group: 0,
                Room: 0x83,
                Id: 0xd5,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            return new GreatFairyRoomEntity(npc, soundRequested);
        }
        if (record is
            {
                Group: 0,
                Room: 0x5d,
                Id: 0xcb,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            return new SpecializedNpcRoomEntity(npc);
        }
        if (record.Group == 2 &&
            record.Room is 0xea or 0xeb &&
            record.Id is 0x28 or 0x2b or 0x35)
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
            _shootingGallery,
            spawn.Session,
            room,
            random,
            soundRequested,
            animationTick);
        return new ShootingGalleryGameControllerRoomEntity(controller);
    }

    private IRoomEntity CreateShootingGalleryBall(
        ShootingGalleryBallSpawn spawn,
        OracleRoomData room)
    {
        var ball = new ShootingGalleryBall
        {
            Name = "ShootingGalleryBall",
            ZIndex = 10
        };
        ball.Initialize(
            _shootingGallery,
            spawn.Session,
            room,
            random,
            spawn.Position,
            soundRequested,
            animationTick);
        return new ShootingGalleryBallRoomEntity(ball);
    }

    private IRoomEntity CreateShootingGalleryTargetDebris(
        ShootingGalleryTargetDebrisSpawn spawn)
    {
        var debris = new ShootingGalleryTargetDebris
        {
            Name = "ShootingGalleryTargetDebris",
            ZIndex = 10
        };
        debris.Initialize(_shootingGallery.Debris, spawn);
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
            if (!_nayruHouse.Matches(record))
            {
                throw new InvalidOperationException(
                    $"{NpcSource(record)} is not part of room 3:9e's " +
                    "imported native interaction set.");
            }
            if (record is { Id: 0x4f, SubId: 0x00 })
                impas.Add(record);
            else if (record is { Id: 0x36, SubId: 0x0b })
                nayru = record;
            else if (record is { Id: 0xad, SubId: 0x07 })
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

        if (saveData is null)
            yield break;
        IReadOnlyList<RoomTileChangeWatcherDatabaseRecord> watchers =
            _tileChangeWatchers.GetRoomRecords(
                _nayruHouse.Record.Group, _nayruHouse.Record.Room);
        if (watchers is not [{ Order: 3 }])
        {
            throw new InvalidOperationException(
                "Room 3:9e requires one source-order-3 tile-change watcher.");
        }
        yield return new RoomTileChangeWatcherRoomEntity(
            watchers[0], room, saveData);
    }

    private NayruHouseNpcRoomEntity CreateNayruHouseNpc(
        NpcRecord record,
        OracleRoomData room) => new(
            CreateNpcCharacter(record),
            _nayruHouse,
            room,
            animationTick);

    private static NpcCharacter CreateNpcCharacter(NpcRecord record)
    {
        var npc = new NpcCharacter
        {
            Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
            ZIndex = NpcCharacter.BehindLinkZIndex
        };
        npc.Initialize(record);
        return npc;
    }

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
        if (saveData is null)
        {
            throw new InvalidOperationException(
                "Room 1:38 Maku Sprout interactions require save data.");
        }
        if (records.Count != 1 ||
            !_makuSproutRoom.MatchesSprout(records[0]))
        {
            throw new InvalidOperationException(
                "Room 1:38 requires exactly one imported placed " +
                "INTERAC_MAKU_SPROUT $88:$00 before its conditional statue.");
        }

        // Preserve group1Map38ObjectData order: the sprout is first, then the
        // conditional $6b:$15 Link statue.
        yield return new MakuSproutRoomEntity(
            CreateNpcCharacter(records[0]),
            _makuSproutRoom,
            saveData);
        if (!saveData.HasGlobalFlag(_makuSproutRoom.Record.FinishedFlag))
            yield break;

        yield return new MakuLinkStatueRoomEntity(
            CreateNpcCharacter(
                _makuSproutRoom.Record.CreateStatueNpcRecord()),
            _makuSproutRoom,
            room,
            animationTick);
    }

    private IEnumerable<IRoomEntity> CreateRoom148Npcs(
        IReadOnlyList<NpcRecord> records)
    {
        bool foundWorker = false;
        foreach (NpcRecord record in records)
        {
            if (record is { Id: 0x57, SubId: 0x00 })
            {
                RequireNpcImplementation(
                    record,
                    NpcImplementationClassification.SpecializedNative);
                if (foundWorker)
                    throw new InvalidOperationException(
                        "Room 1:48 contains more than one pickaxe worker $57:$00.");
                foundWorker = true;
                NpcCharacter npc = CreateNpcCharacter(record);
                PickaxeRecord pickaxe = _room148.Record;
                npc.SetDialogue(
                    pickaxe.TextId, pickaxe.Message, canFace: false);
                npc.SetScriptAnimation(pickaxe.WorkAnimation);
                yield return new Room148PickaxeWorkerRoomEntity(
                    npc, pickaxe, soundRequested);
            }
            else
            {
                RequireNpcImplementation(
                    record,
                    NpcImplementationClassification.OrdinaryGeneric);
                yield return new NpcRoomEntity(
                    CreateNpcCharacter(record));
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
            bool supported = record.Id == 0x89 && record.SubId is 0x00 or 0x01 or 0x06 ||
                record.Id == 0xe5 && record.SubId is 0x00 or 0x01;
            if (!supported)
            {
                throw new InvalidOperationException(
                    $"Unsupported Vasu Jewelers interaction ${record.Id:x2}:${record.SubId:x2}.");
            }
            NpcCharacter npc = CreateNpcCharacter(record);
            yield return new VasuShopNpcRoomEntity(npc, _vasuShop);
        }
    }

    private IEnumerable<IRoomEntity> CreateLynnaShop(
        OracleRoomData room,
        IReadOnlyList<NpcRecord> records)
    {
        if (records.Count != 1 || records[0] is not { Id: 0x46, SubId: 0x00 })
        {
            throw new InvalidOperationException(
                $"Room 2:5e must contain shopkeeper $46:$00, got {records.Count} NPC records.");
        }

        // The three $47 placements precede $46:$00 in mainData.s. Stock
        // replacement can delete a placement, but surviving objects retain
        // that source order.
        foreach (StockRecord stock in
            _lynnaShop.ResolveStock(saveData))
        {
            var item = new LynnaShopItem
            {
                Name = $"ShopItem_{stock.Order}_{stock.Item.SubId:x2}",
                ZIndex = NpcCharacter.FixedLowPriorityZIndex
            };
            item.Initialize(stock, room);
            yield return new LynnaShopItemRoomEntity(item);
        }

        NpcRecord record = records[0];
        RequireNpcImplementation(
            record,
            NpcImplementationClassification.SpecializedNative);
        NpcCharacter shopkeeper = CreateNpcCharacter(record);
        yield return new LynnaShopkeeperRoomEntity(shopkeeper, _lynnaShop);

        // The final $71:$0c object is invisible and deletes itself after this
        // one entry-side effect.
        _lynnaShop.ApplyCompanionEntryState(saveData);
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
            NpcCharacter npc = CreateNpcCharacter(record);

            IRoomEntity entity = record switch
            {
                { Id: 0x3a, SubId: 0x02 } =>
                    new BlackTowerBlockingVillagerRoomEntity(npc, _blackTower),
                { Id: 0x40, SubId: 0x0c } =>
                    new BlackTowerSoldierRoomEntity(npc, _blackTower, random),
                { Id: 0x57, SubId: 0x03 } =>
                    new BlackTowerPickaxeWorkerRoomEntity(
                        npc, _room148.Record, _blackTower, random, soundRequested),
                { Id: 0x58, SubId: 0x00 } =>
                    new BlackTowerShovelWorkerRoomEntity(npc, _blackTower),
                { Id: 0x58, SubId: 0x03 } =>
                    new BlackTowerPatrollingWorkerRoomEntity(
                        npc, _blackTower, random),
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
                    _dungeonEntrances.GetRoomRecords(4, 0xe7))
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
            => CreateNpcCharacter(record);

        NpcCharacter boy = CreateNpc(Find(0x3c, 0x0e));
        NpcCharacter father = CreateNpc(Find(0x3a, 0x0c));
        NpcCharacter observer = CreateNpc(Find(0x43, 0x06));
        var ball = new Room149Ball
        {
            Name = "Room149Ball",
            ZIndex = 10
        };
        ball.Initialize(_room149.Visual("ball"));
        var family = new Room149FamilyInteraction(
            saveData, _room149, boy, father, observer, ball);

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
            saveData?.HasRoomFlag(group, room, 0x02) != true)
        {
            return true;
        }
        return type switch
        {
            0 => false,
            1 => saveData is null ||
                !saveData.HasGlobalFlag(OracleSaveData.GlobalFlagMakuTreeSaved),
            2 => saveData is null ||
                !saveData.HasTreasure(TreasureDatabase.TreasureSeedSatchel),
            _ => false
        };
    }

    private IRoomEntity CreateRock(OctorokRockSpawn spawn, OracleRoomData room)
    {
        var rock = new OctorokRockProjectile { Name = "OctorokRock", ZIndex = 10 };
        rock.Initialize(enemies.OctorokProjectile, room, spawn.Position, spawn.Angle);
        return new HostileProjectileRoomEntity<OctorokRockProjectile>(rock);
    }

    private IRoomEntity CreateMaskedMoblin(
        MaskedMoblinSpawn spawn, OracleRoomData room)
    {
        var moblin = new MaskedMoblinCharacter
        {
            Name = "MaskedMoblin",
            ZIndex = 10
        };
        moblin.Initialize(enemies.MaskedMoblin, room, spawn.Position, random);
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
            soundRequested);
    }

    private IRoomEntity CreateEnemyArrow(EnemyArrowSpawn spawn, OracleRoomData room)
    {
        var arrow = new EnemyArrowProjectile { Name = "EnemyArrow", ZIndex = 10 };
        arrow.Initialize(enemies.EnemyArrow, room, spawn.Position, spawn.Angle);
        return new HostileProjectileRoomEntity<EnemyArrowProjectile>(arrow);
    }

    private IRoomEntity CreateGel(
        GelSpawn spawn,
        OracleRoomData room,
        EnemyCombatSourceDescriptor? combatSource = null)
    {
        var gel = new GelCharacter { Name = spawn.Name, ZIndex = 10 };
        gel.Initialize(enemies.Gel, room, spawn.Position, random);
        EnemyCombatSourceDescriptor source = combatSource ??
            enemies.EnemyHandlers.ResolveHandler(
                enemies.Gel.Id,
                enemies.Gel.SubId,
                $"dynamic {spawn.Name} ENEMY_GEL")
            .CombatSource(
                objectFlags: 0,
                killableEnemyIndex: spawn.KillableEnemyIndex,
                source: $"dynamic {spawn.Name} ENEMY_GEL");
        return new GelRoomEntity(
            gel, source, soundRequested);
    }

    private static int NextKillableEnemyIndex(int flags, ref int count)
    {
        // checkEnemyKilled is bypassed by object flag bit $01. Only the first
        // seven checked objects receive an index in Enemy.enabled.
        if ((flags & 0x01) != 0 || count >= 7)
            return 0;
        count++;
        return count;
    }

    private IRoomEntity CreateRoom148Debris(Room148DebrisSpawn spawn)
    {
        var debris = new Room148PickaxeDebris
        {
            Name = "Room148PickaxeDebris"
        };
        debris.Initialize(_room148.Record, spawn);
        return new DialogueFixedEffectRoomEntityAdapter<Room148PickaxeDebris>(
            debris);
    }

    private static IRoomEntity CreateShovelDebris(ShovelDebrisSpawn spawn)
    {
        var debris = new ShovelDebrisEffect
        {
            Name = "ShovelDebris",
            ZIndex = 9
        };
        debris.Initialize(spawn.Position, spawn.Direction);
        return new DialogueFixedEffectRoomEntityAdapter<ShovelDebrisEffect>(
            debris);
    }

    private IRoomEntity CreateGrassDebris(GrassDebrisSpawn spawn)
    {
        var debris = new GrassDebrisEffect
        {
            Name = spawn.InteractionId == 0x01
                ? "RedGrassDebris"
                : "GrassDebris",
            ZIndex = 12
        };
        debris.Initialize(
            spawn.Position,
            spawn.InteractionId,
            spawn.Flickers,
            spawn.Underwater,
            soundRequested);
        return new GrassDebrisRoomEntity(debris);
    }

    private IRoomEntity CreateRockDebris(RockDebrisSpawn spawn)
    {
        var debris = new RockDebrisEffect
        {
            Name = spawn.InteractionId == 0x0c
                ? "RockDebris2"
                : "RockDebris",
            ZIndex = 9
        };
        debris.Initialize(
            spawn.Position, spawn.InteractionId, soundRequested);
        return new DialogueFixedEffectRoomEntityAdapter<RockDebrisEffect>(
            debris);
    }

    private IRoomEntity CreateEmberSeed(EmberSeedSpawn spawn, OracleRoomData room)
    {
        var seed = new EmberSeedEffect
        {
            Name = spawn.Record.SeedItem == OwlStatueDatabase.MysterySeedItem
                ? "MysterySeed"
                : "EmberSeed",
            ZIndex = 11
        };
        int mysteryEffect =
            spawn.Record.SeedItem == OwlStatueDatabase.MysterySeedItem
                ? random.Next().Value & 0x03
                : 0;
        seed.Initialize(
            spawn.Record, room, _breakables, spawn.LinkPosition, spawn.Direction,
            soundRequested, itemDropEnteredHazard, roomTileChanged, animationTick,
            drop => itemDrops.DecideBreakableDrop(
                drop, random, inventory, saveData), saveData,
            spawn.Group,
            rooms is null
                ? null
                : direction => rooms.TryGetNeighbor(
                    spawn.Group, room.Id, direction, out int neighbor)
                    ? neighbor
                    : null,
            mysteryEffect);
        return new EmberSeedRoomEntity(seed);
    }

    private IRoomEntity CreateBomb(BombSpawn spawn, OracleRoomData room)
    {
        if (inventory is null)
        {
            throw new InvalidOperationException(
                "ITEM_BOMB cannot be allocated without live inventory state.");
        }
        var bomb = new BombEffect
        {
            Name = "Bomb",
            ZIndex = 11
        };
        bomb.Initialize(
            spawn.Record,
            room,
            _breakables,
            spawn.Player,
            spawn.Group,
            spawn.HeldExplosion,
            soundRequested,
            (position, hazard, kind) =>
            {
                if (hazard is HazardType.Water or HazardType.Lava)
                    itemDropEnteredHazard(position, hazard);
                else if (hazard == HazardType.Hole)
                    objectFellInHole(kind);
            },
            roomTileChanged,
            animationTick,
            drop => itemDrops.DecideBreakableDrop(
                drop, random, inventory, saveData),
            saveData,
            rooms is null
                ? null
                : direction => rooms.TryGetNeighbor(
                    spawn.Group, room.Id, direction, out int neighbor)
                    ? neighbor
                    : null);
        return new BombRoomEntity(bomb);
    }

    private static IRoomEntity CreateOwlStatueSparkle(
        OwlStatueSparkleSpawn spawn)
    {
        var sparkle = new OwlStatueSparkleEffect
        {
            Name = "OwlStatueSparkle",
            ZIndex = NpcCharacter.InFrontOfLinkZIndex
        };
        sparkle.Initialize(spawn.Position, spawn.Visual);
        return new DialogueFixedEffectRoomEntityAdapter<OwlStatueSparkleEffect>(
            sparkle);
    }

    private IRoomEntity CreateSwordBeam(
        SwordBeamSpawn spawn, OracleRoomData room)
    {
        var beam = new SwordBeamEffect
        {
            Name = "SwordBeam",
            ZIndex = 11
        };
        beam.Initialize(
            _swordBeam, room, spawn.LinkPosition, spawn.Direction,
            worldToScreen, soundRequested);
        return new SwordBeamRoomEntity(beam);
    }

    private static IRoomEntity CreateSwordBeamClink(SwordBeamClinkSpawn spawn)
    {
        var clink = new ClinkEffect
        {
            Name = "SwordBeamClink",
            ZIndex = 11
        };
        // Subid $81 requests the flickering variant; unlike sword-on-wall
        // clinks, the beam collision does not play a second sound.
        clink.Initialize(spawn.Position, flickers: true);
        clink.SetPhysicsProcess(false);
        return new SwordBeamClinkRoomEntity(clink);
    }

    private IRoomEntity CreateEnemyClink(EnemyClinkSpawn spawn)
    {
        var clink = new ClinkEffect
        {
            Name = "EnemyClink",
            ZIndex = 11
        };
        clink.Initialize(spawn.Position, flickers: false);
        clink.SetPhysicsProcess(false);
        soundRequested(OracleSoundEngine.SndClink);
        return new SwordBeamClinkRoomEntity(clink);
    }

    private IRoomEntity CreatePuzzlePuff(PuzzlePuffSpawn spawn)
    {
        var puff = new PuzzlePuffEffect
        {
            Name = "PuzzlePuff",
            ZIndex = 10
        };
        puff.Initialize(spawn.Position, spawn.Sound, soundRequested);
        return new DialogueFixedEffectRoomEntityAdapter<PuzzlePuffEffect>(
            puff);
    }

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
            ZIndex = 11
        };
        effect.Initialize(
            spawn.Position,
            spawn.Hazard,
            autoFree: false);
        effect.SetPhysicsProcess(false);
        soundRequested(OracleSoundEngine.SndSplash);
        return new DialogueFixedEffectRoomEntityAdapter<SplashEffect>(effect);
    }

    private IRoomEntity CreateFallingDownHole(FallingDownHoleSpawn spawn)
    {
        var effect = new FallingDownHoleEffect
        {
            Name = "FallingDownHole",
            ZIndex = 10
        };
        effect.Initialize(spawn.Position);
        soundRequested(OracleSoundEngine.SndFallInHole);
        return new DialogueFixedEffectRoomEntityAdapter<FallingDownHoleEffect>(
            effect);
    }

    private IRoomEntity CreateDungeonKeyUse(DungeonKeyUseSpawn spawn)
    {
        var effect = new DungeonKeyUseEffect
        {
            Name = "DungeonKeyUse",
            ZIndex = 10
        };
        effect.Initialize(spawn.Position, spawn.Visual);
        soundRequested(OracleSoundEngine.SndGetSeed);
        return new FixedEffectRoomEntityAdapter<DungeonKeyUseEffect>(effect);
    }

    private static IRoomEntity CreateOverworldKeyUse(OverworldKeyUseSpawn spawn)
    {
        var effect = new OverworldKeyUseEffect
        {
            Name = "OverworldKeyUse",
            ZIndex = 10
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
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position, spawn.HighKnockback, spawn.EnemyId);
        soundRequested(OracleSoundEngine.SndKillEnemy);
        return new DeathPuffRoomEntity(
            puff, itemDrops, random, inventory, saveData,
            spawn.DecrementsRoomCount);
    }

    private IRoomEntity CreateBossDeathExplosion(BossDeathExplosionSpawn spawn)
    {
        var explosion = new BossDeathExplosionEffect
        {
            Name = "BossDeathExplosion",
            ZIndex = 10
        };
        explosion.Initialize(spawn.Position, spawn.BossId, soundRequested);
        return new BossDeathExplosionRoomEntity(
            explosion, itemDrops, random, inventory, saveData, roomEnemyCount);
    }

    private static IRoomEntity CreateBossShadow(BossShadowSpawn spawn)
    {
        var shadow = new BossShadowEffect
        {
            Name = "BossShadow",
            ZIndex = NpcCharacter.FixedLowPriorityZIndex
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
        var puff = new KillEnemyPuffEffect { Name = "KillEnemyPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position);
        soundRequested(OracleSoundEngine.SndKillEnemy);
        return new KillPuffRoomEntity(puff);
    }

    private IRoomEntity CreateItemDrop(ItemDropSpawn spawn, OracleRoomData room)
    {
        var drop = new ItemDropEffect { Name = $"ItemDrop_{spawn.SubId:x2}", ZIndex = 10 };
        int treasure = ItemDropDatabase.TreasureForDrop(spawn.SubId);
        int collectionSound = treasure == TreasureDatabase.TreasureNone
            ? 0
            : treasures.GetBehaviour(treasure).Sound;
        drop.Initialize(
            spawn.SubId, spawn.Position, room, itemDrops.GetVisual(spawn.SubId),
            spawn.Angle, spawn.DugUp, soundRequested, collectionSound,
            itemDrops, random);
        return new ItemDropRoomEntity(drop, itemDropEnteredHazard);
    }

    private static IRoomEntity CreateCutsceneNpc(CutsceneNpcSpawn spawn)
    {
        RequireNpcImplementation(
            spawn.Record,
            NpcImplementationClassification.EventOwned);
        var npc = new NpcCharacter
        {
            Name = spawn.Name,
            ZIndex = NpcCharacter.BehindLinkZIndex
        };
        npc.Initialize(spawn.Record);
        return new CutsceneNpcRoomEntity(npc, spawn.Talkable, spawn.Solid);
    }

    private IRoomEntity CreateGroundTreasure(GroundTreasureDatabaseRecord record)
    {
        var treasure = new GroundTreasurePickup
        {
            Name = $"GroundTreasure_{record.TreasureObject}",
            ZIndex = 12
        };
        treasure.Initialize(record, soundRequested, worldToScreen);
        return new GroundTreasureRoomEntity(
            treasure, groundTreasureCollectionAllowed,
            groundTreasureCollected);
    }

    private IRoomEntity CreateMapleDroppedItem(
        MapleDroppedItemSpawn spawn,
        OracleRoomData room)
    {
        var item = new MapleDroppedItem
        {
            Name = $"MapleItem_{spawn.Slot}_{spawn.Record.Index:x2}",
            ZIndex = 12
        };
        item.Initialize(
            spawn.Record,
            spawn.Encounter,
            room,
            random,
            spawn.Slot,
            spawn.SourcePosition,
            spawn.SourceZFixed,
            mapleItemCollected);
        spawn.Encounter.Register(item);
        return new MapleDroppedItemRoomEntity(item);
    }

    private IRoomEntity CreateLightableTorch(
        LightableTorchSpawn spawn,
        OracleRoomData room) => new LightableTorchRoomEntity(
            spawn.State, spawn.PackedPosition, room, _darkRooms,
            soundRequested, roomTileChanged, animationTick);

    internal IEnumerable<IRoomEntity> CreateTimePortals(int group, OracleRoomData room)
    {
        foreach (PortalRecord record in timePortals.GetRoomPortals(group, room.Id))
        {
            var portal = new TimePortal { Name = $"TimePortal_{record.SubId:x2}", ZIndex = 8 };
            portal.InitializePlaced(
                record,
                room,
                StartsActive(record, group, room.Id),
                saveData,
                playingInstrument,
                soundRequested);
            yield return new TimePortalRoomEntity(portal, portalEntered);
        }
        if (saveData is not null &&
            saveData.TimePortalGroup == group &&
            saveData.TimePortalRoom == room.Id)
        {
            timePortals.ApplyEntryTileReplacement(
                room, saveData.TimePortalPosition, animationTick());
            yield return CreateTemporaryTimePortal(
                room, PointForPackedPosition(saveData.TimePortalPosition));
        }
    }

    internal IRoomEntity CreateTemporaryTimePortal(
        OracleRoomData room,
        Vector2 position)
    {
        var portal = new TimePortal
        {
            Name = "TemporaryTimePortal",
            ZIndex = 8
        };
        portal.InitializeTemporary(timePortals.TemporaryVisual, room, position);
        return new TimePortalRoomEntity(portal, portalEntered);
    }

    private bool RoomObjectConditionMet(
        RoomObjectRecord record,
        int group,
        OracleRoomData room)
    {
        int stateModifier = (room.TilesetFlags & 0x40) != 0 ? 1 : 0;
        if (saveData?.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagLayoutSwap) == true)
            stateModifier++;
        return (record.ConditionMask & (1 << stateModifier)) != 0;
    }

    private (int Group, int Room) ResolveWallmasterDestination(
        RoomObjectRecord source)
    {
        int dungeon = rooms?.World.GetDungeonIndex(source.Group, source.Room) ?? -1;
        DungeonInfo info;
        if (dungeon >= 0)
        {
            info = _dungeonMaps.GetDungeon(dungeon);
        }
        else if (!_dungeonMaps.TryGetDungeonForRoom(
            source.Group, source.Room, out info))
        {
            throw new InvalidOperationException(
                $"Wallmaster room {source.Group:x1}:{source.Room:x2} has no " +
                "unambiguous imported dungeon metadata.");
        }
        if (info.Group != source.Group)
        {
            throw new InvalidOperationException(
                $"Wallmaster room {source.Group:x1}:{source.Room:x2} resolved " +
                $"dungeon ${info.Index:x2} in group ${info.Group:x1}.");
        }
        return (info.Group, info.WallmasterDestinationRoom);
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
                    if (!resolution
                        .RequireEnemyHandler(source)
                        .CompletesDungeonEnemyCount)
                    {
                        return false;
                    }
                    break;

                case EnemyObjectSlotPolicy.ParameterEnemy:
                    return false;
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
            _spiritsGrave.GetRoomRecords(group, room.Id))
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
            _wingDungeon.GetRoomRecords(group, room.Id))
        {
            if (native.Kind is DungeonObjectKind.HeadThwomp or DungeonObjectKind.Swoop)
            {
                hasSupportedNativeBossRecord = true;
                break;
            }
        }
        bool hasSupportedDoor = false;
        foreach (DungeonMechanicDatabaseRecord record in records)
        {
            if (record.Id == 0x09)
                continue;
            if (record.Id == 0x1e)
                hasSupportedDoor = true;
            if (record.Id == 0x1e && record.SubId <= 0x07)
                continue;
            if (!record.CountSourceComplete && !hasSupportedNativeBossRecord)
                return false;
        }
        return hasSupportedDoor && DungeonEnemyCountIsComplete(group, room);
    }

    private bool TryChooseRandomEnemyPosition(
        OracleRoomData room,
        int flags,
        EnemyPlacementReservations reservations,
        EnemyPlacementContext placementContext,
        out Vector2 position)
    {
        int attemptsRemaining = 0x3f;
        while (attemptsRemaining > 0)
        {
            int packed = random.NextPlacementValue();
            int tileY = packed >> 4;
            int tileX = packed & 0x0f;
            bool validBoundary = room.Group < 4
                ? tileY < OracleRoomData.ViewportHeight / OracleRoomData.MetatileSize &&
                    tileX < OracleRoomData.ViewportWidth / OracleRoomData.MetatileSize
                : tileY > 0 && tileY < room.HeightInTiles - 1 &&
                    tileX > 0 && tileX < room.WidthInTiles - 1;
            if (!validBoundary || reservations.Contains(packed))
                continue;

            // getCandidatePositionForEnemy loops over out-of-bounds and
            // reserved entries internally. Only a candidate returned from it
            // consumes one of getRandomPositionForEnemy's `$3f attempts.
            attemptsRemaining--;
            if (!placementContext.Allows(room, packed))
                continue;

            position = new Vector2(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            if ((flags & 0x04) == 0 && !_enemySpawnTiles.IsValid(
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

internal sealed record BossDeathExplosionSpawn(Vector2 Position, int BossId)
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
    int Group)
    : RoomEntitySpawn;

internal sealed record BombSpawn(
    Player Player,
    BombRecord Record,
    int Group,
    Action<BombEffect> HeldExplosion)
    : RoomEntitySpawn;

internal sealed record SwordBeamClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;

internal sealed record EnemyClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;

internal sealed record StatueEyeballSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MovingPlatformSpawn(Vector2 Position, int SubId)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MinibossPortalSpawn : RoomEntitySpawn;

internal sealed record ShovelDebrisSpawn(Vector2 Position, Vector2I Direction)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record GrassDebrisSpawn(
    Vector2 Position,
    int InteractionId = 0x00,
    bool Flickers = false,
    bool Underwater = false)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record RockDebrisSpawn(
    Vector2 Position,
    int InteractionId = 0x06)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record PuzzlePuffSpawn(Vector2 Position, int Sound)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record PumpkinHeadProjectileSpawn(Vector2 Position, int Angle)
    : RoomEntitySpawn;

internal sealed record HeadThwompProjectileSpawn(
    Vector2 Position,
    HeadThwompProjectileKind Kind,
    int Angle,
    int Speed) : RoomEntitySpawn;

internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabaseRecord Visual,
    ConstantsRecord Constants) : RoomEntitySpawn;

internal sealed record EraInfoSpawn(EraInfoDatabaseRecord Record)
    : RoomEntitySpawn;

internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record LightableTorchSpawn(
    DarkRoomState State,
    int PackedPosition)
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
