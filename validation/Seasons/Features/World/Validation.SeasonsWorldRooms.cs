using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSeasonsWorldRooms()
    {
        var rom = ValidationRom.LoadCleanUs(OracleGame.Seasons);
        int Byte(int address) => rom.Span[address];
        int Word(int address) => Byte(address) | Byte(address + 1) << 8;
        var world = new SeasonsWorldData();
        int admitted = 0, custom = 0;
        foreach (int group in new[] { 6, 7 })
        for (int room = 0; room < 256; room++)
        for (int season = 0; season < 4; season++)
        {
            // Native group6/7 assignments alias group4/5 in bank $04.
            int assignment = Byte(0x1154c + (group - 6) * 256 + room);
            int tileset = assignment & 0x7f, header = 0x10c84 + tileset * 8;
            if (Byte(header) == 0xff) header = 0x10000 + (Word(header + 1) & 0x3fff) + season * 8;
            bool sideview = (Byte(header + 1) & 0x20) != 0;
            // tileset $62 uses GFXH_DRAGON_ONOX's custom WRAM tilemaps.
            bool unsupported = sideview && tileset == 0x62;
            string context = $"Seasons room ${group:x1}:{room:x2}, season ${season:x2}";
            FailIf(world.HasRoom(group, room) != (sideview && !unsupported),
                context + ": admission differs from the native tileset flags and supported renderer contract.");
            if (!sideview) continue;
            if (unsupported)
            {
                custom++;
                bool rejected = false;
                try { world.LoadRoom(room, season, group); }
                catch (NotSupportedException error)
                {
                    rejected = error.Message.Contains("gfxHeaders.s:GFXH_DRAGON_ONOX", StringComparison.Ordinal) &&
                        error.Message.Contains("w6DragonOnoxTileMap1", StringComparison.Ordinal);
                }
                FailIf(!rejected, context + ": custom WRAM graphics must retain an explicit source-aware rejection.");
                continue;
            }
            admitted++;
            var loaded = world.LoadRoom(room, season, group);
            int dungeon = Byte(header) & 15;
            FailIf(loaded.Group != group || loaded.Id != room || loaded.TilesetId != tileset ||
                loaded.TilesetFlags != Byte(header + 1) || loaded.ActiveCollisions != Byte(header) >> 4 ||
                loaded.LoadsUniqueGraphicsAfterScroll != ((assignment & 0x80) != 0) ||
                world.GetDungeonIndex(group, room) != (dungeon == 15 ? -1 : dungeon) ||
                loaded.Width != 240 || loaded.Height != 176 || loaded.Texture.GetSize() != new Vector2(240, 176),
                context + ": native group alias, graphics, collision, dungeon or large-room geometry differs.");
        }
        FailIf(admitted != 136 || custom != 4,
            "Seasons: preserve all 34 admitted side-view rooms and the custom-renderer room across four season inputs.");
        for (int room = 0; room < 256; room++)
        for (int season = 0; season < 4; season++)
        {
            int assignment = Byte(0x1144c + room), tileset = assignment & 0x7f;
            int header = 0x10c84 + tileset * 8;
            if (Byte(header) == 0xff) header = 0x10000 + (Word(header + 1) & 0x3fff) + season * 8;
            var loaded = world.LoadRoom(room, season, 1);
            FailIf(!world.HasRoom(1, room) || loaded.Group != 1 || loaded.Id != room || loaded.TilesetId != tileset ||
                loaded.TilesetFlags != Byte(header + 1) || loaded.ActiveCollisions != Byte(header) >> 4 ||
                loaded.Width != 160 || loaded.Height != 128 || loaded.Texture.GetSize() != new Vector2(160, 128),
                $"Seasons Subrosia room $1:{room:x2}, season ${season:x2}: native assignment, flags, graphics or geometry differs.");
        }
    }
}
