using Godot;
using System;
using System.Collections.Generic;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>
/// Address-independent bridge between oracles-disasm's expanded assets and the
/// Godot runtime. Rooms and tilesets are decoded on demand, then cached.
/// </summary>
public sealed class OracleWorldData
{
    private const int TilesetRecordSize = 8;

    private readonly byte[] _tilesetMetadata;
    private readonly Dictionary<int, byte[]> _groupTilesets = new();
    private readonly Dictionary<(int Group, int Room, int DataGroup), OracleRoomData> _rooms = new();
    private readonly Dictionary<int, Image> _graphics = new();
    private readonly Dictionary<int, byte[]> _mappings = new();
    private readonly Dictionary<int, byte[]> _collisions = new();
    private readonly Dictionary<int, Color[,]> _palettes = new();
    private readonly Image _hudGraphics;
    private readonly OracleAnimationData _animations;
    private OracleRoomData? _currentPaletteRoom;
    private OracleRoomData? _loadingPaletteRoom;

    public int CachedRoomCount => _rooms.Count;
    internal BackgroundPaletteState BackgroundPalettes { get; }

    public OracleWorldData()
    {
        _tilesetMetadata = ReadBytes("res://assets/oracle/metadata/tilesets.bin", 128 * TilesetRecordSize);
        Color[] commonBgPalette0 = LoadFourColorPalette(
            "res://assets/oracle/metadata/commonBgPalette0.bin");
        Color[,] textboxBgPalette1 = LoadPaletteSet(
            "res://assets/oracle/metadata/textboxBgPalette1.bin", 3);
        BackgroundPalettes = new BackgroundPaletteState(
            commonBgPalette0, textboxBgPalette1);
        BackgroundPalettes.Changed += RedrawLivePaletteRooms;
        _hudGraphics = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/gfx_hud.png");
        _animations = new OracleAnimationData();
    }

    public bool HasRoom(int group, int room)
    {
        if (group < 0 || group > 7 || room < 0 || room > 0xff)
            return false;

        int tileset = GetTilesetId(group, room);
        if (_tilesetMetadata[tileset * TilesetRecordSize + 3] == 0)
            return false;

        int layoutGroup = _tilesetMetadata[tileset * TilesetRecordSize + 1];
        return Godot.FileAccess.FileExists(GetRoomPath(layoutGroup, room));
    }

    public OracleRoomData LoadRoom(int group, int room)
    {
        return LoadRoom(group, room, group);
    }

    public OracleRoomData LoadRoom(int group, int room, int dataGroup)
    {
        var key = (group, room, dataGroup);
        if (!HasRoom(dataGroup, room))
            throw new InvalidOperationException($"Room {group:x1}:{room:x2} is not available.");

        int tileset = GetTilesetId(dataGroup, room);
        if (_rooms.TryGetValue(key, out OracleRoomData? cached))
        {
            if (cached.TilesetId == tileset)
            {
                _loadingPaletteRoom = cached;
                cached.LoadTilesetPalette();
                return cached;
            }
            _rooms.Remove(key);
        }

        int metadataOffset = tileset * TilesetRecordSize;
        int layoutGroup = _tilesetMetadata[metadataOffset + 1];
        int animationGroup = _tilesetMetadata[metadataOffset + 4];
        int activeCollisions = _tilesetMetadata[metadataOffset + 6];
        byte tilesetFlags = _tilesetMetadata[metadataOffset + 7];
        string roomPath = GetRoomPath(layoutGroup, room);

        if (!_graphics.TryGetValue(tileset, out Image? graphics))
        {
            graphics = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/gfx_tileset{tileset:x2}.png");
            _graphics.Add(tileset, graphics);
        }
        // expandedTilesetMappingsTable is indexed by tileset ID. The original
        // shared layout index retained in tilesets.s is not the lookup key for
        // tileset_layouts_expanded.
        if (!_mappings.TryGetValue(tileset, out byte[]? mappings))
        {
            mappings = ReadBytes($"res://assets/oracle/layouts/tilesetMappings{tileset:x2}.bin", 2048);
            _mappings.Add(tileset, mappings);
        }
        if (!_collisions.TryGetValue(tileset, out byte[]? collisions))
        {
            collisions = ReadBytes($"res://assets/oracle/layouts/tilesetCollisions{tileset:x2}.bin", 256);
            _collisions.Add(tileset, collisions);
        }
        if (!_palettes.TryGetValue(tileset, out Color[,]? palette))
        {
            palette = LoadPalette(tileset);
            _palettes.Add(tileset, palette);
        }

        byte[] layout = Godot.FileAccess.GetFileAsBytes(roomPath);
        var result = new OracleRoomData(
            group, room, tileset, animationGroup, activeCollisions, tilesetFlags,
            layout, collisions,
            graphics, _hudGraphics, mappings, palette, BackgroundPalettes,
            _animations);
        _rooms.Add(key, result);
        _loadingPaletteRoom = result;
        result.LoadTilesetPalette();
        return result;
    }

    public int GetTilesetId(int group, int room)
    {
        if (!_groupTilesets.TryGetValue(group, out byte[]? roomTilesets))
        {
            roomTilesets = ReadBytes($"res://assets/oracle/groups/group{group}Tilesets.bin", 256);
            _groupTilesets.Add(group, roomTilesets);
        }
        return roomTilesets[room] & 0x7f;
    }

    internal void SetCurrentPaletteRoom(OracleRoomData room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _currentPaletteRoom = room;
        _loadingPaletteRoom = null;
        room.RedrawForPaletteChange();
    }

    public int GetDungeonIndex(int group, int room)
    {
        int tileset = GetTilesetId(group, room);
        int dungeon = _tilesetMetadata[tileset * TilesetRecordSize + 5];
        return dungeon == 0xff ? -1 : dungeon;
    }

    public void ValidateRepresentativeRooms()
    {
        for (int group = 0; group <= 7; group++)
        {
            for (int room = 0; room <= 0xff; room++)
            {
                if (!HasRoom(group, room))
                    continue;
                OracleRoomData loaded = LoadRoom(group, room);
                GD.Print($"Validated group {group}, room {room:x2}, tileset {loaded.TilesetId:x2}, " +
                    $"layout {loaded.WidthInTiles}x{loaded.HeightInTiles}");
                break;
            }
        }
    }

    private Color[,] LoadPalette(int tileset)
    {
        byte[] values = ReadBytes($"res://assets/oracle/metadata/palette{tileset:x2}.bin", 72);
        var result = new Color[6, 4];
        for (int palette = 0; palette < 6; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            byte r = (byte)Mathf.RoundToInt(values[offset] * 255.0f / 31.0f);
            byte g = (byte)Mathf.RoundToInt(
                values[offset + 1] * 255.0f / 31.0f);
            byte b = (byte)Mathf.RoundToInt(
                values[offset + 2] * 255.0f / 31.0f);
            result[palette, shade] = Color.Color8(r, g, b);
        }
        return result;
    }

    private static Color[] LoadFourColorPalette(string path)
    {
        byte[] values = ReadBytes(path, 12);
        var result = new Color[4];
        for (int shade = 0; shade < result.Length; shade++)
        {
            int offset = shade * 3;
            byte r = (byte)Mathf.RoundToInt(values[offset] * 255.0f / 31.0f);
            byte g = (byte)Mathf.RoundToInt(
                values[offset + 1] * 255.0f / 31.0f);
            byte b = (byte)Mathf.RoundToInt(
                values[offset + 2] * 255.0f / 31.0f);
            result[shade] = Color.Color8(r, g, b);
        }
        return result;
    }

    private static Color[,] LoadPaletteSet(string path, int paletteCount)
    {
        byte[] values = ReadBytes(
            path, paletteCount * BackgroundPaletteState.ColorsPerPalette * 3);
        var result = new Color[
            paletteCount, BackgroundPaletteState.ColorsPerPalette];
        for (int palette = 0; palette < paletteCount; palette++)
        for (int shade = 0;
             shade < BackgroundPaletteState.ColorsPerPalette;
             shade++)
        {
            int offset =
                (palette * BackgroundPaletteState.ColorsPerPalette + shade) * 3;
            result[palette, shade] = new Color(
                values[offset] / 31.0f,
                values[offset + 1] / 31.0f,
                values[offset + 2] / 31.0f);
        }
        return result;
    }

    private void RedrawLivePaletteRooms()
    {
        _currentPaletteRoom?.RedrawForPaletteChange();
        if (_loadingPaletteRoom is not null &&
            _loadingPaletteRoom != _currentPaletteRoom)
        {
            _loadingPaletteRoom.RedrawForPaletteChange();
        }
    }

    private static string GetRoomPath(int layoutGroup, int room)
    {
        string size = layoutGroup < 4 ? "small" : "large";
        return $"res://assets/oracle/rooms/{size}/room{layoutGroup:x2}{room:x2}.bin";
    }
}
