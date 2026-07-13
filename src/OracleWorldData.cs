using Godot;
using System;
using System.Collections.Generic;

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
    private readonly Dictionary<(int Group, int Room), OracleRoomData> _rooms = new();
    private readonly Dictionary<int, Image> _graphics = new();
    private readonly Dictionary<int, byte[]> _mappings = new();
    private readonly Dictionary<int, byte[]> _collisions = new();
    private readonly Dictionary<int, Color[,]> _palettes = new();
    private readonly Color[] _commonBgPalette0;
    private readonly OracleAnimationData _animations;

    public int CachedRoomCount => _rooms.Count;

    public OracleWorldData()
    {
        _tilesetMetadata = ReadBytes("res://assets/oracle/metadata/tilesets.bin", 128 * TilesetRecordSize);
        _commonBgPalette0 = LoadFourColorPalette(
            "res://assets/oracle/metadata/commonBgPalette0.bin");
        _animations = new OracleAnimationData();
    }

    public bool HasRoom(int group, int room)
    {
        if (group < 0 || group > 5 || room < 0 || room > 0xff)
            return false;

        int tileset = GetTilesetId(group, room);
        if (_tilesetMetadata[tileset * TilesetRecordSize + 3] == 0)
            return false;

        int layoutGroup = _tilesetMetadata[tileset * TilesetRecordSize + 1];
        return Godot.FileAccess.FileExists(GetRoomPath(layoutGroup, room));
    }

    public OracleRoomData LoadRoom(int group, int room)
    {
        var key = (group, room);
        if (!HasRoom(group, room))
            throw new InvalidOperationException($"Room {group:x1}:{room:x2} is not available.");

        int tileset = GetTilesetId(group, room);
        if (_rooms.TryGetValue(key, out OracleRoomData? cached))
        {
            if (cached.TilesetId == tileset)
                return cached;
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
            Texture2D texture = GD.Load<Texture2D>($"res://assets/oracle/gfx/gfx_tileset{tileset:x2}.png");
            graphics = texture.GetImage();
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
            graphics, mappings, palette, _commonBgPalette0, _animations);
        _rooms.Add(key, result);
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

    public int GetDungeonIndex(int group, int room)
    {
        int tileset = GetTilesetId(group, room);
        int dungeon = _tilesetMetadata[tileset * TilesetRecordSize + 5];
        return dungeon == 0xff ? -1 : dungeon;
    }

    public void ValidateRepresentativeRooms()
    {
        for (int group = 0; group <= 5; group++)
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
            byte g = (byte)Mathf.RoundToInt(values[offset + 1] * 255.0f / 31.0f);
            byte b = (byte)Mathf.RoundToInt(values[offset + 2] * 255.0f / 31.0f);
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
            byte g = (byte)Mathf.RoundToInt(values[offset + 1] * 255.0f / 31.0f);
            byte b = (byte)Mathf.RoundToInt(values[offset + 2] * 255.0f / 31.0f);
            result[shade] = Color.Color8(r, g, b);
        }
        return result;
    }

    private static byte[] ReadBytes(string path, int expectedLength)
    {
        byte[] data = Godot.FileAccess.GetFileAsBytes(path);
        if (data.Length != expectedLength)
            throw new InvalidOperationException($"{path} should contain {expectedLength} bytes, got {data.Length}.");
        return data;
    }

    private static string GetRoomPath(int layoutGroup, int room)
    {
        string size = layoutGroup < 4 ? "small" : "large";
        return $"res://assets/oracle/rooms/{size}/room{layoutGroup:x2}{room:x2}.bin";
    }
}
