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
    Action<int> soundRequested)
{
    private readonly Room148PickaxeDatabase _room148 = new();
    private readonly Room149FamilyDatabase _room149 = new();
    private readonly EnemySpawnTileDatabase _enemySpawnTiles = new();

    public IEnumerable<IRoomEntity> CreateRoomEntities(
        int group,
        OracleRoomData room,
        EnemyPlacementContext placementContext)
    {
        IReadOnlyList<NpcDatabase.NpcRecord> roomNpcs =
            npcs.GetRoomNpcs(group, room.Id, saveData, runtimeState);
        if (group == 1 && room.Id == 0x48)
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

        foreach (IRoomEntity portal in CreateTimePortals(group, room))
            yield return portal;

        var reservations = new EnemyPlacementReservations();
        int enemySlots = 0;
        int partSlots = 0;
        foreach (EnemyDatabase.RoomObjectRecord source in enemies.GetRoomObjects(group, room.Id))
        {
            if (!RoomObjectConditionMet(source, group, room))
                continue;

            switch (source.Kind)
            {
                case EnemyDatabase.RoomObjectKind.RandomEnemy:
                    for (int instance = 0; instance < source.Count; instance++)
                    {
                        if (enemySlots >= 16)
                            break;
                        enemySlots++;
                        if (!TryChooseRandomEnemyPosition(
                            room, source.Flags, reservations, placementContext,
                            out Vector2 position))
                        {
                            continue;
                        }
                        IRoomEntity? entity = CreateOrderedEnemy(source, room, position, instance);
                        if (entity is not null)
                            yield return entity;
                    }
                    break;

                case EnemyDatabase.RoomObjectKind.FixedEnemy:
                    if (enemySlots >= 16)
                        break;
                    enemySlots++;
                    reservations.Add(source.PackedPosition);
                    IRoomEntity? fixedEntity = CreateOrderedEnemy(
                        source, room, new Vector2(source.X, source.Y), 0);
                    if (fixedEntity is not null)
                        yield return fixedEntity;
                    break;

                case EnemyDatabase.RoomObjectKind.ParameterEnemy:
                    if (enemySlots < 16)
                        enemySlots++;
                    break;

                case EnemyDatabase.RoomObjectKind.ItemDrop:
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
        int instance)
    {
        if (source.Id == 0x32 && enemies.TryGetKeeseDefinition(source, out EnemyDatabase.EnemyRecord keeseRecord))
        {
            var keese = new KeeseCharacter
            {
                Name = $"Keese_{source.SubId:x2}_{source.Order}_{instance}",
                ZIndex = 10
            };
            keese.Initialize(keeseRecord, room, position, random);
            return new KeeseRoomEntity(keese);
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
            return new OctorokRoomEntity(octorok);
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
            return new ZolRoomEntity(zol);
        }

        return source.Id == 0x43 && source.SubId == 0
            ? Create(new GelSpawn(position, $"RoomGel_{source.Order}_{instance}"), room)
            : null;
    }

    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn switch
    {
        OctorokRockSpawn rock => CreateRock(rock, room),
        GelSpawn gel => CreateGel(gel, room),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        ItemDropSpawn drop => CreateItemDrop(drop, room),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
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

    private static bool StartsActive(int subId)
    {
        // Bit 7 means always active. Subid $01 is active until the Maku Tree
        // is saved. Ordinary subid $00 portals normally wait for the Tune of
        // Echoes; until harp playback exists, exposed `$d7 markers use the
        // deterministic active fallback so they are usable instead of inert.
        int type = subId & 0x0f;
        return (subId & 0x80) != 0 || type is 0 or 1;
    }

    private IRoomEntity CreateRock(OctorokRockSpawn spawn, OracleRoomData room)
    {
        var rock = new OctorokRockProjectile { Name = "OctorokRock", ZIndex = 10 };
        rock.Initialize(enemies.OctorokProjectile, room, spawn.Position, spawn.Angle);
        return new OctorokRockRoomEntity(rock);
    }

    private IRoomEntity CreateGel(GelSpawn spawn, OracleRoomData room)
    {
        var gel = new GelCharacter { Name = spawn.Name, ZIndex = 10 };
        gel.Initialize(enemies.Gel, room, spawn.Position, random);
        return new GelRoomEntity(gel);
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

    private IRoomEntity CreateDeathPuff(EnemyDeathPuffSpawn spawn)
    {
        var puff = new EnemyDeathPuffEffect { Name = "EnemyDeathPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position, spawn.HighKnockback, spawn.EnemyId);
        return new DeathPuffRoomEntity(puff, itemDrops, random);
    }

    private static IRoomEntity CreateKillPuff(KillEnemyPuffSpawn spawn)
    {
        var puff = new KillEnemyPuffEffect { Name = "KillEnemyPuff", ZIndex = 10 };
        puff.Initialize(spawn.Position);
        return new KillPuffRoomEntity(puff);
    }

    private IRoomEntity CreateItemDrop(ItemDropSpawn spawn, OracleRoomData room)
    {
        var drop = new ItemDropEffect { Name = $"ItemDrop_{spawn.SubId:x2}", ZIndex = 10 };
        drop.Initialize(spawn.SubId, spawn.Position, room, itemDrops.GetVisual(spawn.SubId));
        return new ItemDropRoomEntity(drop);
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
