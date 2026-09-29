using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>CPU work of source room decoders and tile-buffer construction.</summary>
internal sealed class OracleRoomLoadingWork
{
    internal static OracleRoomLoadingWork Shared { get; } = new();
    private readonly Dictionary<(string Operation, int Index, int Variant), int> _clocks = new();
    private readonly Dictionary<(string Operation, int Index), int> _selectors = new();

    private OracleRoomLoadingWork()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/timing/room_cpu.tsv",
            new GeneratedTableSchema("original room loading CPU work", GeneratedTableKeySemantics.Unique,
                ["operation", "index", "variant", "cpu-cycles", "source"],
                ["operation", "index", "variant"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string operation = row.RequiredString(0);
            if (operation is not ("tileset" or "room" or "collisions" or "vram" or "seed-wrapper" or "seed-entry" or
                "layout-wrapper" or "vram-wrapper" or "scroll-clear" or "graphics-traversal" or "edge-warp" or
                "initialization" or "room-state" or "room-specific" or "vram-specific" or
                "tile-specific" or "single-tiles" or "screen-data" or "tile-dispatch" or
                "standard-tiles" or "pollution-gate" or "opened-chest" or
                "object-freeze" or "object-parse" or "enemy-history" or
                "next-room" or "room-advance" or "room-pack" or "pirate-load" or "portal-spawn"))
                throw new InvalidOperationException($"Unsupported room loading operation {operation}.");
            int index = Convert.ToInt32(row.RequiredString(1), 16);
            int variant = row.Decimal(2, 0, 1023);
            _clocks.Add((operation, index, variant), row.Decimal(3, 1, 10_000_000));
            _selectors[(operation, index)] = _selectors.GetValueOrDefault((operation, index)) | variant;
            _ = row.RequiredString(4);
        }
    }

    internal int Scroll(OracleRoomData source, OracleRoomData destination, OracleSaveData save)
    {
        int clocks = Get("room", destination.LayoutGroup * 256 + destination.Id, 0) +
            Get("collisions", 0, destination.Group < 4 ? 0 : 1) + Get("vram", 0, 0) +
            Get("layout-wrapper", 0, source.TilesetLayoutId != destination.TilesetLayoutId ? 1 : 0) +
            Get("vram-wrapper", 0, 0) + Get("scroll-clear", 0, 0) + Get("graphics-traversal", 0, 0) +
            Get("object-freeze", 0, 0) +
            PirateLoad(destination, save) +
            Get("portal-spawn", 0, save.TimePortalGroup != destination.Group ? 0 :
                save.TimePortalRoom != destination.Id ? 1 : 2) +
            OracleSeaEffectSearchWork.Shared.Search(destination.ActiveCollisions, destination.Layout) +
            Get("initialization", 0, 0) +
            Get("room-specific", destination.Group * 256 + destination.Id, 0) +
            Get("vram-specific", destination.Group * 256 + destination.Id, 0) +
            Get("tile-specific", destination.Group * 256 + destination.Id, 0) +
            ScreenData(destination.Group, destination.Id, save) +
            SingleTiles(destination.Group, destination.Id, save) +
            Get("tile-dispatch", destination.Group, 0) +
            Get("standard-tiles", 0, save.GetRoomFlags(destination.Group, destination.Id) & 0x8f) +
            Get("opened-chest", destination.Group * 256 + destination.Id,
                (save.GetRoomFlags(destination.Group, destination.Id) & 0x20) != 0 ? 1 : 0) +
            Get("pollution-gate", 0, (destination.TilesetFlags & 1) |
                ((destination.TilesetFlags & 0x40) >> 5) |
                (save.HasGlobalFlag(GlobalFlag.WaterPollutionFixed) ? 4 : 0)) +
            Get("room-state", destination.Group,
                ((destination.TilesetFlags & 0x40) != 0 ? 1 : 0) +
                ((save.GetRoomFlags(destination.Group, destination.Id) & OracleSaveData.RoomFlagLayoutSwap) != 0 ? 2 : 0) +
                (destination.IsCompanionRegion ? 4 : 0) +
                (save.ReadWramByte(WramAddress.wAnimalCompanion) != 0 ? 8 : 0));
        // wLoadedTilesetLayout gates decoding; a cached Godot texture does not.
        if (source.TilesetLayoutId != destination.TilesetLayoutId)
            clocks += Tileset(destination.TilesetLayoutId, destination.ActiveCollisions,
                destination.TilesetFlags, destination.Id);
        return clocks;
    }

    internal int Tileset(int layout, int collisions, int flags, int room) =>
        Get("tileset", layout, collisions != 0 ? 0 : (flags & 0x80) == 0 ? 1 : room == 0x38 ? 3 : 2);

    internal int PirateLoad(OracleRoomData room, OracleSaveData save) =>
        Get("pirate-load", 0, (room.TilesetFlags & 1) | ((room.TilesetFlags & 0xc0) >> 5) |
            (save.IsLinkedGame ? 8 : 0) | (save.HasGlobalFlag(GlobalFlag.PiratesGone) ? 16 : 0) |
            (save.ReadWramByte(WramAddress.wPirateShipRoom) == room.Id ? 32 : 0));

    internal int ScreenData(int group, int room, OracleSaveData save)
    {
        int index = group * 256 + room;
        int selector = _selectors[("screen-data", index)];
        int companion = 0;
        if ((selector & 0x0c) != 0)
        {
            int animal = save.ReadWramByte(WramAddress.wAnimalCompanion);
            companion = animal switch { 0 => 0, 0x0b => 1, 0x0c => 2, 0x0d => 3,
                _ => throw new InvalidOperationException($"loadTilesetData: unsupported animal companion ${animal:x2}.") };
        }
        int water = save.ReadWramByte(WramAddress.wJabuWaterLevel) & 7;
        if ((selector & 0x30) != 0 && water > 2)
            throw new InvalidOperationException($"loadTilesetData: unsupported Jabu water level ${water:x2}.");
        int variant = (save.GetRoomFlags(group, room) & OracleSaveData.RoomFlagLayoutSwap) |
            ((save.GetRoomFlags(1, 0x48) & 1) << 1) | (companion << 2) |
            (water << 4);
        return Get("screen-data", index, variant & selector);
    }

    internal int SingleTiles(int group, int room, OracleSaveData save)
    {
        int index = group * 256 + room;
        int variant = save.GetRoomFlags(group, room) | (save.IsLinkedGame ? 0x100 : 0) |
            (save.HasGlobalFlag(GlobalFlag.FinishedGame) ? 0x200 : 0);
        return Get("single-tiles", index, variant & _selectors[("single-tiles", index)]);
    }

    internal int EdgeWarp(int group, int room, bool down, int linkX, bool mounted) =>
        Get("edge-warp", group * 256 + room,
            (mounted ? 4 : 0) + (down ? 2 : 0) + (linkX < (group < 4 ? 0x58 : 0x80) ? 0 : 1));

    internal int Get(string operation, int index, int variant) =>
        _clocks.TryGetValue((operation, index, variant), out int clocks) ? clocks :
        throw new InvalidOperationException($"Missing source room loading work {operation}/${index:x4}/{variant}.");
}
