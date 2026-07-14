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
    Action<TimePortal> portalEntered)
{
    public IEnumerable<IRoomEntity> CreateRoomEntities(int group, OracleRoomData room)
    {
        foreach (NpcDatabase.NpcRecord record in npcs.GetRoomNpcs(group, room.Id))
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = NpcCharacter.BehindLinkZIndex
            };
            npc.Initialize(record);
            yield return new NpcRoomEntity(npc);
        }

        foreach (TimePortalDatabase.PortalRecord record in timePortals.GetRoomPortals(group, room.Id))
        {
            if (!StartsActive(record.SubId))
                continue;
            var portal = new TimePortal { Name = $"TimePortal_{record.SubId:x2}", ZIndex = 8 };
            portal.Initialize(record, room);
            yield return new TimePortalRoomEntity(portal, portalEntered);
        }

        var occupied = new HashSet<int>();
        foreach (EnemyDatabase.EnemyRecord record in enemies.GetRoomKeese(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                if (!TryChooseRandomEnemyPosition(room, record.Flags, occupied, out Vector2 position))
                    continue;
                var keese = new KeeseCharacter { Name = $"Keese_{record.SubId:x2}_{instance}", ZIndex = 10 };
                keese.Initialize(record, room, position, random);
                yield return new KeeseRoomEntity(keese);
            }
        }

        occupied.Clear();
        foreach (EnemyDatabase.OctorokRecord record in enemies.GetRoomOctoroks(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                if (!TryChoosePosition(room, record.FixedPosition, record.X, record.Y,
                    record.Flags, occupied, out Vector2 position))
                    continue;
                var octorok = new OctorokCharacter { Name = $"Octorok_{record.SubId:x2}_{instance}", ZIndex = 10 };
                octorok.Initialize(record, room, position, random);
                yield return new OctorokRoomEntity(octorok);
            }
        }

        occupied.Clear();
        foreach (EnemyDatabase.ZolRecord record in enemies.GetRoomZols(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                if (!TryChoosePosition(room, record.FixedPosition, record.X, record.Y,
                    record.Flags, occupied, out Vector2 position))
                    continue;
                var zol = new ZolCharacter { Name = $"Zol_{record.SubId:x2}_{instance}", ZIndex = 10 };
                zol.Initialize(record, room, position, random);
                yield return new ZolRoomEntity(zol);
            }
        }

        occupied.Clear();
        foreach (EnemyDatabase.GelRecord record in enemies.GetRoomGels(group, room.Id))
        {
            for (int instance = 0; instance < record.Count; instance++)
            {
                if (!TryChoosePosition(room, record.FixedPosition, record.X, record.Y,
                    record.Flags, occupied, out Vector2 position))
                    continue;
                yield return Create(new GelSpawn(position, $"RoomGel_{instance}"), room);
            }
        }
    }

    public IRoomEntity Create(RoomEntitySpawn spawn, OracleRoomData room) => spawn switch
    {
        OctorokRockSpawn rock => CreateRock(rock, room),
        GelSpawn gel => CreateGel(gel, room),
        EnemyDeathPuffSpawn puff => CreateDeathPuff(puff),
        KillEnemyPuffSpawn puff => CreateKillPuff(puff),
        ItemDropSpawn drop => CreateItemDrop(drop, room),
        CutsceneNpcSpawn npc => CreateCutsceneNpc(npc),
        _ => throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Unknown room-entity spawn request.")
    };

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
        return new CutsceneNpcRoomEntity(npc);
    }

    private bool TryChoosePosition(
        OracleRoomData room,
        bool fixedPosition,
        int x,
        int y,
        int flags,
        HashSet<int> occupied,
        out Vector2 position)
    {
        if (fixedPosition)
        {
            position = new Vector2(x, y);
            return true;
        }
        return TryChooseRandomEnemyPosition(room, flags, occupied, out position);
    }

    private bool TryChooseRandomEnemyPosition(
        OracleRoomData room,
        int flags,
        HashSet<int> occupied,
        out Vector2 position)
    {
        for (int attempt = 0; attempt < 0x3f; attempt++)
        {
            int packed = random.NextPlacementValue();
            int tileY = packed >> 4;
            int tileX = packed & 0x0f;
            bool validBoundary = room.Group < 4
                ? tileY < OracleRoomData.ViewportHeight / OracleRoomData.MetatileSize &&
                    tileX < OracleRoomData.ViewportWidth / OracleRoomData.MetatileSize
                : tileY > 0 && tileY < room.HeightInTiles - 1 &&
                    tileX > 0 && tileX < room.WidthInTiles - 1;
            if (!validBoundary || occupied.Contains(packed))
                continue;

            position = new Vector2(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            if ((flags & 0x04) == 0 && room.IsSolid(position))
                continue;
            occupied.Add(packed);
            return true;
        }
        position = Vector2.Zero;
        return false;
    }
}
