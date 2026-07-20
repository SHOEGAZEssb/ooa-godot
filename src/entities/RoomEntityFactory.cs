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
    Action<Vector2, OracleRoomData.HazardType> itemDropEnteredHazard,
    Action<int> soundRequested,
    Func<int> roomEnemyCount,
    Func<int, bool> enemyWasKilled,
    Func<int, bool> triggerActive,
    Func<int> triggerState,
    Action<int, bool> setTrigger,
    Action roomTileChanged,
    Func<Vector2, Vector2> worldToScreen,
    Func<long> animationTick)
{
    private readonly Room148PickaxeDatabase _room148 = new();
    private readonly Room149FamilyDatabase _room149 = new();
    private readonly BlackTowerWorkerDatabase _blackTower = new();
    private readonly EnemySpawnTileDatabase _enemySpawnTiles = new();
    private readonly GroundTreasureDatabase _groundTreasures = new();
    private readonly DungeonMechanicDatabase _dungeonMechanics = new();
    private readonly RoomTileChangeWatcherDatabase _tileChangeWatchers = new();
    private readonly BreakableTileDatabase _breakables = new();

    public IEnumerable<IRoomEntity> CreateRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        // Buttons and trigger-controlled shutters use wActiveTriggers without
        // depending on the enemy roster. Push triggers require a complete live
        // wNumEnemies equivalent. Enemy-shutter controllers are retained even
        // when that count is incomplete so an incoming shutter can perform the
        // original entry substitution. That crossed route remains open for
        // safe backtracking; solving and non-entry shutters stay disabled.
        IReadOnlyList<DungeonMechanicDatabase.Record> dungeonRecords =
            _dungeonMechanics.GetRoomRecords(group, room.Id);
        bool enemyMechanicsSupported = DungeonEnemyMechanicsAreSupported(
            dungeonRecords, group, room);
        foreach (DungeonMechanicDatabase.Record record in dungeonRecords)
        {
            if (record.Id == 0x09)
            {
                yield return new GroundButtonRoomEntity(
                    record, room, _dungeonMechanics, setTrigger,
                    animationTick, soundRequested);
                continue;
            }
            if (record.Id is 0x20 or 0x21)
            {
                yield return new TriggerChestRoomEntity(
                    record, room, _dungeonMechanics, triggerState,
                    () => saveData?.HasRoomFlag(
                        group, room.Id, OracleSaveData.RoomFlagItem) == true,
                    animationTick, soundRequested);
                continue;
            }
            if (record.Id == 0x13 && !enemyMechanicsSupported)
                continue;
            yield return record.Id switch
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

        if (saveData is not null)
        {
            foreach (RoomTileChangeWatcherDatabase.Record record in
                _tileChangeWatchers.GetRoomRecords(group, room.Id))
            {
                yield return new RoomTileChangeWatcherRoomEntity(
                    record, room, saveData);
            }
        }

        IReadOnlyList<NpcDatabase.NpcRecord> roomNpcs =
            npcs.GetRoomNpcs(group, room.Id, saveData, runtimeState);
        if (group == 4 && room.Id is 0xe0 or 0xe1 or 0xe2 or 0xe7 or 0xe8)
        {
            foreach (IRoomEntity entity in CreateBlackTowerNpcs(
                room.Id, roomNpcs, placementContext))
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
        else
        {
            foreach (NpcDatabase.NpcRecord record in roomNpcs)
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

        foreach (GroundTreasureDatabase.Record record in
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
            treasure.Initialize(record, soundRequested);
            yield return new GroundTreasureRoomEntity(
                treasure, groundTreasureCollectionAllowed,
                groundTreasureCollected);
        }

        foreach (IRoomEntity portal in CreateTimePortals(group, room))
            yield return portal;

        var reservations = new EnemyPlacementReservations();
        int enemySlots = 0;
        int partSlots = 0;
        int killableEnemies = 0;
        foreach (EnemyDatabase.RoomObjectRecord source in enemies.GetRoomObjects(group, room.Id))
        {
            if (!RoomObjectConditionMet(source, group, room))
                continue;

            switch (source.Kind)
            {
                case EnemyDatabase.RoomObjectKind.RandomEnemy:
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

                case EnemyDatabase.RoomObjectKind.FixedEnemy:
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

                case EnemyDatabase.RoomObjectKind.ParameterEnemy:
                    if (enemySlots < 16)
                        enemySlots++;
                    break;

                case EnemyDatabase.RoomObjectKind.ItemDrop:
                    int itemKillableEnemyIndex = NextKillableEnemyIndex(
                        source.Flags, ref killableEnemies);
                    if (enemyWasKilled(itemKillableEnemyIndex))
                        break;
                    if (enemySlots >= 16)
                        break;
                    enemySlots++;
                    reservations.Add(source.PackedPosition);
                    break;

                case EnemyDatabase.RoomObjectKind.ReservingPart:
                    if (partSlots >= 16)
                        break;
                    partSlots++;
                    reservations.Add(source.PackedPosition);
                    break;

                case EnemyDatabase.RoomObjectKind.ParameterPart:
                    if (partSlots < 16)
                        partSlots++;
                    break;
            }
        }
    }

    private IRoomEntity? CreateOrderedEnemy(
        EnemyDatabase.RoomObjectRecord source,
        OracleRoomData room,
        Vector2 position,
        int instance,
        int killableEnemyIndex)
    {
        if (source.Id == 0x32 && enemies.TryGetKeeseDefinition(source, out EnemyDatabase.EnemyRecord keeseRecord))
        {
            var keese = new KeeseCharacter
            {
                Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            keese.Initialize(keeseRecord, room, position, random);
            return new KeeseRoomEntity(keese, killableEnemyIndex);
        }

        if (source.Id == 0x09 &&
            enemies.TryGetOctorokDefinition(source, out EnemyDatabase.OctorokRecord octorokRecord))
        {
            var octorok = new OctorokCharacter
            {
                Name = $"Octorok_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            octorok.Initialize(octorokRecord, room, position, random);
            return new OctorokRoomEntity(octorok, soundRequested, killableEnemyIndex);
        }

        if (source.Id == 0x31 &&
            enemies.TryGetStalfosDefinition(source, out EnemyDatabase.StalfosRecord stalfosRecord))
        {
            var stalfos = new StalfosCharacter
            {
                Name = $"Stalfos_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            stalfos.Initialize(stalfosRecord, room, position, random);
            return new StalfosRoomEntity(stalfos, soundRequested, killableEnemyIndex);
        }

        if (source.Id == 0x34 &&
            enemies.TryGetZolDefinition(source, out EnemyDatabase.ZolRecord zolRecord))
        {
            var zol = new ZolCharacter
            {
                Name = $"Zol_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            zol.Initialize(zolRecord, room, position, random);
            return new ZolRoomEntity(zol, soundRequested, killableEnemyIndex);
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
        GelSpawn gel => CreateGel(gel, room),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        ItemDropSpawn drop => CreateItemDrop(drop, room),
        ShovelDebrisSpawn debris => CreateShovelDebris(debris),
        EmberSeedSpawn seed => CreateEmberSeed(seed, room),
        PuzzlePuffSpawn puff => CreatePuzzlePuff(puff),
        FallingDownHoleSpawn fall => CreateFallingDownHole(fall),
        DungeonKeyUseSpawn key => CreateDungeonKeyUse(key),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        GroundTreasureSpawn treasure => CreateGroundTreasure(treasure.Record),
        Room148DebrisSpawn debris => CreateRoom148Debris(debris),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

    private IEnumerable<IRoomEntity> CreateRoom148Npcs(
        IReadOnlyList<NpcDatabase.NpcRecord> records)
    {
        bool foundWorker = false;
        foreach (NpcDatabase.NpcRecord record in records)
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
                Room148PickaxeDatabase.PickaxeRecord pickaxe = _room148.Record;
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

    private IEnumerable<IRoomEntity> CreateBlackTowerNpcs(
        int room,
        IReadOnlyList<NpcDatabase.NpcRecord> records,
        EnemyPlacementContext placementContext)
    {
        for (int index = 0; index < records.Count; index++)
        {
            NpcDatabase.NpcRecord record = records[index];
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
                yield return new BlackTowerEntranceRoomEntity(
                    new Vector2(0x78, 0x88), _blackTower,
                    placementContext.Kind == EnemyPlacementEntryKind.ScreenWarp,
                    dungeonEntranceTriggered);
            }
        }
    }

    private IEnumerable<IRoomEntity> CreateRoom149Family(
        IReadOnlyList<NpcDatabase.NpcRecord> records)
    {
        NpcDatabase.NpcRecord Find(int id, int subId)
        {
            foreach (NpcDatabase.NpcRecord record in records)
            {
                if (record.Id == id && record.SubId == subId)
                    return record;
            }
            throw new InvalidOperationException(
                $"Room 1:49 is missing interaction ${id:x2}:${subId:x2}.");
        }

        NpcCharacter CreateNpc(NpcDatabase.NpcRecord record)
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
        return new MaskedMoblinRoomEntity(moblin, soundRequested);
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
            gel, soundRequested, countsAsEnemy,
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
            spawn.Group);
        return new EmberSeedRoomEntity(seed);
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

    private IRoomEntity CreateDeathPuff(EnemyDeathPuffSpawn spawn)
    {
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position, spawn.HighKnockback, spawn.EnemyId);
        soundRequested(OracleSoundEngine.SndKillEnemy);
        return new DeathPuffRoomEntity(puff, itemDrops, random);
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
        drop.Initialize(
            spawn.SubId, spawn.Position, room, itemDrops.GetVisual(spawn.SubId),
            spawn.Angle, spawn.DugUp);
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

    private IRoomEntity CreateGroundTreasure(GroundTreasureDatabase.Record record)
    {
        var treasure = new GroundTreasurePickup
        {
            Name = $"GroundTreasure_{record.TreasureObject}",
            ZIndex = 12
        };
        treasure.Initialize(record, soundRequested);
        return new GroundTreasureRoomEntity(
            treasure, groundTreasureCollectionAllowed,
            groundTreasureCollected);
    }

    internal IEnumerable<IRoomEntity> CreateTimePortals(int group, OracleRoomData room)
    {
        foreach (TimePortalDatabase.PortalRecord record in timePortals.GetRoomPortals(group, room.Id))
        {
            if (!StartsActive(record.SubId))
                continue;
            var portal = new TimePortal { Name = $"TimePortal_{record.SubId:x2}", ZIndex = 8 };
            portal.Initialize(record, room);
            yield return new TimePortalRoomEntity(portal, portalEntered);
        }
    }

    private bool RoomObjectConditionMet(
        EnemyDatabase.RoomObjectRecord record,
        int group,
        OracleRoomData room)
    {
        int stateModifier = (room.TilesetFlags & 0x40) != 0 ? 1 : 0;
        if (saveData?.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlagLayoutSwap) == true)
            stateModifier++;
        return (record.ConditionMask & (1 << stateModifier)) != 0;
    }

    private bool DungeonEnemyCountIsComplete(int group, OracleRoomData room)
    {
        foreach (EnemyDatabase.RoomObjectRecord source in
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
                case EnemyDatabase.RoomObjectKind.RandomEnemy:
                case EnemyDatabase.RoomObjectKind.FixedEnemy:
                    bool supported = source.Id switch
                    {
                        0x09 => enemies.TryGetOctorokDefinition(source, out _),
                        0x31 => enemies.TryGetStalfosDefinition(source, out _),
                        0x32 => enemies.TryGetKeeseDefinition(source, out _),
                        0x34 => enemies.TryGetZolDefinition(source, out _),
                        0x43 => source.SubId == 0,
                        _ => false
                    };
                    if (!supported)
                        return false;
                    break;

                case EnemyDatabase.RoomObjectKind.ParameterEnemy:
                    return false;
            }
        }
        return true;
    }

    private bool DungeonEnemyMechanicsAreSupported(
        IReadOnlyList<DungeonMechanicDatabase.Record> records,
        int group,
        OracleRoomData room)
    {
        bool hasSupportedDoor = false;
        foreach (DungeonMechanicDatabase.Record record in records)
        {
            if (record.Id == 0x09)
                continue;
            if (record.Id == 0x1e)
                hasSupportedDoor = true;
            if (record.Id == 0x1e && record.SubId <= 0x07)
                continue;
            if (!record.CountSourceComplete)
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

internal sealed class EnemyPlacementReservations
{
    private readonly byte[] _positions = new byte[16];
    private int _count;

    internal int Count => _count;

    internal bool Contains(int packedPosition)
    {
        for (int index = 0; index < _count; index++)
        {
            if (_positions[index] == packedPosition)
                return true;
        }
        return false;
    }

    internal void Add(int packedPosition)
    {
        if (packedPosition is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(packedPosition));
        _positions[_count] = (byte)packedPosition;
        _count = (_count + 1) & 0x0f;
    }
}
