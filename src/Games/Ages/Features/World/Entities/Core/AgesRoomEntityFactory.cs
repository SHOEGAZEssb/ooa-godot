
namespace Oracles;

// Creation shares the manager's authoritative state and named operations.
// Live providers are sampled by the owner when actors call them, including
// providers assigned after this factory and those actors were constructed.
internal sealed class AgesRoomEntityFactory : IRoomEntityFactory
{
    internal MooshRescueEvent? MooshRescue { get; set; }
    internal RickyGlovesEvent? RickyGloves { get; set; }
    internal MooshGoodbyeEvent? MooshGoodbye { get; set; }
    internal AgesCompanionScriptController? NativeCompanions { get; set; }
    private AgesCompanionScriptController CompanionScripts => NativeCompanions ??
        throw new InvalidOperationException("Ages native animals require their script services.");
    public IRoomEntity? CreateStandaloneEnemy(int id, int subId, OracleRoomData room, Vector2 position,
        string source, out string error) => EnemyFactory.CreateStandaloneEnemy(id,subId,room,position,source,out error);
    internal static RoomEntityManager CreateManager(
        Node worldRoot,
        NpcDatabase npcs,
        EnemyDatabase enemies,
        OracleSaveData? saveData = null,
        OracleRuntimeState? runtimeState = null)
    {
        return CreateManager(worldRoot, npcs, enemies, new ItemDropDatabase(enemies.AssetRoot),
            new TimePortalDatabase(), new OracleRandom(), saveData, runtimeState);
    }

    internal static RoomEntityManager CreateManager(
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
        return new RoomEntityManager(worldRoot, RequireAgesEntityPack(npcs, enemies, rooms),
            owner => owner.BindAgesFactory(npcs, enemies, timePortals, rooms), random, saveData,
            runtimeState ?? OracleRuntimeState.CreateInitialized(OracleGame.Ages),
            inventory, treasures ?? TreasureDatabase.For(enemies.AssetRoot), itemDrops, animationTick);
    }

    private static string RequireAgesEntityPack(NpcDatabase npcs, EnemyDatabase enemies, RoomSession? rooms)
    {
        if (enemies.AssetRoot != AgesGameContent.AssetRoot || npcs.AssetRoot != enemies.AssetRoot || rooms is not null && rooms.AssetRoot != enemies.AssetRoot)
            throw new ArgumentException("The Ages entity binding requires matching Ages source packs.");
        return enemies.AssetRoot;
    }

    public string AssetRoot => enemies.AssetRoot;
    public bool CanReconstructDebugSpecialObject(OracleRuntimeState memory, int group, int room) =>
        memory.ReadWramByte(0xd101) switch
        {
            SpecialObjectId.Minecart => memory.ReadField("wLinkObjectIndex") == 0xd1 &&
                (group >= 4 || rooms?.GetDungeonIndex(group, room) >= 0) &&
                MinecartRoomEntity.CanReconstruct(memory, MinecartData.For(AssetRoot)),
            SpecialObjectId.Ricky or SpecialObjectId.Dimitri or SpecialObjectId.Moosh =>
                _common.CanReconstructCompanion(memory),
            SpecialObjectId.Raft => RaftRoomEntity.CanReconstruct(memory, Resources.Raft.Behavior),
            _ => false
        };
    public void ValidateDebugNativeInteractions(OracleRuntimeState memory)
    {
        for (int slot = 2; slot < 16; slot++)
        {
            int at = 0xd040 + slot * 0x100;
            if (memory.ReadWramByte(at) != 0 && memory.ReadWramByte(at + 1) == 0x7c)
                throw new NotSupportedException($"screenDistortion.s: INTERAC $7c slot${at:x4} has no transient debug reconstruction.");
            if (memory.ReadWramByte(at) != 0 && memory.ReadWramByte(at + 1) == 0x16 &&
                !MinecartInteractionRoomEntity.CanReconstruct(memory, slot, MinecartData.For(AssetRoot)))
                throw new NotSupportedException($"INTERAC $16 slot${at:x4} state${memory.ReadWramByte(at + 4):x2}: native debug reconstruction is unsupported.");
            if (memory.ReadWramByte(at) == 0 || memory.ReadWramByte(at + 1) != 0xe6) continue;
            if (!RaftInteractionRoomEntity.CanReconstruct(memory, slot, Resources.Raft.Behavior))
                throw new NotSupportedException($"INTERAC $e6:${memory.ReadWramByte(at + 2):x2} slot${at:x4} state${memory.ReadWramByte(at + 4):x2}: native debug reconstruction is unsupported.");
        }
    }
    public int OpenShutterTile => Resources.DungeonMechanics.OpenTile;
    private readonly RoomEntityManager owner;
    private readonly EnemyDatabase enemies;
    private readonly TimePortalDatabase timePortals;
    private readonly RoomSession? rooms;
    internal AgesRoomEntityResources Resources { get; }
    internal FairiesWoodsEvent? FairiesWoods { get; set; }
    internal CompanionForestEvent? CompanionForest { get; set; }
    internal AgesRoomEnemyFactory EnemyFactory { get; }

    private readonly BipinBlossomFamilyStateResolver _familyState;
    private FamilyChildMotionDatabase? _childMotion;
    private readonly ItemDropDatabase _itemDrops;
    private readonly OracleRandom _random;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtimeState;
    private readonly InventoryState? _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly Func<long> _animationTick;
    private readonly MovingPlatformRidingState _platformRiding;
    private readonly CommonRoomEntityFactory _common;
    private AgesRickyStateAController? _rickyStateA;
    private AgesDimitriStateAController? _dimitriStateA;
    private void UpdateDimitriStateA(DimitriCompanionRoomEntity body,RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>
        (_dimitriStateA??=new(_runtimeState,owner.OnSoundRequested)).Update(body,frame,spawns);
    private void UpdateRickyStateA(RickyCompanionRoomEntity body,RoomEntityFrame frame)=>
        (_rickyStateA??=new(_runtimeState,rooms??throw new InvalidOperationException("Ages rickyStateA requires its live room owner."),
            Resources.RickyBody,Resources.RickyDeparture,owner.IsTextActive,owner.OnRoomEntityDialogueRequested,owner.OnSoundRequested)).Update(body,frame);

    internal AgesRoomEntityFactory(RoomEntityManager owner, EnemyDatabase enemies,
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
        Resources = AgesRoomEntityResources.CreateSession(rooms);
        _common = new CommonRoomEntityFactory(owner,() => Resources.SwordBeam,rooms,UpdateRickyStateA,UpdateDimitriStateA);
        EnemyFactory = new(owner, enemies, Resources, rooms);
    }

    public IEnumerable<bool> PrepareResources() => Resources.PrepareResources();
    public bool TryCreateRoomInitializationOverride(int group, OracleRoomData room, out IRoomEntity? entity)
    {
        entity = null;
        // initializeRoom tail-jumps to checked INTERAC_SCREEN_DISTORTION creation.
        if (_runtimeState.ReadField("wSentBackByStrangeForce") != 1) return false;
        if (owner.InteractionSlotAvailable) entity = new ScreenDistortionRoomEntity(owner);
        return true;
    }
    public void RefreshState(IEnumerable<IRoomEntity> entities) => owner.RefreshAgesNpcState(entities);
    public void BindEntity(IRoomEntity entity) => owner.BindAgesEntity(entity);

    public void UpdateSeedTreeRefillState(
        int activeGroup,
        int activeRoom)
    {
        if ((owner.ActiveRoom.TilesetFlags & (int)TilesetFlags.Outdoors) != 0)
            Resources.SeedTrees.UpdateRefillState(_runtimeState, activeGroup, activeRoom);
    }

    public IEnumerable<IRoomEntity> CreateRoomInitializationEntities(int group, OracleRoomData room)
    {
        if (owner.ReconstructingDebugObjects && _saveData is not null)
            for (int slot = 2; slot < 16; slot++)
            {
                int at = 0xd040 + slot * 0x100;
                if (_runtimeState.ReadWramByte(at) != 0 && _runtimeState.ReadWramByte(at + 1) == 0x16)
                {
                    Vector2 cartPosition = new(_runtimeState.ReadWramByte(at + 0x0d), _runtimeState.ReadWramByte(at + 0x0b));
                    yield return new MinecartInteractionRoomEntity(new MinecartInteractionSpawn(cartPosition,
                        NativeReconstructionSlot: slot), room, _runtimeState,
                        MinecartData.For(AssetRoot).Visual, owner);
                    continue;
                }
                if (_runtimeState.ReadWramByte(at) == 0 || _runtimeState.ReadWramByte(at + 1) != 0xe6) continue;
                Vector2 position = new(_runtimeState.ReadWramByte(at + 0x0d) + _runtimeState.ReadWramByte(at + 0x0c) / 256f,
                    _runtimeState.ReadWramByte(at + 0x0b) + _runtimeState.ReadWramByte(at + 0x0a) / 256f);
                yield return new RaftInteractionRoomEntity(new RaftInteractionSpawn(position, group, room.Id,
                    _runtimeState.ReadWramByte(at + 2), NativeReconstructionSlot: slot),
                    room, Resources.Raft.Behavior, _saveData, _runtimeState, owner);
            }
        int activeGroup = group;
        if(Resources.TokayDimitri.Matches(activeGroup,room.Id)&&!CompanionRuntimeState.AnyActive(_runtimeState))
            CompanionRuntimeState.ClearScriptScratch(_runtimeState);
        // initializeRoom calls createSeaEffectsPartIfApplicable after room
        // code, before companions and the ordered placed object stream.
        if (SuctionPitDatabase.For(AssetRoot).ShouldSpawn(room) && owner.PartSlotAvailable)
            yield return new SuctionPitRoomEntity(owner);
        // bank0.initializeRoom calls checkLoadPirateShip after sea effects,
        // before parsing companions and placed objects. This creates the
        // reserved $d1 ship for destination state-0 scrolling preload without
        // advancing the global course (which only runs in cutscene01).
        owner.CheckLoadPirateShip();
        bool companionSlotActive = CompanionRuntimeState.AnyActive(_runtimeState);
        // A scrolling native owner survives in the outgoing set. Rebuild only
        // after that presentation has actually been retired (load/restore).
        bool rebuildCompanion = !owner.ScreenTransitionActive && companionSlotActive && owner.SpecialObjectPresentationAvailable;
        IRoomEntity? companionEntity = null;
        if (rebuildCompanion && CompanionRuntimeState.IsActive(_runtimeState, CompanionRuntimeState.DimitriId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            bool riding = _runtimeState.ReadField("wLinkObjectIndex") == 0xd1;
            IRoomEntity dimitri = _runtimeState.ReadWramByte(0xd104) == 0 ? CompanionScripts.CreateBody(SpecialObjectId.Dimitri, active.Position)
                : CreateDimitri(new(active.Position, active.Direction, activeGroup, room.Id, Riding: riding), room);
            companionEntity = dimitri;
        }
        else if (rebuildCompanion && CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RaftId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            if (_saveData is not null)
            {
                companionEntity = new RaftRoomEntity(
                    new RaftSpawn(active.Position, active.Direction,
                        activeGroup, room.Id),
                    room, Resources.Raft.Behavior, _saveData, _runtimeState, owner);
            }
        }
        else if (rebuildCompanion && CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RickyId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            IRoomEntity ricky = _runtimeState.ReadWramByte(0xd104) == 0 ? CompanionScripts.CreateBody(SpecialObjectId.Ricky, active.Position) : _common.CreateRicky(new RickyCompanionSpawn(
                active.Position, active.Direction, activeGroup, room.Id,
                Riding: _runtimeState.ReadField("wLinkObjectIndex") == 0xd1), room);
            companionEntity = ricky;
        }
        else if (rebuildCompanion && CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.MooshId))
        {
            ActiveCompanion active = CompanionRuntimeState.Read(_runtimeState);
            companionEntity = _runtimeState.ReadWramByte(0xd104) == 0
                ? Resources.MooshGoodbye.Matches(activeGroup, room.Id)
                    ? (MooshGoodbye ?? throw new InvalidOperationException("Moosh native initialization requires its Ages content binding.")).CreateBody(active.Position)
                    : CompanionScripts.CreateBody(SpecialObjectId.Moosh, active.Position)
                : CreateMoosh(new MooshCompanionSpawn(
                active.Position, active.Direction, activeGroup, room.Id,
                Riding: _runtimeState.ReadField("wLinkObjectIndex") == 0xd1), room);
        }
        if (companionEntity is not null)
            yield return companionEntity;
    }

    public IRoomEntity CreateRememberedCompanion(RememberedCompanionAdmission source, OracleRoomData room)
    {
        if (source.IsInteraction)
        {
            if (source is not { SpecialObjectId: 0x13, InteractionId: 0xe6, InteractionSubId: 2 } || _saveData is null)
                throw new NotSupportedException($"{source.Source}: unbound remembered interaction content.");
            return new RaftInteractionRoomEntity(new RaftInteractionSpawn(source.Position, room.Group, room.Id),
                room, Resources.Raft.Behavior, _saveData, _runtimeState, owner);
        }
        if (source.SpecialObjectId is not (SpecialObjectId.Ricky or SpecialObjectId.Dimitri or SpecialObjectId.Moosh))
            throw new NotSupportedException($"{source.Source}: no native animal body binding.");
        if (_runtimeState.ReadWramByte(0xd104) != 0)
            throw new NotSupportedException($"{source.Source}: retained state ${_runtimeState.ReadWramByte(0xd104):x2} has no fresh remembered-body binding.");
        return source.SpecialObjectId == SpecialObjectId.Moosh && Resources.MooshGoodbye.Matches(room.Group, room.Id)
            ? (MooshGoodbye ?? throw new InvalidOperationException($"{source.Source}: the room's native Moosh host is unbound.")).CreateBody(source.Position)
            : CompanionScripts.CreateBody(source.SpecialObjectId, source.Position);
    }

    public IRoomEntity CreateMaple(OracleRoomData room)
    {
        if (_runtimeState.ReadWramByte(0xd104) != 0)
            throw new NotSupportedException($"code/roomInitialization.s:checkAndSpawnMaple: room ${room.Group:x1}:${room.Id:x2} retains SPECIALOBJECT $0e state ${_runtimeState.ReadWramByte(0xd104):x2}; encounter reconstruction is unbound.");
        if (_saveData is null || _inventory is null)
            throw new InvalidOperationException("SPECIALOBJECT $0e encounter content requires the live save and inventory owners.");
        var encounterState = new MapleEncounterState(owner.RuntimeState);
        var maple = new MapleEncounter
        {
            Name = "Maple",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        maple.Initialize(
            room.Group,
            Resources.Maple,
            encounterState,
            room,
            _random,
            _saveData!,
            _inventory!,
            owner.OnMapleDialogueRequested,
            owner.IsTextActive,
            owner.OnSoundRequested,
            owner.BeginHorizontalScreenShake,
            owner.OnRoomMusicRequested);
        return new MapleEncounterRoomEntity(maple, owner);
    }

    public IEnumerable<IRoomEntity> CreateRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        using var diagnostics = OracleRoomLoadDiagnostics.Begin("factory", group, room.Id);
        if (_saveData is not null && Resources.VireEntrance.Matches(group, room.Id))
            yield return owner.VireEntranceFactory?.Invoke(Resources.VireEntrance) ??
                throw new InvalidOperationException($"{Resources.VireEntrance.Source}: active $b8:$00 requires its Ages story binding.");
        if (_saveData is not null && Resources.FairiesWoods.TryController(group, room.Id, out var fairyController) && owner.InteractionSlotAvailable)
            yield return new FairyHidingMinigameRoomEntity(fairyController, Resources.FairiesWoods,
                _saveData, _runtimeState, () => FairiesWoods);
        var forestObjects = Resources.CompanionForest.RoomObjects(group, room.Id);
        int forestPrefixEnd = _saveData is not null && Resources.FairiesWoods.TryController(group, room.Id, out _) ? 1 : 0;
        if (_saveData is not null)
            foreach (var record in forestObjects)
            {
                if (record.Order != forestPrefixEnd) break;
                if (!owner.InteractionSlotAvailable) break;
                yield return CreateForestObject(record, room);
                forestPrefixEnd++;
            }
        bool hasKeyholeController = Resources.KeyholeControllers.TryGet(group, room.Id, out var keyholeController)
            && _saveData is not null;
        // A leading controller allocates immediately; later controllers are
        // merged with their actual preceding companion placements below.
        if (hasKeyholeController && keyholeController.Order == 0)
            yield return new KeyholeControllerRoomEntity(keyholeController, _saveData!, rooms);
        var reservations = new EnemyPlacementReservations(_runtimeState);
        var deferredMermaidEnemies = new List<DungeonObjectRecord>();
        int activeGroup = group;
        Resources.KingMoblin.ApplyRoomLayout(group,room,_animationTick());
        if (group == Resources.Patch.ResetGroup && room.Id == Resources.Patch.ResetRoom)
            for (int address = 0xcfd0; address < 0xcfd8; address++) _runtimeState.SetWramByte(address, 0);
        foreach (var waterfall in Resources.WaterfallWarps.Records)
            if (waterfall.Group == group && waterfall.Room == room.Id && _saveData is not null)
                yield return new WaterfallWarpRoomEntity(waterfall, _saveData, _runtimeState,
                    () => _runtimeState.ReadField("wLinkObjectIndex") == 0xd1 &&
                        CompanionRuntimeState.IsActive(_runtimeState, CompanionRuntimeState.DimitriId), owner.OnRoomWarpRequested);
        if (_saveData is not null && !owner.ReconstructingDebugObjects)
            foreach (RaftPlacement placement in Resources.Raft.GetPlacements(activeGroup, room.Id))
                yield return new RaftInteractionRoomEntity(
                    new RaftInteractionSpawn(new(placement.X, placement.Y), activeGroup, room.Id, placement.SubId),
                    room, Resources.Raft.Behavior, _saveData, _runtimeState, owner);
        if (_saveData is not null)
        {
            var placements = new Dictionary<int, IRoomEntity>();
            if (Resources.MooshGoodbye.Matches(group, room.Id))
            {
                MooshGoodbyeEvent goodbye = MooshGoodbye ?? throw new InvalidOperationException(
                    "mainData.s:group0Map6bObjectData requires its Ages native companion binding.");
                foreach (MooshGoodbyePlacementRecord placement in Resources.MooshGoodbye.Placements)
                {
                    if (!owner.InteractionSlotAvailable) break;
                    yield return placement.Id switch
                    {
                        0x71 => new CompanionBarrierRoomEntity(Resources.CompanionBarriers.GetRoomRecords(group, room.Id).Single(),
                            _saveData, _runtimeState, owner.SetSpecialObjectBarrierCoordinate, owner.OnRoomEntityDialogueRequested)
                            { NativeAllocationEnabled = true },
                        0x67 => new CompanionPresetSpawnerRoomEntity(placement.Id, placement.SubId, goodbye.SpawnPreset),
                        _ => throw new NotSupportedException($"{placement.Source}: unsupported native interaction ${placement.Id:x2}:${placement.SubId:x2}.")
                    };
                }
            }
            if (Resources.Ricky.Matches(group, room.Id))
            {
                RickyGlovesEvent ricky = RickyGloves ?? throw new InvalidOperationException(
                    "mainData.s:group0Map6aObjectData requires its Ages native interaction bindings.");
                foreach (RickyGlovesPlacementRecord placement in Resources.Ricky.Placements)
                {
                    if (!owner.InteractionSlotAvailable) break;
                    yield return placement.Id switch
                    {
                        0x31 => new ImpaEncounterRoomEntity(NpcCharacter.CreateFromRecord(
                            NpcDatabase.For(AssetRoot).GetRoomNpcs(group, room.Id).Single(npc =>
                                npc.Id == placement.Id && npc.SubId == placement.SubId), AssetRoot)),
                        0xdc => new RoomTileChangeWatcherRoomEntity(
                            Resources.TileChangeWatchers.GetRoomRecords(group, room.Id).Single(), room, _saveData),
                        0x71 when placement.SubId is 4 or 5 => new CompanionBarrierRoomEntity(
                            Resources.CompanionBarriers.GetRoomRecords(group, room.Id).Single(record => record.SubId == placement.SubId),
                            _saveData, _runtimeState, owner.SetSpecialObjectBarrierCoordinate, owner.OnRoomEntityDialogueRequested)
                            { NativeAllocationEnabled = true },
                        0x67 => new CompanionPresetSpawnerRoomEntity(placement.Id, placement.SubId, ricky.SpawnPreset),
                        0x71 when placement.SubId == 3 => new RickyGlovesControllerRoomEntity(ricky),
                        _ => throw new NotSupportedException($"{placement.Source}: unsupported native interaction ${placement.Id:x2}:${placement.SubId:x2}.")
                    };
                }
            }
            if (Resources.Moosh.Matches(group, room.Id))
            {
                MooshRescueEvent rescue = MooshRescue ?? throw new InvalidOperationException(
                    "mainData.s:group0Map6cObjectData requires its Ages native interaction bindings.");
                foreach (MooshRescuePlacementRecord placement in Resources.Moosh.Placements)
                {
                    if (!owner.InteractionSlotAvailable) break;
                    yield return placement.Id switch
                    {
                        0x73 => new MooshRescueGhiniRoomEntity(NpcCharacter.CreateFromRecord(
                            NpcDatabase.For(AssetRoot).GetRoomNpcs(group, room.Id).Single(npc =>
                                npc.Id == placement.Id && npc.SubId == placement.SubId), AssetRoot), rescue),
                        0x71 when placement.SubId == 0 => new MooshRescueControllerRoomEntity(rescue),
                        0x71 when placement.SubId == 2 => new CompanionBarrierRoomEntity(
                            Resources.CompanionBarriers.GetRoomRecords(group, room.Id).Single(), _saveData, _runtimeState,
                            owner.SetSpecialObjectBarrierCoordinate, owner.OnRoomEntityDialogueRequested) { NativeAllocationEnabled = true },
                        0x67 when placement.SubId == 0 => new CompanionPresetSpawnerRoomEntity(placement.Id, placement.SubId, rescue.SpawnPreset),
                        _ => throw new NotSupportedException($"{placement.Source}: unsupported native interaction ${placement.Id:x2}:${placement.SubId:x2}.")
                    };
                }
            }
            foreach (var ship in PirateShipDatabase.Shared.Placements)
                if (ship.Group == group && ship.Room == room.Id &&
                    _saveData.IsLinkedGame == (ship.SubId == 2) && _saveData.HasGlobalFlag(AgesGlobalFlag.PiratesGone) &&
                    !_saveData.HasRoomFlag(group, room.Id, PirateShipRoomEntity.DepartureCompletedRoomFlag))
                    placements.Add(ship.Order, new PirateShipRoomEntity(owner,
                        PirateShipDatabase.Shared.Ship(group, room.Id, ship.SubId, ship.Y, ship.X)));
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
                Resources.CompanionBarriers.GetRoomRecords(activeGroup, room.Id).Where(_ => !Resources.Moosh.Matches(group, room.Id) &&
                    !Resources.Ricky.Matches(group, room.Id) && !Resources.MooshGoodbye.Matches(group, room.Id)))
            {
                placements.Add(barrier.Order, new CompanionBarrierRoomEntity(
                    barrier,
                    _saveData,
                    _runtimeState,
                    owner.SetSpecialObjectBarrierCoordinate,
                    owner.OnRoomEntityDialogueRequested)
                    {NativeAllocationEnabled=Resources.DimitriDeparture.Matches(group,room.Id)});
            }
            foreach(var source in Resources.DimitriDeparture.Placements.Where(p=>p.Group==group && p.Room==room.Id))
                placements.Add(source.Order,new DimitriDepartureRoomEntity(rooms!,owner,Resources.DimitriDeparture));
            foreach(var source in Resources.GroundTreasures.GetRoomRecords(group,room.Id).Where(p=>p.NativeSpawnerId==0x74))
                placements.Add(source.Order,new RickyGloveSpawnerRoomEntity(source,_saveData,
                    _inventory??throw new InvalidOperationException("$74 requires the live inventory owner."),owner));
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
        bool spawnMaple = owner.MaplePresent();

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
                yield return _common.CreateSpinner(spinner);
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
            if (record is { Id: AgesInteractionId.TriggerTranslator, SubId: 0x02 } or
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
        IReadOnlyList<DungeonObjectRecord> laterDungeonRecords = Resources.LaterDungeonRecords(group, room.Id);
        if (laterDungeonRecords.Any(record => record.Kind == DungeonObjectKind.MermaidTorchOrder))
            lightableTorchState ??= new LightableTorchState();
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
        // Equal-order precedence matches the source merge; sort operands before constructing any live object.
        var dungeonPlacements = dungeonRecords.Select((record, index) => (record.Order, Family: 0, Index: index))
            .Concat(moonlitGrottoRecords.Select((record, index) => (record.Order, Family: 1, Index: index)))
            .Concat(wingDungeonRecords.Select((record, index) => (record.Order, Family: 2, Index: index)))
            .Concat(spiritsGraveRecords.Select((record, index) => (record.Order, Family: 3, Index: index)))
            .Concat(sharedDungeonRecords.Select((record, index) => (record.Order, Family: 4, Index: index)))
            .Concat(laterDungeonRecords.Select((record, index) => (record.Order, Family: 5, Index: index)))
            .OrderBy(record => record.Order).ThenBy(record => record.Family);
        foreach (var placement in dungeonPlacements)
        {
            if (placement.Family == 5)
            {
                var record = laterDungeonRecords[placement.Index];
                if (!DungeonObjectConditionMet(record)) continue;
                if (record.Kind is DungeonObjectKind.CavePortal or DungeonObjectKind.CavePuzzle or DungeonObjectKind.Lever or DungeonObjectKind.LeverLavaFiller && !owner.InteractionSlotAvailable)
                    continue;
                if (record.Kind == DungeonObjectKind.Twinrova)
                {
                    foreach (var entity in owner.PlaceEnemies(Resources.Twinrova.EntrySource(record), room, placementContext, reservations)) yield return entity;
                    continue;
                }
                if (record.Kind is DungeonObjectKind.Octogon or DungeonObjectKind.Vire)
                {
                    deferredMermaidEnemies.Add(record);
                    continue;
                }
                if (record.Kind == DungeonObjectKind.SeedShooterEyeStatue && !owner.PartSlotAvailable)
                    continue;
                if (record.Kind is DungeonObjectKind.AnglerFish or DungeonObjectKind.Plasmarine or DungeonObjectKind.BlueStalfos or DungeonObjectKind.Ramrock && !owner.EnemySlotAvailable)
                    continue;
                if (record.Kind is DungeonObjectKind.StonePanel or DungeonObjectKind.Sarcophagus or DungeonObjectKind.EnemyAttachedKey or DungeonObjectKind.SlateSlot or DungeonObjectKind.PanelStairs or DungeonObjectKind.JabuWater or DungeonObjectKind.JabuLadder or DungeonObjectKind.JabuHookKey or DungeonObjectKind.OctogonInitializer or DungeonObjectKind.SmogController or DungeonObjectKind.PushBlockSynchronizer
                    or DungeonObjectKind.WallSquish or DungeonObjectKind.ButtonBridge or DungeonObjectKind.PuzzleTrapReset
                    or DungeonObjectKind.FinalDungeonEnergy or DungeonObjectKind.Decoration or DungeonObjectKind.EyePuzzleExit
                    or DungeonObjectKind.TwinrovaFlame or DungeonObjectKind.ZeldaApproach or DungeonObjectKind.Zelda
                    or DungeonObjectKind.PatternHint or DungeonObjectKind.TilePatternChest or DungeonObjectKind.TriggerChestScript
                    or DungeonObjectKind.BossReward or DungeonObjectKind.MinibossReward or DungeonObjectKind.Essence or DungeonObjectKind.OrbChest
                    or DungeonObjectKind.TileFiller or DungeonObjectKind.FloorFillChest or DungeonObjectKind.BlueFlameChest
                    or DungeonObjectKind.ColoredCube or DungeonObjectKind.CubeLightSensor or DungeonObjectKind.CubeFloorSensor or DungeonObjectKind.CubeFlame
                    or DungeonObjectKind.FloorPatternTrigger or DungeonObjectKind.FloorPatternKey or DungeonObjectKind.ToggleFloor or DungeonObjectKind.FloorColorChanger or DungeonObjectKind.MermaidChangingFloor or DungeonObjectKind.MermaidBossKey
                    or DungeonObjectKind.FloorSwitchBit or DungeonObjectKind.CubeSwitchSensor or DungeonObjectKind.MinecartGate or DungeonObjectKind.RoomFlagMirror or DungeonObjectKind.MermaidTorchOrder or DungeonObjectKind.Spinner or DungeonObjectKind.BossKeyMirror or DungeonObjectKind.SignalScript or DungeonObjectKind.TileScript)
                {
                    if (!owner.InteractionSlotAvailable) continue;
                }
                if (record.Kind is DungeonObjectKind.SharedDungeonScript or DungeonObjectKind.SwitchTrigger or DungeonObjectKind.RestoreCrystals or DungeonObjectKind.LinkStatue or DungeonObjectKind.EntryText or DungeonObjectKind.SymmetryBarrier &&
                    !owner.InteractionSlotAvailable) continue;
                if (record.Kind == DungeonObjectKind.Lever)
                {
                    LeverProfile profile = Resources.Levers.Profile(record.SubId);
                    var lever = new LeverRoomEntity(profile.ToNpcRecord(record),
                        new LeverState(_runtimeState, (record.SubId & 0x40) == 0
                            ? AgesWramAddress.wLever1PullDistance : AgesWramAddress.wLever2PullDistance),
                        profile.Behavior, owner.OnSoundRequested,
                        parent => owner.TryCreateLeverConnection(parent,profile.ToNpcRecord(record,connection:true),profile));
                    yield return lever;
                    continue;
                }
                yield return record.Kind switch
                {
                    DungeonObjectKind.SharedDungeonScript => CreateSharedDungeonScript(record, lightableTorchState),
                    DungeonObjectKind.SwitchTrigger => new SwitchTriggerTranslatorRoomEntity(record, _runtimeState, owner.SetTrigger, owner.IsOutgoingEntity),
                    DungeonObjectKind.RestoreCrystals => new DungeonCrystalRestoreRoomEntity(record, Resources.SharedInteractions, _saveData!, _runtimeState),
                    DungeonObjectKind.LinkStatue => CreateSharedDecoration(record, room),
                    DungeonObjectKind.EntryText => new RoomEntryTextRoomEntity(record, _runtimeState, player => {
                        var text = Resources.SharedInteractions.EntryText(owner.ActiveRoom.Id);
                        owner.OnRoomEntityDialogueRequested(owner.ActiveRoom.Id == 0xd0 ? 0x0209 : 0x120a,
                            text.Position < 0 ? text.Message : $"\\pos({text.Position}){text.Message}", player.Position);
                    }),
                    DungeonObjectKind.SymmetryBarrier => new SymmetryCompanionBarrierRoomEntity(record, _saveData!, _runtimeState,
                        () => owner.EntityAdapters<IRoomEntity>().Where(e => e is not IRoomEntityLifetime { Finished: true }).OfType<ICompanionBarrierTarget>().FirstOrDefault(),
                        (companion, position) => {
                            var text = Resources.SharedInteractions.SymmetryText(companion);
                            owner.OnRoomEntityDialogueRequested(text.Id,
                                text.Position < 0 ? text.Message : $"\\pos({text.Position}){text.Message}", position);
                        }),
                    DungeonObjectKind.CavePortal => CreateHerosCavePortal(record),
                    DungeonObjectKind.CavePuzzle => new HerosCavePuzzleRoomEntity(record, Resources.HerosCave,
                        owner, _saveData!, _runtimeState, skullDungeonPuzzle, (position, tile) => {
                            if (rooms!.TrySetTile(position, tile)) owner.OnRoomTileChanged();
                        }, record.SubId == 0x18 ? CreateFallingSmallKeyRequest(record)
                            : new GroundTreasureGrantRequest(record.Group, record.Room, record.Order, record.Y, record.X,
                                "TREASURE_OBJECT_RUPEES_16", record.Source) { SpawnMode = TreasureSpawnMode.Instant }),
                    DungeonObjectKind.Zelda => owner.RoomOfRitesZeldaFactory?.Invoke(NpcDatabase.For(AssetRoot).AllRecords.Single(n =>
                        n.Group == record.Group && n.Room == record.Room && n.Id == record.Id && n.SubId == record.SubId &&
                        n.X == record.X && n.Y == record.Y)) ??
                        throw new InvalidOperationException($"{record.Source}: Zelda $ad:$00 requires its native script owner."),
                    DungeonObjectKind.TwinrovaFlame => new TwinrovaFlameRoomEntity(_saveData!, group, room.Id, record.SubId,
                        Resources.RoomOfRitesRitual.Flame(record.SubId)),
                    DungeonObjectKind.ZeldaApproach => owner.ZeldaApproachFactory?.Invoke(record, Resources.RoomOfRitesRitual) ??
                        throw new InvalidOperationException($"{record.Source}: Zelda approach $da:$00 requires its native cutscene owner."),
                    DungeonObjectKind.FinalDungeonEnergy => owner.FinalDungeonEnergyFactory?.Invoke(Resources.FinalDungeonEntry) ??
                        throw new InvalidOperationException($"{record.Source}: finalDungeonEnergy $b5:$00 requires its native lifecycle owner."),
                    DungeonObjectKind.Decoration => new DecorationRoomEntity(record, Resources.FinalDungeonEntry.Decoration),
                    DungeonObjectKind.EyePuzzleExit => new EyePuzzleExitRoomEntity(_runtimeState, placementContext.ScrollDirection, owner.OnSoundRequested),
                    DungeonObjectKind.TileScript => CreateDungeonTileScript(record, lightableTorchState),
                    DungeonObjectKind.EnemyAttachedKey => CreateEnemyAttachedKey(record),
                    DungeonObjectKind.Sarcophagus => new SarcophagusRoomEntity(record,
                        Resources.DungeonVisuals.Visual("sarcophagus"), Resources.DungeonVisuals.Visual("grotto-crystal-break"),
                        room, _saveData!, _runtimeState, (position, tile) => {
                            if (rooms!.TrySetTile((byte)room.GetPackedPosition(position), tile)) owner.OnRoomTileChanged();
                        }, owner.OnNativeSoundRequested),
                    DungeonObjectKind.StonePanel => new StonePanelRoomEntity(record,
                        Resources.DungeonVisuals.Visual(record.SubId == 0 ? "stone-panel-left" : "stone-panel-right"),
                        Resources.AncientTomb.PanelPalette,room,_saveData!,_runtimeState,() => owner.ActiveTriggers,
                        owner.SetTrigger,owner.OnNativeSoundRequested,owner.NativeActiveMusicSource),
                    DungeonObjectKind.PanelStairs => new PanelStairsRoomEntity(record,_saveData!,
                        () => owner.ActiveTriggers,owner.SetTrigger,() => lightableTorchState?.LitCount ?? 0,
                        owner.BeginScreenShake,owner.OnNativeSoundRequested,owner.TryCreatePuzzlePuff,(position,tile) => {
                            if (rooms!.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        }),
                    DungeonObjectKind.SlateSlot => new SlateSlotRoomEntity(record,Resources.AncientTomb,
                        _saveData!,_inventory!,_runtimeState,(position,tile) => {
                            if (rooms!.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        },owner.OnNativeSoundRequested,() => owner.OnRoomEntityDialogueRequested(
                            0x5111,Resources.AncientTomb.Text(0x5111),record.Position)),
                    DungeonObjectKind.JabuWater => new JabuWaterControllerRoomEntity(
                        _saveData ?? throw new InvalidOperationException($"{record.Source}: Jabu water requires save state."),
                        _runtimeState,() => (byte)owner.ActiveTriggers,owner.OnNativeSoundRequested,owner.BeginScreenShake,
                        id => owner.OnRoomEntityDialogueRequested(id,Resources.JabuDungeon.Text(id),record.Position),
                        owner.NativeActiveMusicSource),
                    DungeonObjectKind.JabuLadder => new JabuLadderRoomEntity(record,
                        () => owner.ActiveRoomHasFlag(_saveData!,0x80),() => {
                            _saveData!.SetRoomFlag(owner.ActiveRoom.Group,owner.ActiveRoom.Id,0x80);
                            _saveData.SetRoomFlag(5,0x4d,0x80);
                        },() => owner.RoomEnemyCount,(position,tile) => {
                            if (rooms!.TrySetTile(position,tile)) owner.OnRoomTileChanged();
                        },owner.TryCreatePuzzlePuff,owner.OnNativeSoundRequested,owner.EnableLinkCollisionsAndMenu),
                    DungeonObjectKind.JabuHookKey => new DungeonPatternKeyRoomEntity(record,
                        () => owner.ActiveRoom,0xdb,Resources.JabuDungeon.HookKeyPattern,
                        CreateFallingSmallKeyRequest(record),DungeonChestItemFlagSet,owner.TryCreateGroundTreasure,owner.IsOutgoingEntity),
                    DungeonObjectKind.ArmosWarrior => CreateArmosWarrior(record, room, placementContext),
                    DungeonObjectKind.Smasher => CreateSmasher(record, room, placementContext),
                    DungeonObjectKind.BlueStalfos => CreateBlueStalfos(record, room, placementContext),
                    DungeonObjectKind.Ramrock => CreateRamrock(record, room, placementContext),
                    DungeonObjectKind.AnglerFish => CreateAnglerFish(record,room,placementContext),
                    DungeonObjectKind.Plasmarine => CreatePlasmarine(record,placementContext),
                    DungeonObjectKind.OctogonInitializer => new OctogonEncounterInitializerRoomEntity(
                        Resources.MermaidDungeon.OctogonInitialization,_runtimeState),
                    DungeonObjectKind.SignalScript => CreateDungeonSignalScript(record),
                    DungeonObjectKind.RoomFlagMirror => CreateRoomFlagMirror(record),
                    DungeonObjectKind.Spinner => _common.CreateSpinner(record),
                    DungeonObjectKind.BossKeyMirror => new DungeonBossKeyMirrorRoomEntity(
                        () => owner.ActiveRoomHasFlag(_saveData ?? throw new InvalidOperationException($"{record.Source}: boss-key mirror requires save state."),0x20),
                        () => (_inventory ?? throw new InvalidOperationException($"{record.Source}: boss-key mirror requires inventory."))
                            .GrantDungeonBossKey(Resources.BossKeyMirrors.Dungeon(record))) { Visible=false,Name="DungeonBossKeyMirror" },
                    DungeonObjectKind.SmogSentinel => CreateSmogSentinel(record, room, placementContext),
                    DungeonObjectKind.SmogController => CreateSmogEncounter(new(record.X,record.Y),room),
                    DungeonObjectKind.PushBlockSynchronizer => new PushBlockSynchronizerRoomEntity(
                        room,Resources.PushSynchronizers,() => owner.RequiredReservedPushBlock,owner.TryCreateSynchronizedBlock),
                    DungeonObjectKind.WallSquish => new WallSquishRoomEntity(room,
                        () => rooms!.BlockPushAngle, () => _runtimeState.ReadWramByte(AgesWramAddress.wLinkRaisedFloorOffset), record.Source),
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
                        record.Position,_runtimeState,record.SubId == 1 ? 0x0f : Resources.CrownDungeon.TriggerChestValue,
                        record.SubId == 1 ? Resources.HerosCave.Constant("chest-wait") : Resources.CrownDungeon.TriggerChestWait,
                        () => owner.ActiveTriggers,() => _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem) == true,
                        owner.IsScriptTextActive,owner.OnSoundRequested,() => WriteDungeonChest(record)),
                    DungeonObjectKind.SeedShooterEyeStatue => new SeedShooterEyeStatueRoomEntity(
                        Resources.EyeStatues.GetRoomRecords(group,room.Id).Single(eye => eye.Order == record.Order),
                        Resources.EyeStatues,Resources.DungeonVisuals.Visual("seed-shooter-eye-statue"),owner.SetTrigger),
                    DungeonObjectKind.Eyesoar => CreateEyesoar(record, room, placementContext),
                    DungeonObjectKind.Essence => CreateLaterDungeonEssence(record, room),
                    DungeonObjectKind.BossReward => CreateDungeonReward(record, "TREASURE_OBJECT_HEART_CONTAINER_00", falling: false),
                    DungeonObjectKind.MinibossReward => new DungeonRewardRoomEntity(record, DungeonRewardDatabase.For(AssetRoot),
                        _runtimeState, _saveData, () => owner.RoomEnemyCount, null, owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal, scriptTextActive: owner.IsScriptTextActive),
                    DungeonObjectKind.SwitchTileToggler => CreateSwitchTileToggler(record,room),
                    DungeonObjectKind.MovingPlatform => CreateMovingPlatform(record.Position, record.SubId,
                        rooms?.GetDungeonIndex(record.Group, record.Room) ?? -1),
                    DungeonObjectKind.MovingOrb => new MovingOrbRoomEntity(record, Resources.DungeonVisuals.Visual("grotto-orb"), _runtimeState, owner.OnSoundRequested, Resources.DungeonMechanics.SwitchSound),
                    DungeonObjectKind.OrbChest => new DungeonOrbChestRoomEntity(record,_runtimeState,owner.OnSoundRequested,
                        owner.IsScriptTextActive,() => _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem) == true,
                        () => WriteDungeonChest(record)),
                    DungeonObjectKind.ToggleFloor => CreateToggleFloor(room),
                    DungeonObjectKind.FloorColorChanger => CreateFloorColorChanger(record),
                    DungeonObjectKind.LeverLavaFiller => new LeverLavaFillerRoomEntity(
                        room, Resources.LeverLava.Script(record.SubId), new LeverState(_runtimeState, AgesWramAddress.wLever1PullDistance),
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
                        () => owner.ParseNativeObjectStream("objectData78db",
                            EnemyPlacementContext.FromWarpDestination(_runtimeState.ReadWramByte(AgesWramAddress.wWarpDestPos))),
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
            if (placement.Family == 4)
            {
                PlacementRecord record =
                    sharedDungeonRecords[placement.Index];
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

            if (placement.Family == 3)
            {
                DungeonObjectRecord record =
                    spiritsGraveRecords[placement.Index];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateSpiritsGraveInteraction(
                    record, room, spiritsGravePuzzle, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            if (placement.Family == 2)
            {
                DungeonObjectRecord record =
                    wingDungeonRecords[placement.Index];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateWingDungeonInteraction(
                    record, room, wingDungeonPuzzle, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            if (placement.Family == 1)
            {
                DungeonObjectRecord record =
                    moonlitGrottoRecords[placement.Index];
                if (!DungeonObjectConditionMet(record))
                    continue;
                IRoomEntity? entity = CreateMoonlitGrottoInteraction(
                    record, room, placementContext);
                if (entity is not null)
                    yield return entity;
                continue;
            }

            DungeonMechanicDatabaseRecord mechanic = dungeonRecords[placement.Index];
            IRoomEntity? mechanicEntity = CreateDungeonMechanic(
                mechanic, room, group, enemyMechanicsSupported, placementContext,
                lightableTorchState);
            if (mechanicEntity is not null)
                yield return mechanicEntity;
        }

        foreach (var record in Resources.CollapsingFloors.InRoom(group, room.Id))
            yield return new CollapsingFloorRoomEntity(record,owner.OnSoundRequested,
                (position,tile) => owner.BreakCrackedFloor(rooms ?? throw new InvalidOperationException("INTERAC$dc:$0b requires its room session."),position,tile),
                () => owner.InteractionSlotAvailable);

        foreach (FountainPlacement fountain in Resources.Fountains.InRoom(group, room.Id))
            yield return new FountainDecorationRoomEntity(fountain, Resources.Fountains, _saveData);

        if(group==Resources.JabuExterior.Group && room.Id==Resources.JabuExterior.Room)
            foreach(byte subid in Resources.JabuExterior.ActorSubIds)
                if(owner.InteractionSlotAvailable)
                    yield return new JabuExteriorActorRoomEntity(subid,Resources.JabuExterior,owner,
                        rooms ?? throw new InvalidOperationException("INTERAC $dc:$09/$0a requires its room session."));

        if (!spawnMaple)
        {
            foreach (VolcanoPlacement record in Resources.Volcano.Placements(group, room.Id))
                yield return CreateVolcanoController(record, room);
        }

        if (_saveData is not null)
        {
            // Room 3:9e places its watcher after Impa, Nayru, and Zelda.
            // CreateNayruHouseNpcs emits it at that exact source position.
            if (!Resources.Ricky.Matches(group, room.Id) && (group != Resources.NayruHouse.Record.Group ||
                room.Id != Resources.NayruHouse.Record.Room))
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
        if (group == 1 && room.Id == 0x46)
            yield return new PalaceApproachRoomEntity(owner, rooms!, Resources.PalaceApproach);
        foreach (var button in Resources.PalaceButtons.GetRoomRecords(group, room.Id))
            yield return new PalaceButtonRoomEntity(owner, rooms ?? throw new InvalidOperationException($"{button.Source}: palace button requires live room/save ownership."), button);
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
        diagnostics?.Mark("before-npcs");
        IReadOnlyList<NpcRecord> roomNpcs =
            _familyState.ResolveRoomNpcs(
                group, room.Id, _saveData, _runtimeState).Where(n =>
                    !(_saveData is not null && Resources.Moosh.Matches(group, room.Id) && n.Id == AgesInteractionId.GhiniHarassingMoosh) &&
                    !laterDungeonRecords.Any(placed =>
                    placed.Kind == DungeonObjectKind.Zelda && placed.Id == n.Id && placed.SubId == n.SubId &&
                    placed.X == n.X && placed.Y == n.Y)).ToArray();
        diagnostics?.Mark("npc-resolution");
        if (group == 0 && roomNpcs.Any(record => record.Id == AgesInteractionId.Carpenter))
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
                yield return new CarpenterRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), Resources.Carpenters,
                    _runtimeState, _saveData, room, _animationTick());
            }
        }
        else if (roomNpcs.Any(record => record.Id == AgesInteractionId.Patch))
        {
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                if (record.Id != AgesInteractionId.Patch || record.SubId > 2)
                    throw new InvalidOperationException($"patch.s: unexpected room NPC ${record.Id:x2}:${record.SubId:x2}.");
                yield return new PatchRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
            }
        }
        else if (roomNpcs.Any(record => record.Id == AgesInteractionId.SymmetryNpc))
        {
            if (group == Resources.TuniNut.Constant("group") && room.Id == Resources.TuniNut.Constant("room"))
                yield return new TuniNutRoomEntity(Resources.TuniNut, Resources.Symmetry, _inventory, _saveData, room);
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                if (record.Id != AgesInteractionId.SymmetryNpc)
                    throw new InvalidOperationException($"symmetryNpc.s: unexpected NPC ${record.Id:x2}:${record.SubId:x2} in room ${group:x}:${room.Id:x2}.");
                yield return new SymmetryRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), Resources.Symmetry, _saveData);
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
            (room.Id == Resources.LynnaShop.Room || room.Id == Resources.HiddenShop.Room) || group == 3 && room.Id == 0xfe)
        {
            diagnostics?.Mark("shop-dispatch");
            foreach (IRoomEntity entity in CreateLynnaShop(room, roomNpcs))
                yield return entity;
        }
        else if (group == 3 && room.Id == 0xed)
        {
            var database = new LynnaShopDatabase(syrup: true);
            foreach (NpcRecord record in roomNpcs)
            {
                RequireNpcImplementation(record, NpcImplementationClassification.EventOwned);
                NpcCharacter actor = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
                actor.SetDialogue(0, string.Empty, canFace: false);
                actor.SetScriptButtonSensitive(true);
                actor.SetBlocksLink(record.Id == 0x5f);
                actor.SetCollisionRadii(record.Id == 0x5f ? database.Constant("syrup-radius-y") : 6,
                    record.Id == 0x5f ? database.Constant("syrup-radius-x") : 6);
                yield return new SyrupShopActorRoomEntity(actor);
            }
            // syrupScript_spawnShopItems allocates after the two placed actors.
            Texture2D[]? priceDigits = null;
            foreach (StockRecord stock in database.ResolveStock(_saveData))
            {
                var item = new LynnaShopItem { Name = $"SyrupStock_{stock.Order}_{stock.Item.SubId:x2}",
                    ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex };
                priceDigits ??= LynnaShopItem.BuildPriceDigitTextures(room);
                item.Initialize(stock, room, priceDigits);
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
            foreach (var placed in Resources.Library.Placements.Where(p => p.Group == group && p.Room == room.Id).OrderBy(p => p.Order))
                yield return new LibraryActorRoomEntity(NpcCharacter.CreateFromRecord(
                    Resources.Library.Actor(group, room.Id, placed.Id, placed.SubId, placed.Y, placed.X), AgesGameContent.AssetRoot),
                    Resources.Library, _saveData, _runtimeState);
            foreach (NpcRecord record in roomNpcs)
            {
                if (Resources.Ricky.Matches(group, room.Id) && record is { Id: 0x31, SubId: 0 }) continue;
                if (!owner.InteractionSlotAvailable) continue;
                switch (record.Implementation)
                {
                    case NpcImplementationClassification.OrdinaryGeneric:
                        if (record.Id == AgesInteractionId.Tokay)
                        {
                            var tokay = new TokayCharacter { Name=$"Npc_48_{record.SubId:x2}" };
                            tokay.Initialize(record, AgesGameContent.AssetRoot);
                            ConfigureTokayActor(tokay);
                            tokay.NativeAnimation=TokayAnimationMode.FaceLink;
                            yield return new TokayNpcRoomEntity(tokay);
                            break;
                        }
                        yield return record.Id == AgesInteractionId.Zora
                            ? new ZoraNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                            : new NpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot),
                                initializeStoryOnce: record is { Id: AgesInteractionId.MiscMan, SubId: 2 or 3 or 5 or 6 }
                                    or { Id: AgesInteractionId.MiscMan2, SubId: 3 }
                                    or { Id: AgesInteractionId.PastOldLady, SubId: 1 }
                                    or { Id: AgesInteractionId.OldLady, SubId: 2 }
                                    or { Id: AgesInteractionId.MaleVillager, SubId: 8 }
                                    or { Id: AgesInteractionId.MaleVillager, SubId: 7 }
                                    or { Id: AgesInteractionId.FemaleVillager, SubId: 3 }
                                    or { Id: AgesInteractionId.FemaleVillager, SubId: 4 }
                                    or { Id: AgesInteractionId.Subrosian, SubId: 0 }
                                    or { Id: AgesInteractionId.Subrosian, SubId: 2 }
                                    or { Id: AgesInteractionId.Zelda, SubId: 8 }
                                    or { Id: AgesInteractionId.Ambi, SubId: 0x0a }
                                    or { Id: AgesInteractionId.Ralph, SubId: 0x12 }
                                    or { Id: AgesInteractionId.Nayru, SubId: 0x0c }
                                    or { Id: 0x39, SubId: 2 or 3 },
                                // Both postgame actors call npcFaceLinkAndAnimate
                                // before interactionRunScript consumes the A-button.
                                animateBeforeDialogue: record is { Id: AgesInteractionId.Ambi, SubId: 0x0a }
                                    or { Id: AgesInteractionId.Ralph, SubId: 0x12 },
                                deleteIfInactiveOnInitialization: record is { Id: 0x39, SubId: 2 or 3 });
                        break;
                    case NpcImplementationClassification.SpecializedNative:
                        IRoomEntity specialized = CreateSpecializedNpc(record, room);
                        yield return specialized;
                        if (specialized.Node is TokayCharacter { Accessory: { } accessory } actor && actor.Record.SubId!=0x1d)
                            yield return accessory;
                        if (specialized is RosaNpcRoomEntity { Shovel: { } shovel })
                            yield return shovel;
                        break;
                    case NpcImplementationClassification.EventOwned:
                        if(record is {Id:0x2d,SubId:0})
                        {
                            yield return owner.VeranFaceFactory?.Invoke(record) ??
                                throw new InvalidOperationException("INTERAC_VERAN_CUTSCENE_FACE $2d:$00 requires its native script host.");
                            break;
                        }
                        if (record is {Id:0x4d,SubId:6})
                        {
                            yield return owner.BlackTowerAmbiFactory?.Invoke(record) ??
                                throw new InvalidOperationException("INTERAC_AMBI $4d:$06 requires its Black Tower event owner.");
                            break;
                        }
                        if (record is { Id: 0x37, SubId: 0x0c })
                        {
                            yield return owner.BlackTowerRalphFactory?.Invoke(record) ??
                                throw new InvalidOperationException("INTERAC_RALPH $37:$0c requires its Black Tower event owner.");
                            break;
                        }
                        if (record.Id == AgesInteractionId.PirateCaptain)
                        {
                            yield return new PirateCaptainRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), owner);
                            break;
                        }
                        if (record is { Id: 0x4d, SubId: 3 })
                        {
                            yield return owner.PostD7AmbiFactory?.Invoke(record) ??
                                throw new InvalidOperationException("INTERAC_AMBI $4d:$03 requires the post-D7 native controller.");
                            break;
                        }
                        if (record.Id == 0x32 && record.SubId == 2)
                        {
                            yield return new SeaFairyNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
                            break;
                        }
                        if (record.Id == AgesInteractionId.KingZora || record.Id == AgesInteractionId.Zora && record.SubId is 0x10 or 0x11 or 0x12)
                        {
                            yield return new OceanScriptNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
                            break;
                        }
                        yield return record is { Id: 0x6d, SubId: 0 }
                            ? new PalacePossessionRoomEntity(owner,Resources.PalacePossession,0x6d,0,new(record.X,record.Y),
                                entryDirection: placementContext.BossEntryDirection==Vector2I.Down?2:
                                    placementContext.BossEntryDirection==Vector2I.Left?3:
                                    placementContext.BossEntryDirection==Vector2I.Right?1:0)
                            : record is { Id: AgesInteractionId.Ralph, SubId: 0x09 }
                            ? new PalaceRalphRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                            : record is { Id: AgesInteractionId.Plen, SubId: 0 }
                            ? new PlenRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                            : record is { Id: AgesInteractionId.KingMoblinDefeated, SubId: 0 }
                            ? new DefeatedMoblinActorRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                            : record is { Id: AgesInteractionId.BombUpgradeFairy, SubId: 0 }
                            ? new BombUpgradeFairyRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                            : record.Id is AgesInteractionId.Goron or AgesInteractionId.GoronElder || record is {Id:AgesInteractionId.ShootingGallery,SubId:1} or {Id:AgesInteractionId.Troy,SubId:0}
                                ? new GoronCaveRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot))
                                : record is { Id: AgesInteractionId.Zelda, SubId: 0 }
                                ? owner.RoomOfRitesZeldaFactory?.Invoke(record) ?? throw new InvalidOperationException("Room $5:$f1 requires its Zelda $ad:$00 native owner.")
                                : record is { Id: 0x31, SubId: 0x07 } or { Id: 0x4c, SubId: 0x04 }
                                ? owner.ZeldaRescueFactory?.Invoke(record) ?? throw new InvalidOperationException("Room $0:$65 requires its Zelda rescue native owner.")
                                : new EventOwnedNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
                        break;
                    case NpcImplementationClassification.DeliberatelyUnsupported:
                        break;
                    default:
                        throw UnsupportedNpcClassification(record);
                }
            }
        }

        if (_saveData is not null)
            foreach (var record in forestObjects)
                if (record.Order >= forestPrefixEnd && owner.InteractionSlotAvailable) yield return CreateForestObject(record, room);

        foreach (var record in Resources.SharedInteractions.NpcTail(group, room.Id))
            if (owner.InteractionSlotAvailable) yield return CreateSharedDecoration(record, room);

        if(_saveData is not null&&Resources.TokayDimitri.Matches(group,room.Id))
        {
            // The complete placed prefix reserves $d2-$d5 before state-zero
            // $67:$03 may delete. $71:$07 must retain the later physical slot.
            foreach(var placement in Resources.TokayDimitri.Placements.Where(p=>p.Id!=0x48))
            {
                if(placement.Id==0x67)
                    yield return new CompanionPresetSpawnerRoomEntity(placement.Id, placement.SubId, ()=>SpawnDimitriPreset(activeGroup,room));
                else yield return new DimitriRescueScriptRoomEntity(_saveData);
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
            if(record.NativeSpawnerId>=0)continue;
            if (!owner.InteractionSlotAvailable) continue;
            if (!GroundTreasureDatabase.ShouldSpawn(
                    record, _saveData, _inventory))
                continue;
            yield return _common.TryCreate(new GroundTreasureRecordSpawn(record),room)!;
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
            // checkAndSpawnMaple allocates its interaction before parseObjectData.
            // The placed $dc:$05 must therefore consume its RNG after Maple.
            foreach (VolcanoPlacement record in Resources.Volcano.Placements(group, room.Id))
                yield return CreateVolcanoController(record, room);

            // checkAndSpawnMaple writes wcc85=$01. checkSkipPointer then
            // suppresses this room's entire enemy/item-drop pointer while
            // preserving every ordinary interaction emitted above.
            yield break;
        }

        var enemyStream = enemies.GetRoomObjects(group,room.Id);
        var program = NativeObjectStreamDatabase.For(AssetRoot);
        var orders = new Dictionary<int,int>();
        for (int index = 0; index < enemyStream.Count; index++)
            orders.Add(enemyStream[index].NativeAddress,index);
        var nativeBodies = deferredMermaidEnemies.ToDictionary(
            record => program.Entry($"group{record.Group:x1}Map{record.Room:x2}BeforeEventObjectData"));
        var reader = new NativeObjectStreamReader(program, _runtimeState,
            () => _saveData?.GetRoomFlags(group,room.Id) ?? 0);
        foreach (var instruction in reader.Read(program.Root(group,room.Id)))
        {
            if (instruction.Opcode is 1 or 2 && instruction.Id == 0xa1)
            {
                if (!owner.InteractionSlotAvailable) { reader.AllocationFailed(); continue; }
                yield return CreateStreamInteraction(instruction, room, placementContext);
                continue;
            }
            if (nativeBodies.TryGetValue(instruction.Address, out var body))
            {
                if (instruction.Id != body.Id || instruction.SubId != body.SubId)
                    throw new InvalidOperationException($"{instruction.Source}: native body differs from its content binding.");
                if (!owner.EnemySlotAvailable) { reader.AllocationFailed(); continue; }
                if (body.Kind == DungeonObjectKind.Vire)
                    yield return CreateVire(body,room,placementContext);
                else if (owner.TryChooseRandomEnemyPosition(room,0x20,reservations,placementContext,out var position))
                    yield return CreateOctogon(body,position,placementContext);
                continue;
            }
            // Existing interaction/main-data constructors above remain content
            // bindings; enemy-pointer execution now owns their placement rows.
            if (!orders.TryGetValue(instruction.Address, out int order)) continue;
            var source = enemyStream[order] with { SourceOverride = instruction.Source, ConditionMask = 0xff };
            foreach (var entity in owner.PlaceRoomObject(source,room,placementContext,reservations,reader.AllocationFailed))
                yield return entity;
        }
    }

    public IRoomEntity CreateStreamInteraction(NativeObjectInstruction source, OracleRoomData room, EnemyPlacementContext placement) =>
        source is { Opcode: 1 or 2, Id: 0xa1 } ?
            new MovingSideScrollPlatformRoomEntity(new(source.X, source.Y),
                Resources.DungeonInteractions.SidePlatform(source.SubId),
                Resources.DungeonVisuals.Visual("moving-side-platform")) :
            throw new NotSupportedException($"{source.Source}: Ages INTERAC ${source.Id:x2}:${source.SubId:x2} has no native construction binding.");

    public IEnumerable<IRoomEntity> CreateStaticRoomEntities(int group, OracleRoomData room)=>_common.CreateStaticRoomEntities(room);

    public IRoomEntity CreateStaticObject(StaticObjectRecord source, OracleRoomData room) =>
        _common.CreateStaticObject(source,room);

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
                    record, DungeonRewardDatabase.For(AssetRoot), _runtimeState, _saveData,
                    () => owner.RoomEnemyCount, treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal, scriptTextActive: owner.IsScriptTextActive);
            case DungeonObjectKind.MovingPlatform:
                return CreateMovingPlatform(record.Position, record.SubId,
                    rooms?.GetDungeonIndex(record.Group, record.Room) ?? -1);
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
                    Resources.DungeonBosses.Enemy(AgesEnemyId.GiantGhini), room, record.Position, _random,
                    owner.OnSoundRequested, owner.BossShuttersClosed,
                    owner.DisableLinkCollisionsAndMenu,
                    () => owner.OnRoomMusicRequested(record.Group, record.Room),owner.ToScreen,owner.InitializeBossRoom);
                return new GiantGhiniBossRoomEntity(
                    giantGhini);
            case DungeonObjectKind.PumpkinHead:
                ImportedEnemyDefinition pumpkin = Resources.DungeonBosses.Enemy(AgesEnemyId.PumpkinHead);
                return new PumpkinHeadBossRoomEntity(
                    new PumpkinHeadBoss(
                        pumpkin, room, record.Position, _random, owner.OnSoundRequested,
                        owner.BossShuttersClosed, owner.BeginScreenShake,
                        owner.DisableLinkCollisionsAndMenu,
                        () => owner.OnRoomMusicRequested(record.Group, record.Room),
                        Resources.DungeonBosses.Constant("pumpkin-body-palette"),
                        Resources.DungeonBosses.Constant("pumpkin-ghost-palette"), _runtimeState,owner.InitializeBossRoom),
                    pumpkin.DamageQuarters);
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

    private ToggleFloorRoomEntity CreateToggleFloor(OracleRoomData room) => new(room,Resources.DungeonInteractions,
        () => _runtimeState.ReadWramByte(AgesWramAddress.wActiveTilePos),owner.TryCreateFloorToggleTile,
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

    private DungeonTileScriptRoomEntity CreateDungeonTileScript(DungeonObjectRecord record, LightableTorchState? torches)
    {
        if (rooms is null || _saveData is null) throw new InvalidOperationException($"{record.Source}: tile script requires room/save owners.");
        var profile = rooms.World.GetDungeonIndex(record.Group, record.Room) is 9 or 10 or 11 && record.SubId == 3
            ? new DungeonTileScriptProfile(DungeonTileScriptKind.NorthEnemyStairs, 0,
                (byte)Resources.HerosCave.Constant("north-stairs"), OracleSaveData.RoomFlagItem, 0,
                "scripts/ages/dungeonScripts.s:herosCaveScript_spawnNorthStairsWhenEnemiesKilled")
            : Resources.AncientTomb.Script(record.SubId);
        return new(record, profile, _runtimeState, () => owner.ActiveTriggers, () => torches?.LitCount ?? 0,
            () => owner.RoomEnemyCount, () => owner.ActiveRoomHasFlag(_saveData, profile.Flag),
            () => _saveData.SetRoomFlag(owner.ActiveRoom.Group, owner.ActiveRoom.Id, profile.Flag),
            owner.IsScriptTextActive, tile => {
                byte position = (byte)(((int)record.Position.Y & 0xf0) | ((int)record.Position.X >> 4));
                if (rooms.TrySetTile(position, tile)) owner.OnRoomTileChanged();
            }, owner.OnNativeSoundRequested, owner.TryCreatePuzzlePuff, owner.OnWallRetractionTriggered);
    }

    private SharedDungeonScriptRoomEntity CreateSharedDungeonScript(DungeonObjectRecord record, LightableTorchState? torches)
    {
        if (rooms is null || _saveData is null) throw new InvalidOperationException($"{record.Source}: dungeon script requires room/save owners.");
        return new(record, Resources.SharedInteractions.Script(rooms.World.GetDungeonIndex(record.Group, record.Room), record.SubId),
            _runtimeState, owner.IsScriptTextActive, () => torches?.LitCount ?? 0, () => owner.RoomEnemyCount,
            flag => owner.ActiveRoomHasFlag(_saveData, (byte)flag),
            flag => _saveData.SetRoomFlag(owner.ActiveRoom.Group, owner.ActiveRoom.Id, (byte)flag),
            owner.OnNativeSoundRequested, handler => {
                switch (handler)
                {
                    case "SpawnSmallKey": owner.TryCreateGroundTreasure(CreateFallingSmallKeyRequest(record)); break;
                    case "SpawnRupees": owner.TryCreateGroundTreasure(new(record.Group, record.Room, record.Order,
                        record.Y, record.X, "TREASURE_OBJECT_RUPEES_0c", record.Source) {
                            SpawnMode = TreasureSpawnMode.Instant, GrabMode = TreasureGrabMode.TwoHands }); break;
                    case "CreatePuff": owner.TryCreatePuzzlePuff(record.Position); break;
                    case "SetStairs": case "SetChest":
                        byte position = (byte)((record.Y & 0xf0) | (record.X >> 4));
                        if (rooms.TrySetTile(position, handler == "SetStairs" ? (byte)0x45 : (byte)0xf1)) owner.OnRoomTileChanged();
                        break;
                    default: throw new InvalidOperationException($"{record.Source}: unsupported dungeon script handler '{handler}'.");
                }
            });
    }

    private IRoomEntity CreateSharedDecoration(DungeonObjectRecord record, OracleRoomData room)
    {
        if (_saveData is null) throw new InvalidOperationException($"{record.Source}: native decoration requires save state.");
        return record.Kind switch {
            DungeonObjectKind.LinkStatue => new MakuLinkStatueRoomEntity(NpcCharacter.CreateFromRecord(
                Resources.MakuSproutRoom.Record.CreateStatueNpcRecord() with {
                    Group = record.Group, Room = record.Room, X = record.X, Y = record.Y }, AgesGameContent.AssetRoot),
                Resources.MakuSproutRoom, room, _saveData, _animationTick, () => {
                    var colors = new Color[1, 4];
                    for (int shade = 0; shade < 4; shade++) colors[0, shade] =
                        (room.TilesetFlags & (int)TilesetFlags.Past) == 0
                            ? Resources.SharedInteractions.PresentStatuePalette[0, shade] : Resources.MakuSproutRoom.StatuePalette[shade];
                    owner.LoadNativeObjectPaletteSlots(6, colors);
                    return owner.NativeObjectPalettes![6];
                }),
            DungeonObjectKind.MoblinFlag => new DecorationRoomEntity(record, Resources.SharedInteractions.MoblinFlag,
                () => _saveData.HasGlobalFlag(AgesGlobalFlag.MoblinsKeepDestroyed)),
            DungeonObjectKind.PortalDecoration => new MakuPortalDecorationRoomEntity(record, room,
                () => owner.TryCreatePortalDecoration(record)),
            DungeonObjectKind.SavedMonkeys => CreateSavedMonkey(record),
            DungeonObjectKind.FaroreMemory => new FaroreMemoryRoomEntity(new FaroreMemoryDatabase(AssetRoot).CreateActor(record), _saveData),
            _ => throw new InvalidOperationException($"{record.Source}: unsupported native decoration ${record.Id:x2}:${record.SubId:x2}.")
        };
    }

    internal DecorationRoomEntity CreatePortalDecoration(DungeonObjectRecord producer) =>
        new(producer with { Id = AgesInteractionId.Decoration, SubId = 0, Kind = DungeonObjectKind.Decoration },
            Resources.FinalDungeonEntry.Decoration);

    public IEnumerable<IRoomEntity> CreateRoomLoadEntities(int group, OracleRoomData room, EnemyPlacementContext placement)
    {
        if (Resources.SharedInteractions.TryGetGashaReward(group, room.Id, out var record) && owner.InteractionSlotAvailable)
            yield return new MakuGashaRewardRoomEntity(record, _saveData!, _inventory!, request => owner.TryCreateGroundTreasure(request));
        foreach (var controller in _common.CreateMinecartShutterControllers(room, placement))
            yield return controller;
    }
    internal MonkeyRoomEntity CreateSavedMonkey(DungeonObjectRecord record) => new(record, Resources.Monkeys, owner, _saveData!);

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
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0x6f);
                _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0x6f);
                _saveData.SetRoomFlag(owner.ActiveRoom.Group,0x25,0x40);
                owner.OnWallRetractionTriggered();
            });
    }

    internal LightableTorchScannerRoomEntity CreateLightableTorchScanner(LightableTorchState state) => new(
        new(owner.ActiveRoom.Group,owner.ActiveRoom.Id,0,InteractionId.CreateObjectAtEachTileIndex,0x08,0x06,0x10,TriggerPredicate.None,true),
        owner.ActiveRoom,state,LightableTorchDatabase.For(AssetRoot),owner.TryCreateLightableTorch);

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
                    record, DungeonRewardDatabase.For(AssetRoot), _runtimeState, _saveData, () => owner.RoomEnemyCount,
                    CreateFallingSmallKeyRequest(record),
                    owner.EnableLinkCollisionsAndMenu, scriptTextActive: owner.IsScriptTextActive);
            case DungeonObjectKind.MinibossReward:
                return new DungeonRewardRoomEntity(
                    record, DungeonRewardDatabase.For(AssetRoot), _runtimeState, _saveData,
                    () => owner.RoomEnemyCount, treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal, scriptTextActive: owner.IsScriptTextActive);
            case DungeonObjectKind.FloorColorChanger:
                return CreateFloorColorChanger(record);
            case DungeonObjectKind.CircularSidePlatform:
                return new CircularSideScrollPlatformRoomEntity(
                    record,
                    Resources.DungeonVisuals.Visual("circular-side-platform"),
                    Resources.DungeonInteractions.CircularSidePlatform(record.SubId));
            case DungeonObjectKind.HeadThwomp:
                ImportedEnemyDefinition headThwomp =
                    Resources.DungeonBosses.Enemy(AgesEnemyId.HeadThwomp);
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
                    _animationTick,owner.InitializeBossRoom);
                return new HeadThwompBossRoomEntity(
                    headThwompBoss);
            case DungeonObjectKind.Swoop:
                ImportedEnemyDefinition swoopRecord =
                    Resources.DungeonBosses.Enemy(AgesEnemyId.Swoop);
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
                    Resources.WingDungeon.SwoopMessage,owner.InitializeBossRoom);
                return new SwoopBossRoomEntity(
                    swoop);
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
                    DungeonRewardDatabase.For(AssetRoot),
                    _runtimeState,
                    _saveData,
                    () => owner.RoomEnemyCount,
                    treasure: null,
                    owner.EnableLinkCollisionsAndMenu, owner.TryCreateMinibossPortal, scriptTextActive: owner.IsScriptTextActive);
            case DungeonObjectKind.Subterror:
                ImportedEnemyDefinition definition =
                    Resources.DungeonBosses.Enemy(AgesEnemyId.Subterror);
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
                    Resources.MoonlitGrotto.SubterrorMessage,owner.InitializeBossRoom);
                return new SubterrorBossRoomEntity(
                    subterror);
            case DungeonObjectKind.ShadowHag:
                ImportedEnemyDefinition shadowHagDefinition =
                    Resources.DungeonBosses.Enemy(AgesEnemyId.ShadowHag);
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
                    Resources.MoonlitGrotto.ShadowHagMessage,owner.InitializeBossRoom);
                return new ShadowHagBossRoomEntity(
                    shadowHag);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record),
                    record,
                    "Unsupported Moonlit Grotto native object.");
        }
    }

    private GroundTreasureGrantRequest CreateFallingSmallKeyRequest(
        DungeonObjectRecord record) =>
        CommonRoomEntityFactory.FallingSmallKey(record);

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
        CommonRoomEntityFactory.FallingSmallKey(new(group,room,order,DungeonObjectKind.EnemySmallKey,
            InteractionId.DungeonStuff,1,y,x,DungeonObjectCondition.ItemClear,source));

    private IRoomEntity CreateArmosWarrior(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        var actor = new ArmosWarriorActor();
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.ArmosWarrior), new ArmosWarriorEnvironment(room, Resources.ArmosWarrior, Resources.DungeonBosses,
            _random, owner.BossShuttersClosed, () => owner.PartSlotAvailable, owner.CanAllocateEnemies,
            () => -(int)owner.ToScreen(Vector2.Zero).Y, owner.OnSoundRequested, owner.BeginScreenShake,
            owner.DisableLinkCollisionsAndMenu, owner.EnableLinkCollisionsAndMenu,
            () => owner.OnRoomMusicRequested(record.Group, record.Room), owner.OnRoomEntityDialogueRequested, owner.InitializeBossRoom), record.Position);
        return new ArmosWarriorRoomEntity(actor);
    }

    private IRoomEntity CreateBlueStalfos(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        int killableIndex = owner.NextKillableEnemyIndex(0);
        BlueStalfosEnvironment? world = null;
        world = new(room, _random, owner.InitializeBossRoom,
            () => { owner.SetNativeActiveMusic(SoundId.MusMiniboss); owner.OnNativeSoundRequested(SoundId.MusMiniboss); },
            () => owner.CanAllocateVireBats(3), (parent, subid, counted) =>
                owner.TryAllocateEnemy(_ => {
                    var child = new BlueStalfosCharacter { Name = $"BlueStalfos_{subid:x2}" };
                    child.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.BlueStalfos, subid), parent.Position, world!,
                        subid == 1 ? -1 : parent.Slot, parent.ZFixed);
                    return new BlueStalfosRoomEntity(child, world!, counted, killableIndex);
                })?.Node as BlueStalfosCharacter,
            slot => owner.ResolveEnemySlot(slot)?.Node as BlueStalfosCharacter,
            parent => owner.TryCreatePart(new BossShadowSpawn(() => parent.Position, () => parent.ZFixed >> 8,
                () => !parent.IsDead, 1, 11)),
            (position, z) => owner.TryCreateVirePuff(position, z, 2), owner.InteractionAnimationParameter,
            parent => owner.TryCreatePart(new BlueStalfosProjectileSpawn(parent.Position, parent.ZFixed, parent.Slot, parent.Angle)),
            owner.DisableLinkCollisionsAndMenu,
            parent => owner.TryCreatePart(new BossDeathExplosionSpawn(parent.Position, AgesEnemyId.BlueStalfos, parent.ZFixed >> 8)),
            () => owner.OnRoomMusicRequested(record.Group, record.Room), owner.OnNativeSoundRequested);
        var actor = new BlueStalfosCharacter { Name = "BlueStalfos_Spawner" };
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.BlueStalfos, 0), record.Position, world);
        return new BlueStalfosRoomEntity(actor, world, true, killableIndex);
    }

    private IRoomEntity CreateRamrock(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        int killableIndex = owner.NextKillableEnemyIndex(0);
        RamrockEnvironment? world = null;
        world = new(room, _runtimeState, _random, () => {
            owner.SetNativeObjectPalette(RamrockProfile.Shared.Palette(0x83));
            owner.InitializeBossRoom();
        }, () => {
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
            owner.SetNativeActiveMusic(SoundId.MusBoss); owner.OnNativeSoundRequested(SoundId.MusBoss);
        }, () => owner.BossEntrySignal != 0,
        (parent, subid) => owner.TryAllocateEnemy(_ => {
            var hand = new RamrockCharacter { Name = $"RamrockHand_{subid:x2}" };
            hand.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.RamrockArms, subid), Vector2.Zero, world!, parent.Slot);
            return new RamrockRoomEntity(hand, world!, 0);
        })?.Node as RamrockCharacter,
        slot => owner.ResolveEnemySlot(slot)?.Node as RamrockCharacter,
        id => owner.SetNativeObjectPalette(RamrockProfile.Shared.Palette(id)),
        parent => owner.TryCreatePart(new BossShadowSpawn(() => parent.Position, () => parent.ZFixed >> 8, () => !parent.IsDead, 0, 12)),
        (position, z) => owner.TryCreateVirePuff(position, z, 0), owner.TryCreatePart,
        () => owner.EntityAdapters<BombRoomEntity>().Where(b => !b.Finished).OrderBy(owner.DynamicItemSlotOf).Select(b => b.Bomb).ToArray(),
        owner.ToScreen,
        owner.DisableLinkCollisionsAndMenu,
        parent => owner.TryCreatePart(new BossDeathExplosionSpawn(parent.Position, AgesEnemyId.Ramrock, parent.ZFixed >> 8)),
        owner.OnNativeSoundRequested);
        var actor = new RamrockCharacter { Name = "Ramrock_07" };
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.Ramrock), record.Position, world);
        return new RamrockRoomEntity(actor, world, killableIndex);
    }

    private RamrockPart? AllocateRamrockPart(RamrockPartSpawn spawn)
    {
        if (!owner.TryCreatePart(spawn)) return null;
        return owner.Entities<RamrockPart>().Last(part => part.Id == spawn.Id && part.State == 0);
    }

    private IRoomEntity CreateRamrockPart(RamrockPartSpawn spawn)
    {
        RamrockEnvironment world = spawn.ParentIsPart
            ? (owner.ResolvePartSlot(spawn.ParentSlot)?.Node as RamrockPart)?.World ?? throw new NotSupportedException("Ramrock part references a deleted/reused part parent.")
            : (owner.ResolveEnemySlot(spawn.ParentSlot)?.Node as RamrockCharacter)?.World ?? throw new NotSupportedException("Ramrock part references a deleted/reused enemy parent.");
        return new RamrockPart(spawn, world, slot => {
            var entity = owner.ResolvePartSlot(slot);
            if (entity is not null && entity.Node is not RamrockPart)
                throw new NotSupportedException($"Ramrock references reused PART slot${slot:x2} ({entity.GetType().Name}).");
            return entity?.Node as RamrockPart;
        }, AllocateRamrockPart);
    }

    private IRoomEntity CreateBlueStalfosProjectile(BlueStalfosProjectileSpawn spawn, OracleRoomData room) =>
        new BlueStalfosProjectileRoomEntity(new(spawn, room, _runtimeState, slot => {
            var entity = owner.ResolveEnemySlot(slot);
            if (entity is not null && entity.Node is not BlueStalfosCharacter)
                throw new NotSupportedException($"PART$3d references reused ENEMY slot${slot:x2} ({entity.GetType().Name}); its native field writes are not represented.");
            return entity?.Node as BlueStalfosCharacter;
        }, () => owner.CanAllocateParts(6), (parent, angle) => {
            if (!owner.TryCreatePart(new BlueStalfosProjectileSpawn(parent.Position, parent.ZFixed, parent.ParentSlot, angle, 1)))
                throw new InvalidOperationException("PART$3d checked six-child allocation failed.");
        }, owner.OnNativeSoundRequested));

    private IRoomEntity CreateSmasher(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        SmasherRoomEnvironment? environment = null;
        environment = new SmasherRoomEnvironment(
            _ => (owner.TryAllocateEnemy(slot =>
            {
                var child = new SmasherCharacter { Name = "Smasher_74_Parent" };
                child.InitializePending(enemies.ImportedEnemy(AgesEnemyId.Smasher,1),room,Vector2.Zero,_random,slot);
                return new SmasherRoomEntity(child,environment!,false);
            }) as SmasherRoomEntity)?.Character,
            owner.InitializeBossRoom,
            () => { owner.EnableLinkCollisionsAndMenu(); owner.OnSoundRequested(SoundId.MusMiniboss); },
            () => owner.InteractionSlotAvailable,() => owner.PartSlotAvailable,owner.DisableLinkCollisionsAndMenu,
            () => owner.OnRoomMusicRequested(record.Group,record.Room),owner.OnSoundRequested,0,true,() => owner.FrameCounter,owner.TryCreatePuzzlePuff);
        var ball = new SmasherCharacter { Name = "Smasher_74_Ball" };
        ball.InitializePending(enemies.ImportedEnemy(AgesEnemyId.Smasher,0),room,record.Position,_random,0);
        return new SmasherRoomEntity(ball,environment,true);
    }

    private IRoomEntity CreateVire(DungeonObjectRecord record,OracleRoomData room,EnemyPlacementContext placementContext)
    {
        VireRoomEnvironment? world = null;
        world = new(
            _ => owner.InitializeBossRoom(),
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
            slot => owner.ResolveEnemySlot(slot)?.Node as VireCharacter,owner.TryCreatePart);
        var actor = new VireCharacter { Name = "Vire_75_Main" };
        actor.Initialize(enemies.ImportedEnemy(EnemyId.Vire,0),room,record.Position,_random,_runtimeState,world);
        return new VireRoomEntity(actor,world);
    }

    private IRoomEntity CreateAnglerFish(DungeonObjectRecord record,OracleRoomData room,EnemyPlacementContext placementContext)
    {
        AnglerFishEnvironment? world = null;
        void SetLinkMask(int value)
        {
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,(byte)value);
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,(byte)value);
        }
        world = new(room,_random,_ => owner.InitializeBossRoom(),parent => (owner.TryAllocateEnemy(_ => {
            var child = new AnglerFishCharacter { Name = "AnglerFish_76_Antenna" };
            child.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.AnglerFish,1),Vector2.Zero,world!,false,parent);
            return new AnglerFishRoomEntity(child,world!);
        })?.Node as AnglerFishCharacter),parent => owner.TryAllocateEnemy(_ => {
            var bubble = new AnglerFishCharacter { Name = "AnglerFishBubble_26" };
            bubble.Initialize(Resources.DungeonBosses.Enemy(EnemyId.AnglerFishBubble),Vector2.Zero,world!,false,parent);
            return new AnglerFishRoomEntity(bubble,world!);
        }) is not null,(packed,tile) => {
            if(rooms is null)throw new InvalidOperationException($"{record.Source}: Angler Fish tile writes require the room owner.");
            if(rooms.TrySetTile((byte)packed,(byte)tile))owner.OnRoomTileChanged();
        },() => SetLinkMask(1),() => {
            SetLinkMask(0);owner.SetNativeActiveMusic(SoundId.MusMiniboss);owner.OnNativeSoundRequested(SoundId.MusMiniboss);
        },owner.DisableLinkCollisionsAndMenu,
        point => owner.TryCreatePart(new BossDeathExplosionSpawn(point,AgesEnemyId.AnglerFish)),
        () => owner.OnRoomMusicRequested(record.Group,record.Room),owner.OnNativeSoundRequested,
        point => owner.TryCreatePart(new ItemDropSpawn(ItemDropDatabase.ScentSeeds,point,InvincibilityCounter:0xf0)),
        point => owner.TryCreateCandleExplosion(point)>=0);
        var actor = new AnglerFishCharacter { Name = "AnglerFish_76_Main" };
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.AnglerFish),record.Position,world,true);
        return new AnglerFishRoomEntity(actor,world);
    }

    private IRoomEntity CreatePlasmarine(DungeonObjectRecord record,EnemyPlacementContext placementContext)
    {
        void SetLinkMask(int value)
        {
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,(byte)value);
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,(byte)value);
        }
        var world = new PlasmarineEnvironment(_random,() => {
            SetLinkMask(1);owner.InitializeBossRoom();
        },() => {
            SetLinkMask(0);owner.SetNativeActiveMusic(SoundId.MusBoss);owner.OnNativeSoundRequested(SoundId.MusBoss);
        },parent => owner.TryCreatePart(new PlasmarineProjectileSpawn(parent.Position.Floor()+new Vector2(0,-20),parent.CurrentOamFlags-1,parent.Slot)),
        owner.DisableLinkCollisionsAndMenu,point => owner.TryCreatePart(new BossDeathExplosionSpawn(point,AgesEnemyId.Plasmarine)),owner.OnNativeSoundRequested);
        var actor = new PlasmarineCharacter {Name="Plasmarine_7e"};
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.Plasmarine),record.Position,world);
        return new PlasmarineRoomEntity(actor,world);
    }

    private IRoomEntity CreateOctogon(DungeonObjectRecord record,Vector2 position,EnemyPlacementContext placementContext)
    {
        OctogonRoomEnvironment? world = null;
        bool SpawnInteraction(Vector2 point,int id,int parameter,int related)
        {
            if (!owner.InteractionSlotAvailable) return false;
            owner.AddEntity(new OctogonInteractionRoomEntity(point,id,parameter,parameter == 1 ? related : -1,
                _random,_runtimeState,slot => owner.ResolveEnemySlot(slot)?.Node as OctogonCharacter,SpawnInteraction,
                parameter == 0 ? related : 0));
            return true;
        }
        world = new(_ => owner.InitializeBossRoom(),() => owner.BossEntrySignal,owner.OnNativeSoundRequested,owner.NativeActiveMusicSource,owner.SetNativeActiveMusic,owner.DisableLinkCollisionsAndMenu,
        () => owner.OnRoomMusicRequested(record.Group,record.Room),
        () => { _saveData!.SetRoomFlag(5,0x2d,0x80); _saveData.SetRoomFlag(5,0x36,0x80); },
        parent => {
            int childSlot = -1;
            owner.TryAllocateEnemy(slot => {
                childSlot = slot;
                var shell = new OctogonCharacter { Name = "Octogon_7d_Shell" };
                shell.Initialize(enemies.ImportedEnemy(AgesEnemyId.Octogon,2),Vector2.Zero,_random,_runtimeState,world!,parent.Slot);
                return new OctogonRoomEntity(shell,world!);
            });
            return childSlot;
        },slot => owner.ResolveEnemySlot(slot)?.Node as OctogonCharacter,owner.TryCreatePart,SpawnInteraction);
        var actor = new OctogonCharacter { Name = $"Octogon_7d_{record.SubId:x2}" };
        actor.Initialize(enemies.ImportedEnemy(AgesEnemyId.Octogon,record.SubId),position,_random,_runtimeState,world);
        return new OctogonRoomEntity(actor,world);
    }

    private static IRoomEntity CreateArmosChild(ArmosWarriorChildSpawn spawn)
    {
        var actor = spawn.Spawner.CreateChild(spawn.SubId);
        return new ArmosWarriorRoomEntity(actor);
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
    private IRoomEntity CreateForestObject(ForestNativeObjectRecord record, OracleRoomData room) => record.Id == 0x71
        ? new CompanionForestControllerRoomEntity(record, Resources.CompanionForest, Resources.FairiesWoods,
            _saveData!, _runtimeState, () => CompanionForest)
        : new CompanionPresetSpawnerRoomEntity(record.Id, record.SubId, () =>
        {
            if (!owner.SpecialObjectSlotAvailable || _saveData!.HasGlobalFlag(AgesGlobalFlag.GotFlute) ||
                !_saveData.HasGlobalFlag(record.SubId == 4 ? AgesGlobalFlag.CompanionLostInForest : AgesGlobalFlag.SavedCompanionFromForest)) return;
            int id = _saveData!.ReadField("wAnimalCompanion");
            Vector2 position = Resources.CompanionForest.Preset(record.SubId);
            CompanionScripts.SpawnPreset(id, position);
        });

    private IRoomEntity CreateEyesoar(DungeonObjectRecord record, OracleRoomData room, EnemyPlacementContext placementContext)
    {
        var actor = new EyesoarActor();
        actor.Initialize(Resources.DungeonBosses.Enemy(AgesEnemyId.Eyesoar), new EyesoarEnvironment(room, Resources.Eyesoar, Resources.DungeonBosses,
            Resources.DungeonVisuals.Visual("eyesoar-spawn"), _random, owner.BossShuttersClosed, () => owner.PartSlotAvailable,
            owner.CanAllocateEnemies, () => owner.InteractionSlotAvailable, owner.OnSoundRequested, owner.DisableLinkCollisionsAndMenu,
            owner.EnableLinkCollisionsAndMenu, owner.InitializeBossRoom), record.Position);
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
            record, DungeonRewardDatabase.For(AssetRoot), _runtimeState, _saveData, () => owner.RoomEnemyCount, request,
            owner.EnableLinkCollisionsAndMenu, trySpawnTreasure: owner.TryCreateGroundTreasure,
            scriptTextActive: owner.IsScriptTextActive);
    }

    private DungeonEssence CreateLaterDungeonEssence(DungeonObjectRecord record, OracleRoomData room)
    {
        var definition = new[] { Resources.SkullDungeon.Essence, Resources.CrownDungeon.Essence, Resources.MermaidDungeon.Essence, Resources.JabuDungeon.Essence, Resources.AncientTomb.Essence }.Single(definition =>
            definition.ExitWarp.SourceGroup == record.Group && definition.ExitWarp.SourceRoom == record.Room);
        string visual = definition.Index switch
        {
            3 => "burning-flame",
            4 => "sacred-soil",
            5 => "bereft-peak",
            6 => "rolling-sea",
            7 => "falling-star",
            _ => throw new InvalidOperationException($"{record.Source}: missing Essence visual for index${definition.Index:x2}.")
        };
        return new DungeonEssence(record, Resources.DungeonVisuals.Visual(visual),
            Resources.DungeonVisuals.Visual("essence-pedestal"), Resources.DungeonVisuals.Visual("essence-glow"),
            Resources.DungeonVisuals.Visual("energy-bead"), room,
            _saveData?.HasRoomFlag(record.Group,record.Room,OracleSaveData.RoomFlagItem)==true,
            _animationTick,owner.OnDungeonEssenceTriggered,definition);
    }

    private EnemyAttachedKeyRoomEntity CreateEnemyAttachedKey(DungeonObjectRecord record) => new(record,
        Resources.DungeonVisuals.Visual("enemy-attached-key"),
        () => owner.ActiveRoomHasFlag(_saveData!, OracleSaveData.RoomFlagItem),
        () => {
            var enemy = owner.ResolveEnemySlot(0);
            if (enemy is null) return null;
            if (enemy is not IBombchuTargetRoomEntity target || enemy is not IObjectCollisionHeightRoomEntity height)
                throw new NotSupportedException($"{record.Source}: INTERAC$77 reads reused ENEMY slot$00 ({enemy.GetType().Name}) without a native ID/position/height owner.");
            return new NativeEnemyAttachment(target.BombchuTarget.Id, enemy.Node.Position, height.CollisionZ);
        }, position => owner.TryCreateGroundTreasure(new(record.Group, record.Room, record.Order,
            (byte)(int)position.Y, (byte)(int)position.X, "TREASURE_OBJECT_SMALL_KEY_00", record.Source) {
            SpawnMode = TreasureSpawnMode.Puff, GrabMode = TreasureGrabMode.NoChange,
            SpawnDelayFrames = Resources.DungeonInteractions.Constant("treasure-puff-wait")
        }));

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
            DungeonRewardDatabase.For(AssetRoot),
            _runtimeState,
            _saveData,
            () => owner.RoomEnemyCount,
            request,
            owner.EnableLinkCollisionsAndMenu, scriptTextActive: owner.IsScriptTextActive);
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
        if (record.Id is InteractionId.DoorController or AgesInteractionId.TriggerTranslator or InteractionId.CreateObjectAtEachTileIndex && !owner.InteractionSlotAvailable)
            return null;
        if (record.Id == AgesInteractionId.Miscellaneous1 && record.SubId == 0x0f && !owner.InteractionSlotAvailable)
            return null;
        if (record.Id == AgesInteractionId.Miscellaneous1 && record.SubId == 0x0f)
            return new NuunBridgeRoomEntity(record, room,
                _saveData ?? throw new InvalidOperationException("INTERAC $6b:$0f requires live save state."),
                _runtimeState, _animationTick, owner.OnRoomTileChanged, owner.OnSoundRequested,
                (position,tile) => {
                    if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                        throw new InvalidOperationException("INTERAC$6b:$0f setTile requires its active room session.");
                    return rooms.TrySetTile(position,tile);
                });
        if (record.Id == AgesInteractionId.Miscellaneous2 && record.SubId == 0x12)
        {
            if (!owner.InteractionSlotAvailable) return null;
            return new OrbBridgeControllerRoomEntity(record,
                _saveData ?? throw new InvalidOperationException(
                    $"Room {group:x1}:{room.Id:x2} $dc:$12 requires live save state."),
                _runtimeState, owner.OnSoundRequested, Resources.DungeonMechanics.SolveSound, rooms, owner.TryCreateBridgeSpawner);
        }
        if (record.Id == AgesInteractionId.Miscellaneous2 && record.SubId == 0x0e)
        {
            if (!owner.InteractionSlotAvailable) return null;
            return new AncientTombEntranceRoomEntity(record, room,
                _saveData ?? throw new InvalidOperationException($"Room {group:x1}:{room.Id:x2} $dc:$0e requires live save state."),
                _runtimeState, Resources.DungeonMechanics,
                (position, tile) =>
                {
                    if (rooms is null || !ReferenceEquals(rooms.CurrentRoom, room))
                        throw new InvalidOperationException("miscellaneous2.s:$dc:$0e setTile requires its active room session.");
                    rooms.TrySetTile(position, tile);
                }, owner.TryCreatePuzzlePuff, owner.OnSoundRequested) { Visible = false };
        }
        if (record.Id == AgesInteractionId.Miscellaneous2 && record.SubId is 0x15 or 0x16)
        {
            if (!owner.InteractionSlotAvailable) return null;
            var save = _saveData ?? throw new InvalidOperationException($"Room {group:x1}:{room.Id:x2} $dc:${record.SubId:x2} requires live save state.");
            return new ChestContentsRoomEntity(_runtimeState,
                () => save.HasRoomFlag(rooms?.ActiveGroup ?? group, rooms?.CurrentRoom.Id ?? room.Id, OracleSaveData.RoomFlagItem),
                () => _treasures.GetObject(Resources.ChestContents.Object(record.SubId, save.IsLinkedGame)))
                { Visible = false, Name = $"ChestContents_{record.SubId:x2}" };
        }
        if (record.Id == AgesInteractionId.Miscellaneous2 && record.SubId is 0x0c or 0x0d)
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
        if (record.Id == AgesInteractionId.ExtendableBridge)
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
        if (record.Id == AgesInteractionId.SmogBoss)
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
        if (record.Id == AgesInteractionId.TriggerTranslator && record.SubId == 0x02)
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
                LightableTorchDatabase.For(AssetRoot),owner.TryCreateLightableTorch);
        }
        if (record.Id == InteractionId.CreateObjectAtEachTileIndex && record.SubId == 0x04)
            return new RespawnableBushScannerRoomEntity(record, room);
        if (record.Id == PartId.RespawnableBush)
        {
            if (!owner.PartSlotAvailable) return null;
            return Create(new RespawnableBushSpawn(record.PackedPosition, record.SubId), room);
        }
        if (record.Id == AgesInteractionId.DungeonEvents && record.SubId == 0x09)
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
        if (record.Id == AgesInteractionId.DungeonEvents && record.SubId is 0x0a or 0x0c)
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
                    : null,
                owner.TryCreateInteraction);
        }
        if (record.Id == AgesInteractionId.DungeonEvents && record.SubId == 0x0d)
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
        if (record.Id == AgesInteractionId.DungeonEvents && record.SubId == 0x0e)
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
        if (record.Id == AgesInteractionId.TriggerTranslator)
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
        if (record.Id == AgesInteractionId.DungeonEvents && record.SubId == 0x17)
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
            if (!record.CountSourceComplete ||
                !DungeonEnemyCountIsComplete(group, room))
            {
                return null;
            }
            return _common.CreateEnemyClearChest(record);
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
                () => _runtimeState.SetWramByte(AgesWramAddress.wTmpcfc0,
                    (byte)(_runtimeState.ReadWramByte(AgesWramAddress.wTmpcfc0)^Resources.DungeonMechanics.DoorEntryScratchMask))),
            _ => throw new InvalidOperationException(
                $"Unsupported dungeon interaction ${record.Id:x2}:" +
                $"${record.SubId:x2} in room {group:x1}:{room.Id:x2}.")
        };
    }

    private static InvalidOperationException MissingLightableTorchState(
        DungeonMechanicDatabaseRecord record) => new(
        $"Room {record.Group:x1}:{record.Room:x2} ${record.Id:x2}:" +
        $"${record.SubId:x2} is missing its room-local torch state.");

    private IRoomEntity CreateHerosCavePortal(DungeonObjectRecord record)
    {
        var destination = Resources.HerosCave.Portal(record.X & 15);
        var portal = new MinibossPortal();
        portal.Initialize(Resources.DungeonEntrances);
        portal.Position = PointForPackedPosition(record.Y);
        var placement = new PlacementRecord(record.Group, record.Room, record.Order,
            DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal, record.Id, record.SubId,
            record.Y, record.X, 0x0b, record.Source);
        return new MinibossPortalRoomEntity(portal, placement, Resources.DungeonEntrances, _saveData,
            owner.OnRoomWarpRequested, owner.OnSoundRequested, owner.IsOutgoingEntity, _runtimeState,
            new Warp(record.Group, record.Room, record.Y, 0, 3, 4, destination.Room,
                destination.Position, 0, 0, DirectFadeOut: true));
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
                    Resources.DungeonEntrances.Entry(record.Dungeon),
                    Resources.DungeonEntrances.EntryMinimumY, Resources.DungeonEntrances.EntryRadius,
                    _runtimeState,
                    placementContext.Kind == EnemyPlacementEntryKind.ScreenWarp,
                    dungeon => StaticObjectState.LoadDungeon(_runtimeState, dungeon),
                    owner.OnDungeonEntranceTriggered);

            case DungeonEntranceInteractionDatabaseObjectKind.EyeSpawner:
                if (record.SubId == 3)
                    return new StatueEyeballSpawnerRoomEntity(room, Resources.DungeonEntrances,
                        owner.TryCreateEyePuzzleEye, () => EyePuzzleDatabase.For(AssetRoot).PrepareEyes(owner));
                return new StatueEyeballSpawnerRoomEntity(room, Resources.DungeonEntrances, owner.TryCreateStatueEyeball);

            case DungeonEntranceInteractionDatabaseObjectKind.MinibossPortal:
                return _common.CreateMinibossPortal(record);

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
        return CreateSmogEnemy(new(record.Position,record.SubId),room);
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

    private IRoomEntity CreateSmogEnemy(SmogEnemySpawn spawn, OracleRoomData room)
    {
        if (!owner.CanAllocateEnemies(1)) throw new NotSupportedException("smog.s unchecked ENEMY$7c allocation into a full native pool is not represented.");
        var actor = new SmogCharacter();
        var record = enemies.ImportedEnemy(AgesEnemyId.Smog,0);
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
            _ => owner.InitializeBossRoomWithResult(),() => owner.FrameCounter,
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

    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn is CompanionInitializationSpawn initialization
        ?CompanionScripts.CreateBody(initialization.Id,initialization.Position):spawn is DimitriCompanionSpawn dimitri
        ?CreateDimitri(dimitri,room):spawn is MooshCompanionSpawn moosh
        ?CreateMoosh(moosh,room):_common.TryCreate(spawn,room) ?? spawn switch
    {
        RaftSpawn raft => new RaftRoomEntity(raft, room, Resources.Raft.Behavior,
            _saveData ?? throw new InvalidOperationException("SPECIALOBJECT $13 requires the live save owner."), _runtimeState, owner),
        RaftInteractionSpawn raft => new RaftInteractionRoomEntity(raft, room, Resources.Raft.Behavior,
            _saveData ?? throw new InvalidOperationException("INTERAC $e6 requires the live save owner."), _runtimeState, owner),
        PalaceApproachRalphSpawn => new PalaceApproachRoomEntity(owner, rooms!, Resources.PalaceApproach, ralph: true),
        SmogEnemySpawn smog => CreateSmogEnemy(smog,room),
        DungeonSwitchSpawn switchPart => CreateDungeonSwitch(switchPart.Record,room),
        TimedSparkleSpawn sparkle => new TimedSparkleRoomEntity(sparkle),
        FaroreRewardSpawn faroreReward => new FaroreRewardRoomEntity(faroreReward.Parent),
        FaroreMakeChestSpawn faroreChest => new FaroreMakeChestRoomEntity(faroreChest.Parent, owner),
        FaroreSparkleSpawn faroreSparkle => new FaroreSparkleRoomEntity(faroreSparkle, owner),
        BridgeSpawnerSpawn bridge => new BridgeSpawnerRoomEntity(bridge, room,
            Resources.DungeonMechanics, owner.OnRoomTileChanged, owner.OnSoundRequested,(position,tile) => {
                if (rooms is null || !ReferenceEquals(rooms.CurrentRoom,room))
                    throw new InvalidOperationException("PART$0c setTile requires its active room session.");
                return rooms.TrySetTile(position,tile);
            }),
        VolcanoHandlerSpawn volcano => CreateVolcanoController(volcano.Placement, room),
        VolcanoRockSpawn rock => new VolcanoRockRoomEntity(new VolcanoRock(
            rock.Position, Resources.Volcano, room, _random, owner.OnSoundRequested, owner.ToScreen)),
        MapleTouchingBookSpawn book => CreateMapleTouchingBook(book, room),
        MaskedMoblinSpawn moblin => CreateMaskedMoblin(moblin, room),
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
        EnemyClearChestSpawn chest => _common.CreateEnemyClearChest(new(chest.Group,chest.Room,0,
            InteractionId.DungeonStuff,2,chest.PackedPosition,0,TriggerPredicate.None,true)),
        EnemySmallKeyRewardSpawn key => CreateEnemySmallKeyReward(key.Request),
        StalfosBoneSpawn bone => CreateStalfosBone(bone, room),
        KeeseFireSpawn fire => CreateKeeseFire(fire),
        EnemySwordSpawn sword => new EnemySwordRoomEntity(sword.Parent, sword.SoundRequested, sword.ParentCollisionAllowed),
        SpikedBallSpawn ball => new SpikedBallRoomEntity(ball.Part, ball.Sound),
        FountainFairyHeartSpawn heart => heart.Owner.CreateHeart(_runtimeState),
        LibraryPodiumSpawn podium => podium.Owner.CreatePodium(podium.SubId),
        FountainFairyPuffSpawn puff => new FixedEffectRoomEntityAdapter<StationaryDebrisEffect>(puff.Effect),
        BariChildSpawn bari => EnemyFactory.CreateBariChild(bari, room),
        CuccoAttackerSpawn attacker => CreateCuccoAttacker(attacker),
        BlueEnergyBeadSpawn bead => new BlueEnergyBeadRoomEntity(bead.Index, bead.Center, bead.Duration,
            Resources.DungeonVisuals.Visual("energy-bead"), _random, _runtimeState,bead.Outgoing),
        PlasmarineProjectileSpawn projectile => new PlasmarineProjectileRoomEntity(new PlasmarineProjectile(projectile,room,_runtimeState,
            slot => owner.ResolveEnemySlot(slot)?.Node as PlasmarineCharacter)),
        ArmosWarriorChildSpawn child => CreateArmosChild(child),
        DefeatedMoblinActorSpawn actor => new DefeatedMoblinActorRoomEntity(NpcCharacter.CreateFromRecord(actor.Record, AgesGameContent.AssetRoot)),
        KingMoblinMinionSpawn child => CreateKingMoblinMinion(child),
        KingMoblinBombSpawn bomb => CreateKingMoblinBomb(bomb),
        KingMoblinExplosionSpawn explosion => CreateInteractionExplosion(new InteractionExplosionSpawn(explosion.Position,0,Resources.Patch.ExplosionVisual,Var03:0)),
        EyesoarChildSpawn child => new EyesoarRoomEntity(child.Spawner.CreateChild(child.Index)),
        MoldormChildSpawn child => child.Spawner.CreateChild(child.SubId),
        EyesoarSpawnEffectSpawn effect => new FixedEffectRoomEntityAdapter<EyesoarSpawnEffect>(effect.Effect),
        BlueStalfosProjectileSpawn shot => CreateBlueStalfosProjectile(shot, room),
        RamrockPartSpawn part => CreateRamrockPart(part),
        SubterrorDirtSpawn dirt => CreateSubterrorDirt(dirt),
        ShadowHagShadowSpawn shadow => CreateShadowHagShadow(shadow),
        HeadThwompBombDropSpawn => throw new InvalidOperationException("headThwomp.s: bomb pair requires the manager's checked two-PART allocation."),
        HeadThwompBombDropperSpawn dropper => new HeadThwompBombDropRoomEntity(room, _random,
            () => owner.ResolvePartSlot(dropper.DropSlot)?.Node as ItemDropEffect, dropper.Position, _runtimeState),
        InteractionSparkleSpawn sparkle => CreateInteractionSparkle(sparkle.Position, sparkle.SourceAngle, sparkle.Visual,sparkle.Subid),
        OwlStatueSparkleSpawn sparkle => CreateInteractionSparkle(sparkle.Position, 0, sparkle.Visual),
        GreenPoofSpawn poof => new GreenPoofRoomEntity(Resources.DungeonVisuals.Visual("green-poof"), poof.Position, poof.Z),
        TingleKoolooSparkleSpawn sparkle =>
            CreateInteractionSparkle(sparkle.Position, sparkle.SourceAngle, sparkle.Visual),
        GoronCaveNpcSpawn goron => new GoronCaveRoomEntity(NpcCharacter.CreateFromRecord(goron.Record, AgesGameContent.AssetRoot), goron.Interactive),
        GoronBombSpawn bomb => new GoronBombRoomEntity(NpcCharacter.CreateFromRecord(bomb.Owner.Database.BombRecord, AgesGameContent.AssetRoot),bomb.Owner,bomb.SubId),
        TargetCartDebrisSpawn debris => new TargetCartDebrisRoomEntity(NpcCharacter.CreateFromRecord(debris.Record, AgesGameContent.AssetRoot),debris.Position,debris.Direction,_runtimeState),
        InteractionExplosionSpawn explosion =>
            CreateInteractionExplosion(explosion),
        TingleBalloonExplosionSpawn explosion =>
            CreateInteractionExplosion(new(explosion.Position, explosion.ZOffset, explosion.Visual)),
        WildTokayMeatSpawn => CreateWildTokayMeat(),
        TokayEntranceEyeSpawn eye =>
            new TokayEntranceEyeRoomEntity(eye.Record),
        TokaySeedlingDecorationSpawn seedling =>
            new TokaySeedlingDecorationRoomEntity(seedling.Record),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        GroundTreasureSpawn treasure => CreateGroundTreasure(treasure.Record),
        MapleDroppedItemSpawn item => CreateMapleDroppedItem(item, room),
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
        FireballShooterChildSpawn shooter => shooter.Parent.CreateChild(shooter.Position, shooter.TimingIndex),
        SmogProjectileSpawn smog => CreateSmogProjectile(smog, room),
        OctogonPartSpawn shot => new OctogonPartRoomEntity(new OctogonPart(shot,room,_runtimeState,_random,
            slot => owner.ResolveEnemySlot(slot)?.Node.Visible == true,owner.TryCreatePart,owner.OnSoundRequested),owner.OnSoundRequested),
        VireProjectileSpawn shot => new VireProjectileRoomEntity(new VireProjectile(enemies.VireProjectile,
            room, _runtimeState, shot, owner.ReadNativeEnemyHealth, owner.TryCreateVireProjectile, owner.CanAllocateParts,
            owner.TryCreateTransformationPuff, owner.InteractionAnimationParameter, owner.TryCreatePuzzlePuff), owner.OnSoundRequested),
        WizzrobeProjectileSpawn shot => new WizzrobeProjectileRoomEntity(
            new WizzrobeProjectile(enemies.WizzrobeProjectile, room, _runtimeState,
                shot.Position, shot.Angle, shot.ZHigh), () => -owner.ToScreen(Vector2.Zero), owner.OnSoundRequested),
        LynelBeamSpawn shot => new LynelBeamRoomEntity(enemies.LynelBeam, shot, owner),
        StatueEyeballSpawn eye => CreateStatueEyeball(eye),
        MovingPlatformSpawn platform =>
            CreateMovingPlatform(platform),
        GiantGhiniChildSpawn child => CreateGiantGhiniChild(child, room),
        ShadowHagBugSpawn bug => CreateShadowHagBug(bug),
        PumpkinHeadProjectileSpawn projectile =>
            CreatePumpkinHeadProjectile(projectile, room),
        HeadThwompProjectileSpawn projectile =>
            CreateHeadThwompProjectile(projectile, room),
        HeadThwompBoulderSpawn => CreateHeadThwompBoulder(room),
        TokayRescueEmberSpawn ember => new TokayRescueEmberRoomEntity(ember,owner.IsTextActive),
        TokayAttachedVisualSpawn accessory => new TokayAttachedVisualRoomEntity(
            accessory.Parent, accessory.Record, accessory.Offset),
        KingZoraPotionSpawn potion => new KingZoraPotionRoomEntity(NpcCharacter.CreateFromRecord(potion.Record, AgesGameContent.AssetRoot)),
        SeaPowderSpawn powder => new SeaPowderRoomEntity(powder, _random),
        SeaFairySparkleSpawn sparkle => new SeaFairySparkleRoomEntity(sparkle, _runtimeState, owner.ToScreen),
        CleanSeasZoraSpawn zora => new CleanSeasZoraRoomEntity(zora, _runtimeState, owner.IsScriptTextActive),
        CleansedFairySpawn fairy => new CleansedFairyRoomEntity(fairy.Data, fairy.Position, _runtimeState, owner.OnSoundRequested,
            message => owner.OnRoomEntityDialogueRequested(0x4109, message, fairy.Position),
            owner.TextActiveSource, owner.PaletteFadeActiveSource, fairy.Fade, fairy.Clean),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

    private void SpawnDimitriPreset(int group,OracleRoomData room)
    {
        if(_saveData is null||!Resources.DimitriStory.ShouldSpawnPreset(group,room.Id,_saveData)||!owner.SpecialObjectSlotAvailable)return;
        Vector2 position=Resources.DimitriStory.PresetPosition;
        CompanionScripts.SpawnPreset(SpecialObjectId.Dimitri, position);
    }
    private IRoomEntity CreateDimitri(DimitriCompanionSpawn spawn, OracleRoomData room)
    {
        _=_saveData??throw new InvalidOperationException("Dimitri requires live save state.");
        // State0 samples live flags on its visit, never during construction.
        if(!spawn.Riding && spawn.FluteDestination is null && _runtimeState.ReadWramByte(0xd104)==0)
            return CompanionScripts.CreateBody(SpecialObjectId.Dimitri,spawn.Position);
        return _common.TryCreate(spawn,room)??throw new InvalidOperationException("Common SPECIALOBJECT $0c binding is missing.");
    }

    private IRoomEntity CreateMoosh(MooshCompanionSpawn spawn, OracleRoomData room)
    {
        if (_runtimeState.ReadWramByte(0xd104) is 0 or 0x0a)
            return CompanionScripts.CreateBody(SpecialObjectId.Moosh, spawn.Position);
        return _common.TryCreate(spawn, room) ?? throw new InvalidOperationException("Common SPECIALOBJECT $0d binding is missing.");
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
            Resources.DungeonBosses.Enemy(AgesEnemyId.GiantGhiniChild), spawn.Owner, room, spawn.Index, _random);
        return new GiantGhiniChildRoomEntity(
            child, owner.OnSoundRequested);
    }

    private IRoomEntity CreateShadowHagBug(ShadowHagBugSpawn spawn)
    {
        var bug = new ShadowHagBug();
        bug.Initialize(
            Resources.DungeonBosses.Enemy(AgesEnemyId.ShadowHagBug),
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
            Resources.DungeonVisuals.Visual("shadow-hag-shadow"), _runtimeState);
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
                spawn.Angle, spawn.SubId, owner.TryCreatePart, spawn.ZHigh, _runtimeState));

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
                spawn, visual, impactVisual, room, _random, owner.OnSoundRequested, _runtimeState));
    }

    private VolcanoRoomEntity CreateVolcanoController(VolcanoPlacement record, OracleRoomData room) =>
        new(record, Resources.Volcano, room, _saveData, _random, owner.OnSoundRequested, owner.SetScreenShake,
            () => owner.ScreenIsShaking, owner.SetScreenShakeMagnitude, () => owner.PartSlotAvailable,
            owner.TryCreateInteraction);

    private IRoomEntity CreateHeadThwompBoulder(OracleRoomData room) =>
        new HostileProjectileRoomEntity<HeadThwompBoulder>(
            new HeadThwompBoulder(
                room,
                _random,
                Resources.DungeonVisuals.Visual("head-thwomp-boulder"),
                Resources.DungeonVisuals.Visual("head-thwomp-boulder-impact"),
                owner.OnSoundRequested));


    private IRoomEntity CreateStatueEyeball(StatueEyeballSpawn spawn)
    {
        var eye = new StatueEyeball();
        eye.Initialize(spawn.Position, Resources.DungeonEntrances);
        if (spawn.Puzzle) return new EyePuzzleStatueRoomEntity(eye, owner);
        return new StatueEyeballRoomEntity(eye);
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

        if (record is { Group: 1, Room: 0x90, Id: 0xd6, SubId: 0 })
            return new DekuScrubRoomEntity(
                NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), Resources.DekuScrub);

        if (record is { Id: AgesInteractionId.Pirate, SubId: <= 3 })
            return new PirateCrewRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), owner);

        if (record.Id == AgesInteractionId.ChildJabu && record.SubId == 0)
            return new ChildJabuRoomEntity(record);

        if (record is { Id: AgesInteractionId.Soldier, SubId: 0x0d })
            return new PalaceFriendlySoldierRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot),
                Resources.PalaceSoldiers, _saveData!, _runtimeState, owner.IsScriptTextActive,
                owner.OnRoomEntityDialogueRequested);

        if (record is { Id: AgesInteractionId.Miscellaneous1, SubId: 0x13 or 0x14 })
            return new GoronBombStatueRoomEntity(
                NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), room, _animationTick);

        if (record.Id == AgesInteractionId.KnowItAllBird)
        {
            var bird = new AgesKnowItAllBirdCharacter { Name = $"Npc_e3_{record.SubId:x2}" };
            bird.InitializeBird(record, Resources.KnowItAllBirds, _random, owner.IsTextActive);
            return new KnowItAllBirdRoomEntity(bird);
        }

        if (record is { Id: AgesInteractionId.ForestFairy, SubId: >= 0x0e and <= 0x10 })
            return new ForestHintFairyRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));

        if (record is { Id: AgesInteractionId.ForestFairy, SubId: >= 5 and <= 0x0d })
            return new ForestNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot),
                entity => owner.RefreshAgesNpcState([entity]));
        if (record is { Id: 0x5d, SubId: 2, Var03: 0 })
            return new WaitingNayruAnimalRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), owner,
                Resources.SharedNpcs, _saveData ?? throw new InvalidOperationException("bear.s:$02 requires save state."));

        if (record is { Id: 0x36, SubId: 0x13 } or { Id: 0x37, SubId: 0x11 }
            or { Id: 0x3c, SubId: 0x10 } or { Id: 0x39, SubId: 7, Var03: 1 }
            or { Id: 0x5d, SubId: 2, Var03: 1 } or { Id: 0x29, SubId: 0 }
            or { Id: 0x2e, SubId: 0 })
            return new AgesScriptNpcRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), Resources.SharedNpcs, owner);

        if (record is { Id: AgesInteractionId.ImpaNpc, SubId: 1 })
        {
            var impa = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
            int text = _saveData?.IsLinkedGame == true ? 0x012e : 0x012b;
            impa.SetDialogue(text, Resources.SharedNpcs.Text(text), true);
            return new PastImpaRoomEntity(impa);
        }

        if (record is { Id: AgesInteractionId.HardhatWorker, SubId: 3 })
            return new BlackTowerPatrollingWorkerRoomEntity(
                NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot), Resources.BlackTower);

        if (record is { Group: 1, Room: 0xba, Id: AgesInteractionId.Pirate, SubId: 0x04 })
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
                owner.BeginHorizontalScreenShake,
                owner.OnRoomMusicRequested,
                owner.OnRoomTileChanged,
                _animationTick, owner);
        }

        if (Resources.Tingle.Matches(record))
        {
            if (_inventory is null)
                throw new InvalidOperationException("Tingle requires live inventory state.");
            return new TingleRoomEntity(record, Resources.Tingle, _inventory);
        }

        if (record is { Id: AgesInteractionId.Tokay, SubId: >= 0x06 and <= 0x0a })
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

        if (record.Id == AgesInteractionId.Tokay)
        {
            var tokay = new TokayCharacter { Name = $"Npc_48_{record.SubId:x2}" };
            tokay.Initialize(record, AgesGameContent.AssetRoot);
            ConfigureTokayActor(tokay);
            if (record.SubId == 0x0b && tokay.Active)
                _runtimeState.SetWramByte(AgesWramAddress.wDiggingUpEnemiesForbidden, 1);
            if (record.SubId == 0x1e)
            {
                room.SetPositionTileAndCollision(tokay.Position, 0, 0x0f,
                    _animationTick(), preserveRenderedTile: true);
                owner.OnRoomTileChanged();
            }
            tokay.NativeAnimation = record.SubId switch
            {
                0x05 or >= 0x12 and <= 0x18 or 0x1f => TokayAnimationMode.FaceLink,
                >= 0x1a and <= 0x1c => TokayAnimationMode.Still,
                _ => TokayAnimationMode.Animate
            };
            if (record.SubId is 0x0f or 0x10)
                tokay.SetFacingDirection(record.SubId == 0x0f ? Vector2I.Right : Vector2I.Up);
            if (record.SubId is >=0x1a and <=0x1c)
                return new TokayStatueRoomEntity(tokay,owner,Resources.TokayNative,Resources.TokayInteractions);
            if (record.SubId == 0x1d && _saveData is not null && _inventory is not null)
            {
                bool returned = _saveData.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag40);
                if (returned) tokay.SetScriptAnimation(Resources.TokayInteractions.Animation(2));
            }
            return new TokayNpcRoomEntity(tokay);
        }

        if (record is { Group: 1, Room: 0xcb, Id: AgesInteractionId.Rosa, SubId: 0x00 })
        {
            NpcCharacter tokayActor = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
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
            return new NpcTalkRoomEntity(comedian, scriptSensitive: true);
        }
        if (record.Id == AgesInteractionId.Tokkey && record.SubId == 0)
            return new TokkeyRoomEntity(NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
        if (record.Group == Resources.DumbbellMan.Record.Group &&
            record.Room == Resources.DumbbellMan.Record.Room &&
            record.Id == Resources.DumbbellMan.Record.InteractionId &&
            record.SubId == Resources.DumbbellMan.Record.SubId)
        {
            var man = new TradeNpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            man.InitializeTradeNpc(record, Resources.DumbbellMan.Record);
            return new NpcTalkRoomEntity(man);
        }
        if (record.Id == AgesInteractionId.OldZora && record.SubId == 0)
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
            var salesman = new TradeNpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = ObjectDrawPriority.BehindLinkZIndex
            };
            salesman.InitializeTradeNpc(record, Resources.MaskSalesman.Record);
            return new NpcTalkRoomEntity(salesman);
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
        if (Resources.RalphAfterCheval.Matches(record) || record is { Id: AgesInteractionId.Ralph, SubId: 0x0b })
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
            return new NpcTalkRoomEntity(boy, scriptSensitive: true);
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
                Id: AgesInteractionId.Postman,
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
                Id: AgesInteractionId.ToiletHand,
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

        NpcCharacter npc = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
        if (record.Id == AgesInteractionId.OldMan && record.SubId <= 6)
            return new LibraryActorRoomEntity(npc, Resources.Library, _saveData, _runtimeState);
        if (Resources.Library.IsReader(record))
            return new LibraryReaderRoomEntity(npc, Resources.Library, _saveData, owner);
        if (record.Id == AgesInteractionId.MamamuYan && record.SubId == 0)
        {
            npc.SetAnimationRate(0);
            npc.SetScriptButtonSensitive(false);
            return new MamamuRoomEntity(npc);
        }
        if (record.Id == AgesInteractionId.MamamuDog && record.SubId == 0)
        {
            if (_saveData is not null)
                npc.SetActive(new NpcVisibilityRuleDatabase(AssetRoot).ShouldShow(record, _saveData, _runtimeState));
            return new MamamuDogRoomEntity(npc, () => _random.Next().Value, owner.IsTextActive);
        }
        if (record.Id == AgesInteractionId.MamamuDog && record.SubId == 1)
        {
            if (_saveData is null)
                throw new InvalidOperationException($"{NpcSource(record)} requires save data for roaming dog $54:$01.");
            return new MamamuRoamingDogRoomEntity(npc,_saveData,_runtimeState,
                () => _random.Next().Value,owner.OnRoomEntityDialogueRequested,
                owner.OnSoundRequested,owner.OnRoomWarpRequested,owner.IsTextActive);
        }
        if (record is { Id: AgesInteractionId.OldManWithRupees, SubId: 0x01 })
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
            return new AgesTroyHouseRoomEntity(
                npc, Resources.TroyHouse, _saveData, _random);
        }
        if (record is { Id: InteractionId.Bipin, SubId: 0x00 })
            return new RunningBipinRoomEntity(
                npc, _familyState.RunningBipin, _runtimeState);
        if (record is { Id: InteractionId.Bipin, SubId: 0x0a })
            return new PastBipinRoomEntity(npc);
        if (record is { Group: 5, Room: 0xcf, Id: InteractionId.Farore, SubId: 0 })
            return new FaroreRoomEntity(npc);
        if (record is
            {
                Group: 0,
                Room: 0x83,
                Id: AgesInteractionId.GreatFairy,
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
                Id: AgesInteractionId.LinkedGameGhini,
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
            if (record.Id == InteractionId.Child)
                return new FamilyChildRoomEntity(npc,
                    _childMotion ??= new FamilyChildMotionDatabase(AssetRoot), owner);
            return new FamilyNpcRoomEntity(npc);
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
            _animationTick,
            _runtimeState,
            owner.ToScreen);
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
        debris.Initialize(Resources.ShootingGallery.Debris, spawn, _runtimeState);
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
            if (record is { Id: AgesInteractionId.ImpaNpc, SubId: 0x00 })
                impas.Add(record);
            else if (record is { Id: AgesInteractionId.Nayru, SubId: 0x0b })
                nayru = record;
            else if (record is { Id: AgesInteractionId.Zelda, SubId: 0x07 })
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
            NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot),
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
            NpcCharacter.CreateFromRecord(records[0], AgesGameContent.AssetRoot),
            Resources.MakuSproutRoom,
            _saveData);
        if (!_saveData.HasGlobalFlag(Resources.MakuSproutRoom.Record.FinishedFlag))
            yield break;

        yield return new MakuLinkStatueRoomEntity(
            NpcCharacter.CreateFromRecord(
                Resources.MakuSproutRoom.Record.CreateStatueNpcRecord(), AgesGameContent.AssetRoot),
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
            if (record is { Id: AgesInteractionId.PickaxeWorker, SubId: 0x00 })
            {
                RequireNpcImplementation(
                    record,
                    NpcImplementationClassification.SpecializedNative);
                if (foundWorker)
                    throw new InvalidOperationException(
                        "Room 1:48 contains more than one pickaxe worker $57:$00.");
                foundWorker = true;
                NpcCharacter npc = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
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
                    NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot));
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
            NpcCharacter npc = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
            yield return new VasuShopNpcRoomEntity(npc, Resources.VasuShop);
        }
    }

    private IEnumerable<IRoomEntity> CreateLynnaShop(
        OracleRoomData room,
        IReadOnlyList<NpcRecord> records)
    {
        using var diagnostics = OracleRoomLoadDiagnostics.Begin("shop-stock", room.Group, room.Id);
        LynnaShopDatabase database = room.Id == 0xfe ? new(advance: true) : room.Id == Resources.HiddenShop.Room ? Resources.HiddenShop : Resources.LynnaShop;
        diagnostics?.Mark("database");
        if (records.Count != 1 || records[0].Id != InteractionId.Shopkeeper ||
            records[0].SubId != database.ShopkeeperSubId)
        {
            throw new InvalidOperationException(
                $"Room 2:{room.Id:x2} must contain shopkeeper $46:${database.ShopkeeperSubId:x2}, got {records.Count} NPC records.");
        }

        // The three $47 placements precede $46:$00 in mainData.s. Stock
        // deletion and replacement run from the placement's state0 slot.
        Texture2D[]? priceDigits = null;
        diagnostics?.Mark("stock-selection");
        foreach (ItemRecord placement in database.Placements)
        {
            var item = new LynnaShopItem
            {
                Name = $"ShopItem_{placement.Order}_{placement.SubId:x2}", Visible = false,
                ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex
            };
            item.Place(new(placement, placement.Order, placement.Y, placement.X));
            diagnostics?.Mark("node-construction");
            var entity = new LynnaShopItemRoomEntity(item, _runtimeState, () =>
            {
                StockRecord? selected = database.ResolveStock(_saveData).Where(stock => stock.Order == placement.Order)
                    .Select(stock => (StockRecord?)stock).SingleOrDefault();
                if (selected is not { } stock) return false;
                priceDigits ??= LynnaShopItem.BuildPriceDigitTextures(room);
                item.Name = $"ShopItem_{stock.Order}_{stock.Item.SubId:x2}";
                item.Initialize(stock, room, priceDigits);
                item.Visible = true;
                return true;
            });
            diagnostics?.Mark("adapter-construction");
            yield return entity;
            diagnostics?.Mark("attachment");
        }

        NpcRecord record = records[0];
        RequireNpcImplementation(
            record,
            NpcImplementationClassification.SpecializedNative);
        NpcCharacter shopkeeper = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);
        yield return new LynnaShopkeeperRoomEntity(shopkeeper, database);

    }

    private IEnumerable<IRoomEntity> CreateTokayShop(
        IReadOnlyList<NpcRecord> records)
    {
        if (_inventory is null || _saveData is null)
        {
            throw new InvalidOperationException(
                "Room 2:e4 Tokay trading hut requires live inventory and save state.");
        }
        if (records.Count != 1 || records[0] is not { Id: AgesInteractionId.Tokay, SubId: 0x0e })
        {
            throw new InvalidOperationException(
                $"Room 2:e4 must contain Tokay shopkeeper $48:$0e, got {records.Count} NPC records.");
        }

        foreach (TokayShopPlacementRecord placement in Resources.TokayShop.Placements)
        {
            int subId;
            int treasure;
            bool deleteOnInitialization=false;
            if (placement.PlacedSubId == 0)
            {
                if (_saveData.HasGlobalFlag(Resources.TokayShop.BoughtFeatherFlag))
                    deleteOnInitialization=true;
                bool replace = _inventory.HasTreasure(TreasureId.Feather);
                subId = replace ? 2 : 0;
                treasure = replace
                    ? TreasureId.Shovel
                    : TreasureId.Feather;
            }
            else if (placement.PlacedSubId == 1)
            {
                if (_saveData.HasGlobalFlag(Resources.TokayShop.BoughtBraceletFlag))
                    deleteOnInitialization=true;
                bool replace = _inventory.HasTreasure(TreasureId.Bracelet);
                subId = replace ? 3 : 1;
                treasure = replace
                    ? TreasureId.Shovel
                    : TreasureId.Bracelet;
            }
            else
            {
                if (!_saveData.HasGlobalFlag(Resources.TokayShop.BoughtBraceletFlag))
                    deleteOnInitialization=true;
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
                DeleteOnInitialization=deleteOnInitialization,
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
        npc.Initialize(shopkeeper, AgesGameContent.AssetRoot);
        ConfigureTokayActor(npc);
        yield return new TokayNpcRoomEntity(npc);
    }

    private void ConfigureTokayActor(NpcCharacter npc)
    {
        if (_saveData is null || _inventory is null)
            return;

        NpcRecord record = npc.Record;
        if (record.Id == AgesInteractionId.Rosa)
        {
            // rosa.s:@@state0 reads wEssencesObtained directly after the
            // linked-game check; the inventory view may not have refreshed yet.
            bool visible = _saveData.IsLinkedGame &&
                (_saveData.ReadWramByte(AgesWramAddress.wEssencesObtained) & 0x04) == 0;
            npc.SetActive(visible);
            return;
        }
        if (record.Id != AgesInteractionId.Tokay)
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
            NpcCharacter npc = NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);

            IRoomEntity entity = record switch
            {
                { Id: AgesInteractionId.MaleVillager, SubId: 0x02 } =>
                    new BlackTowerBlockingVillagerRoomEntity(npc, Resources.BlackTower),
                { Id: AgesInteractionId.Soldier, SubId: 0x0c } =>
                    new BlackTowerSoldierRoomEntity(npc, Resources.BlackTower, _random),
                { Id: AgesInteractionId.PickaxeWorker, SubId: 0x03 } =>
                    new BlackTowerPickaxeWorkerRoomEntity(
                        npc, Resources.Room148.Record, Resources.BlackTower, _random, owner.OnSoundRequested),
                { Id: AgesInteractionId.HardhatWorker, SubId: 0x00 } =>
                    new BlackTowerShovelWorkerRoomEntity(npc, Resources.BlackTower),
                { Id: AgesInteractionId.HardhatWorker, SubId: 0x03 } =>
                    new BlackTowerPatrollingWorkerRoomEntity(
                        npc, Resources.BlackTower),
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
            => NpcCharacter.CreateFromRecord(record, AgesGameContent.AssetRoot);

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
                !_saveData.HasGlobalFlag(AgesGlobalFlag.MakuTreeSaved),
            2 => _saveData is null ||
                !_saveData.HasTreasure(TreasureId.SeedSatchel),
            _ => false
        };
    }

    private IRoomEntity CreateMaskedMoblin(
        MaskedMoblinSpawn spawn, OracleRoomData room)
    {
        var moblin = new ArrowMoblinCharacter
        {
            Name = "MaskedMoblin",
            ZIndex = ObjectDrawPriority.BehindLinkZIndex
        };
        moblin.Initialize(EnemyDefinitionDatabase.For(room.AssetRoot).Get(EnemyId.MaskedMoblin),
            room, spawn.Position, _random);
        EnemyHandlerDescriptor handler = enemies.EnemyHandlers.ResolveHandler(
            EnemyId.MaskedMoblin,
            0,
            "scripts/ages/scriptHelper.s:moblin_spawnEnemyHere");
        return new ArrowMoblinRoomEntity(
            moblin,
            handler.CombatSource(
                objectFlags: 0,
                killableEnemyIndex: 0,
                source:
                    "scripts/ages/scriptHelper.s:moblin_spawnEnemyHere"),
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
            _animationTick,
            _random);
        const string source = "object_code/common/enemies/flyingTile.s:flyingTile_state8";
        EnemyHandlerDescriptor handler = enemies.EnemyHandlers.ResolveHandler(
            spawn.Definition.Id, spawn.Definition.SubId, source);
        // Both Ages layouts create active children with subid $80.
        EnemyCombatSourceDescriptor combatSource = handler.CombatSource(
            spawn.CountsAsEnemy ? 0 : 2, 0, source) with { SubId = 0x80 };
        return new FlyingTileRoomEntity(tile, combatSource);
    }

    private IRoomEntity CreateCuccoAttacker(CuccoAttackerSpawn spawn)
    {
        var attacker = new CuccoAttackerCharacter
        {
            Name = "CuccoAttacker",
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex
        };
        attacker.Initialize(enemies.ImportedEnemy(EnemyId.Cucco), _random, spawn.HitCount, owner.ToScreen);
        return new CuccoAttackerRoomEntity(attacker);
    }

    private IRoomEntity CreateStalfosBone(StalfosBoneSpawn spawn, OracleRoomData room)
    {
        var bone = new StalfosBoneProjectile { Name = "StalfosBone", ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex };
        bone.Initialize(StalfosBoneRecord.Load(AgesGameContent.AssetRoot), room, spawn.Position, owner.ToScreen, spawn.ZHigh);
        return new StalfosBoneRoomEntity(bone);
    }

    private IRoomEntity CreateKeeseFire(KeeseFireSpawn spawn)
    {
        var fire = new KeeseFirePart { Name = "KeeseFire", ZIndex = ObjectDrawPriority.BehindLinkZIndex };
        fire.Initialize(AgesGameContent.AssetRoot, spawn.Position, spawn.ZHigh, owner.OnSoundRequested);
        return new KeeseFireRoomEntity(fire);
    }

    private IRoomEntity CreateRoom148Debris(Room148DebrisSpawn spawn)
    {
        var debris = new Room148PickaxeDebris
        {
            Name = "Room148PickaxeDebris"
        };
        debris.Initialize(Resources.Room148.Record, spawn);
        return new AlwaysUpdateFixedEffectRoomEntityAdapter<Room148PickaxeDebris>(
            debris);
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
        Vector2 position, int sourceAngle, InteractionSparkleVisual visual,int subid=0)
    {
        var sparkle = new InteractionSparkleEffect
        {
            Name = $"InteractionSparkle_84_{subid:x2}",
            // sparkle.s selects visible81 for any nonzero source angle.
            ZIndex = subid==2||sourceAngle == 0
                ? ObjectDrawPriority.BehindLinkZIndex
                : ObjectDrawPriority.InFrontOfLinkZIndex
        };
        sparkle.Initialize(position, sourceAngle, visual,subid);
        return new InteractionSparkleRoomEntity(sparkle);
    }

    private IRoomEntity CreateDungeonSwitch(DungeonMechanicDatabaseRecord record,OracleRoomData room) =>
        _common.CreateDungeonSwitch(record,room);

    private IRoomEntity CreateSwitchTileToggler(DungeonObjectRecord record,OracleRoomData room) =>
        _common.CreateSwitchTileToggler(record,room);




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
        npc.Initialize(spawn.Record, AgesGameContent.AssetRoot);
        return new CutsceneNpcRoomEntity(npc, spawn.Talkable, spawn.Solid);
    }

    private IRoomEntity CreateGroundTreasure(GroundTreasureDatabaseRecord record)
    {
        var treasure = new GroundTreasurePickup
        {
            Name = $"GroundTreasure_{record.TreasureObject}",
            ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex
        };
        treasure.Initialize(AgesGameContent.AssetRoot, record, owner.OnSoundRequested, owner.ToScreen, tryCreatePuff: owner.TryCreatePuzzlePuff);
        // Dynamic INTERAC$60 remains hidden until its own state0 visit. A
        // newborn allocated below the live interaction cursor waits one pass.
        treasure.Visible = false;
        return new GroundTreasureRoomEntity(
            treasure, _runtimeState, _treasures!.GetObject(record.TreasureObject).TreasureId,0,owner.CanCollectGroundTreasure,
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
        pickup.Initialize(AgesGameContent.AssetRoot,
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
        pickup.Initialize(AgesGameContent.AssetRoot,
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
        var state = new LeverState(_runtimeState, AgesWramAddress.wLever1PullDistance);
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
                    pickup.Initialize(AgesGameContent.AssetRoot,
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
            owner.OnMapleItemCollected,
            owner.RuntimeState);
        spawn.Encounter.Register(item);
        return new MapleDroppedItemRoomEntity(item, owner);
    }

    private IRoomEntity CreateMapleTouchingBook(MapleTouchingBookSpawn spawn, OracleRoomData room) =>
        new MapleTouchingBookRoomEntity(spawn, owner.RuntimeState, owner.OnSoundRequested,
            position => owner.TryCreateGroundTreasure(new(room.Group, room.Id, 0,
                (int)position.Y, (int)position.X, "TREASURE_OBJECT_TRADEITEM_09",
                "touchingBook.s:@state7 createTreasure TREASURE_TRADEITEM,$09")
            {
                ExpectedTreasureId = TreasureId.TradeItem, ExpectedSubId = 0x09,
                ExpectedObjectParameter = 0x09
            }));

    public IEnumerable<IRoomEntity> CreateTimePortals(int group, OracleRoomData room)
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
            () => _runtimeState.ReadWramByte(AgesWramAddress.wcde0) != 0);
        return new TimePortalRoomEntity(portal, owner.OnTimePortalEntered);
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);

    private bool DungeonEnemyCountIsComplete(int group, OracleRoomData room)
    {
        foreach (RoomObjectRecord source in
            enemies.GetRoomObjects(group, room.Id))
        {
            if (!owner.RoomObjectConditionMet(source))
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
        foreach (DungeonObjectRecord native in Resources.AncientTomb.GetRoomRecords(group,room.Id))
            if (native.Kind is DungeonObjectKind.BlueStalfos or DungeonObjectKind.Ramrock) hasSupportedNativeBossRecord = true;
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

    public IRoomEntity CreatePlacedPart(RoomObjectRecord source, OracleRoomData room)
    {
        if (source.Kind!=RoomObjectKind.ReservingPart)
            throw new NotSupportedException($"{source.Source}: parameter PART ${source.Id:x2}:${source.SubId:x2} has no construction binding.");
        Vector2 position=PointForPackedPosition(source.PackedPosition);
        return source.Id switch
        {
            PartId.OwlStatue => new OwlStatueRoomEntity(source,Resources.OwlStatues.Record(source.SubId),room,
                owner.OnOwlStatueMessageRequested,owner.TryCreateOwlSparkle,_animationTick),
            PartId.WallArrowShooter => new WallArrowShooterRoomEntity(source,owner.TryCreateWallArrow),
            0x45 => new FallingBoulderRoomEntity(new FallingBoulder(source.SubId,position,
                Resources.FallingBoulders,_random,owner.OnSoundRequested,room,owner.ToScreen)),
            0x59 => new BlackTowerFlameRoomEntity(owner,Resources.BlackTowerFlames,source.SubId,position),
            PartId.FallingFire => new FallingFireRoomEntity(owner,Resources.PlacedParts,source.SubId,position),
            PartId.SmallFairy => new SmallFairyRoomEntity(owner,Resources.PlacedParts,position),
            _ => throw new NotSupportedException($"{source.Source}: unsupported placed PART ${source.Id:x2}:${source.SubId:x2}.")
        };
    }

    public IRoomEntity? CreatePlacedEnemy(EnemyHandlerDescriptor handler, RoomObjectRecord source,
        OracleRoomData room, Vector2 position, int instance, int killableIndex, EnemyPlacementContext placement) =>
        EnemyFactory.CreateOrderedEnemy(handler,source,room,position,instance,killableIndex,placement);

}

internal sealed record GreenPoofSpawn(Vector2 Position, int Z) : RoomEntitySpawn;

internal sealed record CutsceneNpcSpawn(
    NpcRecord Record,
    string Name,
    bool Talkable = false,
    bool Solid = false)
    : RoomEntitySpawn;









internal sealed record CuccoAttackerSpawn(int HitCount)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MovingPlatformSpawn(Vector2 Position, int SubId)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record SubterrorDirtSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);








internal sealed record PumpkinHeadProjectileSpawn(Vector2 Position, int Angle, int SubId = 0, int ZHigh = 0)
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

internal sealed record MaskedMoblinSpawn(Vector2 Position)
    : RoomEntitySpawn(UpdateThisFrame: true);

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

internal sealed record RespawnableBushSpawn(
    int PackedPosition,
    int DropSubId)
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
