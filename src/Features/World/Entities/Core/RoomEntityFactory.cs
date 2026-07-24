using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RoomEntityFactory(
    NpcDatabase npcs,
    EnemyDatabase enemies,
    ItemDropDatabase itemDrops,
    TimePortalDatabase timePortals,
    OracleRandom random,
    OracleSaveData? saveData,
    OracleRuntimeState runtimeState,
    Action<TimePortal> portalEntered,
    Func<bool> groundTreasureCollectionAllowed,
    Action<GroundTreasurePickup, Player> groundTreasureCollected,
    Action<int, string> dungeonEntranceTriggered,
    Action<Warp> roomWarpRequested,
    Action<GashaSpotInteraction, Player> gashaInteractionRequested,
    Action<GashaSpotInteraction, Player> gashaNutCaught,
    InventoryState? inventory,
    TreasureDatabase treasures,
    Action<Vector2, HazardType> itemDropEnteredHazard,
    Action<int> soundRequested,
    Func<int> roomEnemyCount,
    Func<int, bool> enemyWasKilled,
    Func<int, bool> triggerActive,
    Func<int> triggerState,
    Action<int, bool> setTrigger,
    Action roomTileChanged,
    Action<SpiritsGraveEssence, Player> spiritsGraveEssenceTriggered,
    Func<bool> bossShuttersClosed,
    Action<int> screenShakeRequested,
    Action disableLinkCollisionsAndMenu,
    Action enableLinkCollisionsAndMenu,
    Action<int, int> roomMusicRequested,
    Action<int, string, Player> mapleDialogueRequested,
    Func<bool> dialogueOpen,
    Action<MapleItemRecord, Player> mapleItemCollected,
    Action<int> horizontalScreenShakeRequested,
    Func<Vector2, Vector2> worldToScreen,
    Func<long> animationTick,
    RoomSession? rooms)
{
    private readonly Room148PickaxeDatabase _room148 = new();
    private readonly Room149FamilyDatabase _room149 = new();
    private readonly VasuShopDatabase _vasuShop = new();
    private readonly LynnaShopDatabase _lynnaShop = new();
    private readonly BlackTowerWorkerDatabase _blackTower = new();
    private readonly DungeonEntranceInteractionDatabase _dungeonEntrances = new();
    private readonly SpiritsGraveDatabase _spiritsGrave = new();
    private readonly EnemySpawnTileDatabase _enemySpawnTiles = new();
    private readonly GroundTreasureDatabase _groundTreasures = new();
    private readonly DungeonMechanicDatabase _dungeonMechanics = new();
    private readonly RoomTileChangeWatcherDatabase _tileChangeWatchers = new();
    private readonly BreakableTileDatabase _breakables = new();
    private readonly SwordBeamDatabase _swordBeam = new();
    private readonly GashaSpotDatabase _gashaSpots = new();
    private readonly DarkRoomDatabase _darkRooms = new();
    private readonly MapleEventDatabase _maple = new();
    private readonly DungeonMapDatabase _dungeonMaps =
        rooms?.DungeonMaps ?? new DungeonMapDatabase();

    /// <summary>
    /// Mirrors replaceShutterForLinkEntering for ordinary layout shutters
    /// $78-$7b. Those source table rows replace only the shutter under Link
    /// with floor $a0 and set bit 7, so no auto-close interaction is created.
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
        if (tile is < DungeonShutterEntry.FirstNormalShutterTile or
            > DungeonShutterEntry.LastNormalShutterTile ||
            !DungeonShutterEntry.Matches(
                placementContext,
                packedPosition,
                tile - DungeonShutterEntry.FirstNormalShutterTile))
        {
            return;
        }

        room.SetPositionTileAndCollision(
            position, (byte)_dungeonMechanics.OpenTile, null, animationTick());
    }

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
                            record, _darkRooms, darkRoomState, saveData, treasures),
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
        IReadOnlyList<ObjectRecord> spiritsGraveRecords =
            _spiritsGrave.GetRoomRecords(group, room.Id);
        SpiritsGravePuzzleState? spiritsGravePuzzle =
            group == 4 && room.Id == 0x20 ? new SpiritsGravePuzzleState() : null;
        bool enemyMechanicsSupported = DungeonEnemyMechanicsAreSupported(
            dungeonRecords, group, room);
        int mechanicIndex = 0;
        int sharedIndex = 0;
        int spiritsGraveIndex = 0;
        while (mechanicIndex < dungeonRecords.Count ||
               sharedIndex < sharedDungeonRecords.Count ||
               spiritsGraveIndex < spiritsGraveRecords.Count)
        {
            int mechanicOrder = mechanicIndex < dungeonRecords.Count
                ? dungeonRecords[mechanicIndex].Order : int.MaxValue;
            int sharedOrder = sharedIndex < sharedDungeonRecords.Count
                ? sharedDungeonRecords[sharedIndex].Order : int.MaxValue;
            int spiritsGraveOrder = spiritsGraveIndex < spiritsGraveRecords.Count
                ? spiritsGraveRecords[spiritsGraveIndex].Order : int.MaxValue;
            bool useShared = sharedOrder < mechanicOrder &&
                sharedOrder < spiritsGraveOrder;
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

            if (spiritsGraveOrder < mechanicOrder)
            {
                ObjectRecord record =
                    spiritsGraveRecords[spiritsGraveIndex++];
                if (!SpiritsGraveConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateSpiritsGraveInteraction(
                    record, room, spiritsGravePuzzle, placementContext);
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
            foreach (RoomTileChangeWatcherDatabaseRecord record in
                _tileChangeWatchers.GetRoomRecords(group, room.Id))
            {
                yield return new RoomTileChangeWatcherRoomEntity(
                    record, room, saveData);
            }

        }

        IReadOnlyList<NpcRecord> roomNpcs =
            npcs.GetRoomNpcs(group, room.Id, saveData, runtimeState);
        if (group == 4 && room.Id is 0xe0 or 0xe1 or 0xe2 or 0xe7 or 0xe8)
        {
            foreach (IRoomEntity entity in CreateBlackTowerNpcs(
                room, roomNpcs, placementContext))
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
                var npc = new NpcCharacter
                {
                    Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                    ZIndex = NpcCharacter.BehindLinkZIndex
                };
                npc.Initialize(record);
                yield return record is { Id: 0x28, SubId: 0x00 }
                    ? new RunningBipinRoomEntity(npc)
                    : new NpcRoomEntity(npc);
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

            switch (source.Kind)
            {
                case RoomObjectKind.RandomEnemy:
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
                            source, room, position, instance, killableEnemyIndex);
                        if (entity is not null)
                            yield return entity;
                    }
                    break;

                case RoomObjectKind.FixedEnemy:
                    int fixedKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags, ref killableEnemies);
                    if (enemyWasKilled(fixedKillableEnemyIndex))
                        break;
                    if (enemySlots >= 16)
                        break;
                    enemySlots++;
                    reservations.Add(source.PackedPosition);
                    IRoomEntity? fixedEntity = CreateOrderedEnemy(
                        source, room, new Vector2(source.X, source.Y), 0,
                        fixedKillableEnemyIndex);
                    if (fixedEntity is not null)
                        yield return fixedEntity;
                    break;

                case RoomObjectKind.ParameterEnemy:
                    if (enemySlots < 16)
                        enemySlots++;
                    break;

                case RoomObjectKind.ItemDrop:
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
                            saveData);
                        yield return new ItemDropProducerRoomEntity(
                            producer, itemKillableEnemyIndex);
                    }
                    break;

                case RoomObjectKind.ReservingPart:
                    if (partSlots >= 16)
                        break;
                    partSlots++;
                    reservations.Add(source.PackedPosition);
                    break;

                case RoomObjectKind.ParameterPart:
                    if (partSlots < 16)
                        partSlots++;
                    break;
            }
        }
    }

    private IRoomEntity? CreateSpiritsGraveInteraction(
        ObjectRecord record,
        OracleRoomData room,
        SpiritsGravePuzzleState? puzzle,
        EnemyPlacementContext placementContext)
    {
        switch (record.Kind)
        {
            case ObjectKind.BraceletReward:
                return CreateSpiritsGraveReward(
                    record, "TREASURE_OBJECT_BRACELET_00", falling: false);
            case ObjectKind.EnemySmallKey:
                return CreateSpiritsGraveReward(
                    record, "TREASURE_OBJECT_SMALL_KEY_01", falling: true);
            case ObjectKind.BossReward:
                return CreateSpiritsGraveReward(
                    record, "TREASURE_OBJECT_HEART_CONTAINER_00", falling: false);
            case ObjectKind.MinibossReward:
                return new SpiritsGraveRewardController(
                    record, saveData, roomEnemyCount, treasure: null,
                    enableLinkCollisionsAndMenu);
            case ObjectKind.MovingPlatform:
                return new SpiritsGraveMovingPlatform(
                    _spiritsGrave.Visual("platform-05"),
                    record.Position,
                    record.SubId,
                    _spiritsGrave.MovingPlatformCollisionRadii(record.SubId));
            case ObjectKind.SpawnMovingPlatform:
                return new SpiritsGraveMovingPlatformSpawner(
                    triggerActive, soundRequested);
            case ObjectKind.TorchStairs:
                return new SpiritsGraveTorchStairs(
                    record, room, saveData, soundRequested,
                    roomTileChanged, animationTick);
            case ObjectKind.ColoredCube:
                return new SpiritsGraveColoredCube(
                    record, _spiritsGrave.Visual("colored-cube"), room,
                    RequireSpiritsGravePuzzle(puzzle, record),
                    _spiritsGrave.CubePalettes,
                    soundRequested, roomTileChanged, animationTick);
            case ObjectKind.CubeFlame:
                return new SpiritsGraveCubeFlame(
                    record, _spiritsGrave.Visual("cube-flame"),
                    RequireSpiritsGravePuzzle(puzzle, record));
            case ObjectKind.CubeLightSensor:
            case ObjectKind.CubeTriggerSensor:
                return new SpiritsGraveCubeSensor(
                    record, room, RequireSpiritsGravePuzzle(puzzle, record),
                    setTrigger, soundRequested);
            case ObjectKind.GiantGhini:
                var giantGhini = new GiantGhiniBoss
                {
                    Name = "GiantGhini",
                    ZIndex = 10
                };
                giantGhini.Initialize(
                    _spiritsGrave.Enemy(0x70), room, record.Position, random,
                    soundRequested, bossShuttersClosed,
                    disableLinkCollisionsAndMenu,
                    () => roomMusicRequested(record.Group, record.Room));
                return new GiantGhiniBossRoomEntity(
                    giantGhini, BossEntryDirection(placementContext));
            case ObjectKind.PumpkinHead:
                ImportedEnemyDefinition pumpkin = _spiritsGrave.Enemy(0x78);
                return new PumpkinHeadBossRoomEntity(
                    new PumpkinHeadBoss(
                        pumpkin, room, record.Position, random, soundRequested,
                        bossShuttersClosed, screenShakeRequested,
                        disableLinkCollisionsAndMenu,
                        () => roomMusicRequested(record.Group, record.Room),
                        _spiritsGrave.Constant("pumpkin-body-palette"),
                        _spiritsGrave.Constant("pumpkin-ghost-palette")),
                    pumpkin.DamageQuarters,
                    BossEntryDirection(placementContext));
            case ObjectKind.Essence:
                return new SpiritsGraveEssence(
                    record,
                    _spiritsGrave.Visual("eternal-spirit"),
                    _spiritsGrave.Visual("essence-pedestal"),
                    _spiritsGrave.Visual("essence-glow"),
                    _spiritsGrave.Visual("energy-bead"),
                    room,
                    saveData?.HasRoomFlag(
                        record.Group, record.Room, OracleSaveData.RoomFlagItem) == true,
                    animationTick,
                    random,
                    spiritsGraveEssenceTriggered);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record), record, "Unsupported Spirit's Grave object.");
        }
    }

    private static Vector2I BossEntryDirection(
        EnemyPlacementContext placementContext) =>
        placementContext.Kind == EnemyPlacementEntryKind.Scrolling
            ? placementContext.ScrollDirection
            : Vector2I.Zero;

    private SpiritsGraveRewardController CreateSpiritsGraveReward(
        ObjectRecord record,
        string treasureName,
        bool falling)
    {
        TreasureObjectRecord treasure = treasures.GetObject(treasureName);
        TreasureObjectVisualRecord visual =
            treasures.GetObjectVisual(treasure.Graphic);
        GroundTreasureDatabaseRecord ground = new GroundTreasureDatabaseRecord(
            record.Group,
            record.Room,
            record.Order,
            record.Y,
            record.X,
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            treasure.TextId,
            treasure.Message,
            record.Source,
            SpawnMode: falling ? 2 : 0,
            GrabMode: 2,
            SpawnDelayFrames: falling ? 40 : 0,
            InitialZPixels: 0,
            BounceCount: falling ? 2 : 0,
            Gravity: falling ? 0x10 : 0,
            BounceSpeed: falling ? -0xaa : 0,
            SpawnSound: falling ? OracleSoundEngine.SndSolvePuzzle : 0,
            LandingSound: falling ? OracleSoundEngine.SndDropEssence : 0,
            InitialZAboveScreen: falling);
        return new SpiritsGraveRewardController(
            record, saveData, roomEnemyCount, ground,
            enableLinkCollisionsAndMenu);
    }

    private static SpiritsGravePuzzleState RequireSpiritsGravePuzzle(
        SpiritsGravePuzzleState? puzzle,
        ObjectRecord record) =>
        puzzle ?? throw new InvalidOperationException(
            $"{record.Source} is missing its room-local rotating-cube state.");

    private bool SpiritsGraveConditionMet(
        ObjectRecord record) => record.Predicate switch
    {
        SpiritsGraveDatabaseCondition.Always => true,
        SpiritsGraveDatabaseCondition.ItemClear =>
            saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlagItem) != true,
        SpiritsGraveDatabaseCondition.Flag80Clear =>
            saveData?.HasRoomFlag(
                record.Group, record.Room, OracleSaveData.RoomFlag80) != true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(record), record, "Unknown Spirit's Grave predicate.")
    };

    private IRoomEntity? CreateDungeonMechanic(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        int group,
        bool enemyMechanicsSupported,
        EnemyPlacementContext placementContext)
    {
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
        RoomObjectRecord source,
        OracleRoomData room,
        Vector2 position,
        int instance,
        int killableEnemyIndex)
    {
        if (source.Id == 0x32 && enemies.TryGetKeeseDefinition(source, out EnemyDatabaseEnemyRecord keeseRecord))
        {
            var keese = new KeeseCharacter
            {
                Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            keese.Initialize(keeseRecord, room, position, random);
            return new KeeseRoomEntity(keese, killableEnemyIndex);
        }

        if (source.Id == 0x41 &&
            enemies.TryGetCrowDefinition(source, out CrowRecord crowRecord))
        {
            var crow = new CrowCharacter
            {
                Name = $"Crow_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            crow.Initialize(crowRecord, room, position, random);
            return new CrowRoomEntity(crow, killableEnemyIndex);
        }

        if (source.Id == 0x09 &&
            enemies.TryGetOctorokDefinition(source, out OctorokRecord octorokRecord))
        {
            var octorok = new OctorokCharacter
            {
                Name = $"Octorok_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            octorok.Initialize(octorokRecord, room, position, random);
            return new OctorokRoomEntity(octorok, killableEnemyIndex);
        }

        if (source.Id == 0x31 &&
            enemies.TryGetStalfosDefinition(source, out StalfosRecord stalfosRecord))
        {
            var stalfos = new StalfosCharacter
            {
                Name = $"Stalfos_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            stalfos.Initialize(stalfosRecord, room, position, random);
            return new StalfosRoomEntity(stalfos, killableEnemyIndex);
        }

        if (source.Id == 0x34 &&
            enemies.TryGetZolDefinition(source, out ZolRecord zolRecord))
        {
            var zol = new ZolCharacter
            {
                Name = $"Zol_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            zol.Initialize(zolRecord, room, position, random);
            return new ZolRoomEntity(zol, killableEnemyIndex);
        }

        if (source.Id == 0x0a &&
            enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition moblinRecord))
        {
            var moblin = new BoomerangMoblinCharacter
            {
                Name = $"BoomerangMoblin_{source.Order}_{instance}",
                ZIndex = 10
            };
            moblin.Initialize(moblinRecord, room, position, random);
            return new BoomerangMoblinRoomEntity(moblin, killableEnemyIndex);
        }

        if (source.Id == 0x0c &&
            enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition arrowMoblinRecord))
        {
            var moblin = new ArrowMoblinCharacter
            {
                Name = $"ArrowMoblin_{source.Order}_{instance}",
                ZIndex = 10
            };
            moblin.Initialize(arrowMoblinRecord, room, position, random);
            return new ArrowMoblinRoomEntity(moblin, killableEnemyIndex);
        }

        if (source.Id == 0x10 &&
            enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition ropeRecord))
        {
            var rope = new RopeCharacter
            {
                Name = $"Rope_{source.Order}_{instance}",
                ZIndex = 10
            };
            rope.Initialize(ropeRecord, room, position, random);
            return new RopeRoomEntity(rope, killableEnemyIndex);
        }

        if (source.Id == 0x17 &&
            enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition ghiniRecord))
        {
            var ghini = new GhiniCharacter
            {
                Name = $"Ghini_{source.Order}_{instance}",
                ZIndex = 10
            };
            ghini.Initialize(ghiniRecord, room, position, random);
            return new GhiniRoomEntity(
                ghini, killableEnemyIndex, soundRequested);
        }

        if (source.Id == 0x28 &&
            enemies.TryGetImportedEnemyDefinition(
                source, out ImportedEnemyDefinition wallmasterRecord))
        {
            var wallmaster = new WallmasterCharacter
            {
                Name = $"Wallmaster_{source.Order}_{instance}",
                ZIndex = 10
            };
            wallmaster.Initialize(
                wallmasterRecord, room, position);
            (int destinationGroup, int destinationRoom) =
                ResolveWallmasterDestination(source);
            return new WallmasterRoomEntity(
                wallmaster, soundRequested, roomWarpRequested,
                source.Group, source.Room,
                destinationGroup, destinationRoom,
                killableEnemyIndex);
        }

        return source.Id == 0x43 && source.SubId == 0
            ? CreateGel(
                new GelSpawn(position, $"RoomGel_{source.Order}_{instance}"),
                room, (source.Flags & 0x02) == 0, killableEnemyIndex)
            : null;
    }

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
        PuzzlePuffSpawn puff => CreatePuzzlePuff(puff),
        EnemySplashSpawn splash => CreateEnemySplash(splash),
        FallingDownHoleSpawn fall => CreateFallingDownHole(fall),
        DungeonKeyUseSpawn key => CreateDungeonKeyUse(key),
        OverworldKeyUseSpawn key => CreateOverworldKeyUse(key),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        GroundTreasureSpawn treasure => CreateGroundTreasure(treasure.Record),
        MapleDroppedItemSpawn item => CreateMapleDroppedItem(item, room),
        LightableTorchSpawn torch => CreateLightableTorch(torch, room),
        Room148DebrisSpawn debris => CreateRoom148Debris(debris),
        SwordBeamSpawn beam => CreateSwordBeam(beam, room),
        SwordBeamClinkSpawn clink => CreateSwordBeamClink(clink),
        StatueEyeballSpawn eye => CreateStatueEyeball(eye),
        SpiritsGraveMovingPlatformSpawn platform =>
            CreateSpiritsGraveMovingPlatform(platform),
        SpiritsGraveMinibossPortalSpawn => CreateSpiritsGraveMinibossPortal(room),
        GiantGhiniChildSpawn child => CreateGiantGhiniChild(child, room),
        PumpkinHeadProjectileSpawn projectile =>
            CreatePumpkinHeadProjectile(projectile, room),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

    private IRoomEntity CreateSpiritsGraveMovingPlatform(
        SpiritsGraveMovingPlatformSpawn spawn) =>
        new SpiritsGraveMovingPlatform(
            _spiritsGrave.Visual(
                (spawn.SubId >> 3) == 0 ? "platform-05" : "platform-09"),
            spawn.Position,
            spawn.SubId,
            _spiritsGrave.MovingPlatformCollisionRadii(spawn.SubId));

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
            _spiritsGrave.Enemy(0x3f), spawn.Owner, room, spawn.Index);
        return new GiantGhiniChildRoomEntity(child);
    }

    private IRoomEntity CreatePumpkinHeadProjectile(
        PumpkinHeadProjectileSpawn spawn,
        OracleRoomData room) =>
        new PumpkinHeadProjectileRoomEntity(
            new PumpkinHeadProjectile(
                _spiritsGrave.Visual("pumpkin-projectile"),
                room,
                spawn.Position,
                spawn.Angle));

    private IRoomEntity CreateSpiritsGraveMinibossPortal(OracleRoomData room)
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

    private IEnumerable<IRoomEntity> CreateRoom148Npcs(
        IReadOnlyList<NpcRecord> records)
    {
        bool foundWorker = false;
        foreach (NpcRecord record in records)
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
            if (record is { Id: 0x57, SubId: 0x00 })
            {
                if (foundWorker)
                    throw new InvalidOperationException(
                        "Room 1:48 contains more than one pickaxe worker $57:$00.");
                foundWorker = true;
                PickaxeRecord pickaxe = _room148.Record;
                npc.SetDialogue(
                    pickaxe.TextId, pickaxe.Message, canFace: false);
                npc.SetScriptAnimation(pickaxe.WorkAnimation);
                yield return new Room148PickaxeWorkerRoomEntity(
                    npc, pickaxe, soundRequested);
            }
            else
            {
                yield return new NpcRoomEntity(npc);
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
            bool supported = record.Id == 0x89 && record.SubId is 0x00 or 0x01 or 0x06 ||
                record.Id == 0xe5 && record.SubId is 0x00 or 0x01;
            if (!supported)
            {
                throw new InvalidOperationException(
                    $"Unsupported Vasu Jewelers interaction ${record.Id:x2}:${record.SubId:x2}.");
            }
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
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
        var shopkeeper = new NpcCharacter
        {
            Name = "Npc_46_00",
            ZIndex = NpcCharacter.BehindLinkZIndex
        };
        shopkeeper.Initialize(record);
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
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);

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
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
            return npc;
        }

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

    private bool StartsActive(int subId)
    {
        // timeportalSpawner.s sets bit 7 for subtype $01 until the Maku Tree
        // is saved and for subtype $02 until the Seed Satchel is obtained.
        // Bit 7 in object data is already-active unconditionally. Ordinary
        // subtype $00 portals normally wait for the Tune of Echoes; until harp
        // playback exists, exposed `$d7 markers use the deterministic active
        // fallback so they are usable instead of inert.
        int type = subId & 0x0f;
        if ((subId & 0x80) != 0)
            return true;
        return type switch
        {
            0 => true,
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
        return new OctorokRockRoomEntity(rock);
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
        return new MaskedMoblinRoomEntity(moblin);
    }

    private IRoomEntity CreateEnemyArrow(EnemyArrowSpawn spawn, OracleRoomData room)
    {
        var arrow = new EnemyArrowProjectile { Name = "EnemyArrow", ZIndex = 10 };
        arrow.Initialize(enemies.EnemyArrow, room, spawn.Position, spawn.Angle);
        return new EnemyArrowRoomEntity(arrow);
    }

    private IRoomEntity CreateGel(
        GelSpawn spawn,
        OracleRoomData room,
        bool countsAsEnemy = true,
        int? killableEnemyIndex = null)
    {
        var gel = new GelCharacter { Name = spawn.Name, ZIndex = 10 };
        gel.Initialize(enemies.Gel, room, spawn.Position, random);
        return new GelRoomEntity(
            gel, countsAsEnemy,
            killableEnemyIndex ?? spawn.KillableEnemyIndex);
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
        return new Room148DebrisRoomEntity(debris);
    }

    private static IRoomEntity CreateShovelDebris(ShovelDebrisSpawn spawn)
    {
        var debris = new ShovelDebrisEffect
        {
            Name = "ShovelDebris",
            ZIndex = 9
        };
        debris.Initialize(spawn.Position, spawn.Direction);
        return new ShovelDebrisRoomEntity(debris);
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
        return new RockDebrisRoomEntity(debris);
    }

    private IRoomEntity CreateEmberSeed(EmberSeedSpawn spawn, OracleRoomData room)
    {
        var seed = new EmberSeedEffect
        {
            Name = "EmberSeed",
            ZIndex = 11
        };
        seed.Initialize(
            spawn.Record, room, _breakables, spawn.LinkPosition, spawn.Direction,
            soundRequested, itemDropEnteredHazard, roomTileChanged, animationTick,
            drop => itemDrops.DecideBreakableDrop(drop, random), saveData,
            spawn.Group,
            rooms is null
                ? null
                : direction => rooms.TryGetNeighbor(
                    spawn.Group, room.Id, direction, out int neighbor)
                    ? neighbor
                    : null);
        return new EmberSeedRoomEntity(seed);
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

    private IRoomEntity CreatePuzzlePuff(PuzzlePuffSpawn spawn)
    {
        var puff = new PuzzlePuffEffect
        {
            Name = "PuzzlePuff",
            ZIndex = 10
        };
        puff.Initialize(spawn.Position, spawn.Sound, soundRequested);
        return new PuzzlePuffRoomEntity(puff);
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
        return new SplashRoomEntity(effect);
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
        return new FallingDownHoleRoomEntity(effect);
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
        return new DungeonKeyUseRoomEntity(effect);
    }

    private static IRoomEntity CreateOverworldKeyUse(OverworldKeyUseSpawn spawn)
    {
        var effect = new OverworldKeyUseEffect
        {
            Name = "OverworldKeyUse",
            ZIndex = 10
        };
        effect.Initialize(spawn.Position, spawn.Visual, spawn.Constants);
        return new OverworldKeyUseRoomEntity(effect);
    }

    private IRoomEntity CreateDeathPuff(EnemyDeathPuffSpawn spawn)
    {
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position, spawn.HighKnockback, spawn.EnemyId);
        soundRequested(OracleSoundEngine.SndKillEnemy);
        return new DeathPuffRoomEntity(puff, itemDrops, random);
    }

    private IRoomEntity CreateBossDeathExplosion(BossDeathExplosionSpawn spawn)
    {
        var explosion = new BossDeathExplosionEffect
        {
            Name = "BossDeathExplosion",
            ZIndex = 10
        };
        explosion.Initialize(spawn.Position, spawn.BossId, soundRequested);
        return new BossDeathExplosionRoomEntity(explosion);
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
            spawn.Angle, spawn.DugUp, soundRequested, collectionSound);
        return new ItemDropRoomEntity(drop, itemDropEnteredHazard);
    }

    private static IRoomEntity CreateCutsceneNpc(CutsceneNpcSpawn spawn)
    {
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
            if (!StartsActive(record.SubId))
                continue;
            var portal = new TimePortal { Name = $"TimePortal_{record.SubId:x2}", ZIndex = 8 };
            portal.Initialize(record, room);
            yield return new TimePortalRoomEntity(portal, portalEntered);
        }
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
            switch (source.Kind)
            {
                case RoomObjectKind.RandomEnemy:
                case RoomObjectKind.FixedEnemy:
                    bool supported = source.Id switch
                    {
                        0x09 => enemies.TryGetOctorokDefinition(source, out _),
                        0x0a or 0x0c or 0x10 or 0x17 or 0x28 =>
                            enemies.TryGetImportedEnemyDefinition(source, out _),
                        0x31 => enemies.TryGetStalfosDefinition(source, out _),
                        0x32 => enemies.TryGetKeeseDefinition(source, out _),
                        0x34 => enemies.TryGetZolDefinition(source, out _),
                        0x41 => enemies.TryGetCrowDefinition(source, out _),
                        0x43 => source.SubId == 0,
                        _ => false
                    };
                    if (!supported)
                        return false;
                    break;

                case RoomObjectKind.ParameterEnemy:
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
        foreach (ObjectRecord native in
            _spiritsGrave.GetRoomRecords(group, room.Id))
        {
            if (native.Kind is ObjectKind.GiantGhini or
                ObjectKind.PumpkinHead)
            {
                // A completed boss's BeforeEvent record is suppressed by
                // ROOMFLAG_80. That is still a complete zero-enemy stream:
                // the original shutter script sees wNumEnemies == 0 and
                // opens every enemy shutter while the room initializes.
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

internal sealed record SwordBeamClinkSpawn(Vector2 Position)
    : RoomEntitySpawn;

internal sealed record StatueEyeballSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record SpiritsGraveMovingPlatformSpawn(Vector2 Position, int SubId)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record SpiritsGraveMinibossPortalSpawn : RoomEntitySpawn;

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

internal sealed record OverworldKeyUseSpawn(
    Vector2 Position,
    OverworldKeyholeDatabaseRecord Visual,
    ConstantsRecord Constants) : RoomEntitySpawn;

internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record LightableTorchSpawn(
    DarkRoomState State,
    int PackedPosition)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record GroundTreasureSpawn(GroundTreasureDatabaseRecord Record)
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
