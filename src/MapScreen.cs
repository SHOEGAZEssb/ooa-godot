using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Renders MENU_MAP from the original 20x18 tilemaps, attributes, graphics,
/// palettes, and 8x8 dungeon floor layouts.
/// </summary>
public partial class MapScreen : Node2D
{
    public enum MapMode { Present, Past, Dungeon }

    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int ScreenRows = 18;
    private const int OverworldWidth = 14;
    private const int OverworldHeight = 14;
    private const int OverworldStartX = 3;
    private const int OverworldStartY = 2;

    private static readonly string[] DungeonBlurbs =
    {
        "makupath", "d1", "d2", "d3", "d4", "d5", "d6", "d7", "d8",
        "blacktowerturret", "roomofrites", "heroscave", "d6", "makupath",
        "makupath", "makupath"
    };

    private static readonly byte[] FloorListOffsets =
    {
        0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x60, 0x40, 0x60, 0x60, 0x60, 0x80, 0x80
    };

    private RoomSession _rooms = null!;
    private Texture2D _background = null!;
    private Image _commonTiles = null!;
    private Image _presentTiles1 = null!;
    private Image _presentTiles2 = null!;
    private Image _pastTiles1 = null!;
    private Image _pastTiles2 = null!;
    private Image _dungeonTiles = null!;
    private Image _spriteTiles = null!;
    private Color[,] _presentPalette = null!;
    private Color[,] _pastPalette = null!;
    private Color[,] _dungeonPalette = null!;
    private Color[,] _spritePalette = null!;
    private int _cursorRoom;
    private int _dungeonIndex = -1;
    private int _dungeonFloor;
    private DungeonMapDatabase.DungeonCell _dungeonLinkCell;
    private double _frameCounter;

    public MapMode Mode { get; private set; }
    public int CursorRoom => _cursorRoom;
    public int DisplayedDungeonFloor => _dungeonFloor;
    public bool LocationArrowVisible => (((int)_frameCounter >> 5) & 1) == 0;

    public override void _Ready()
    {
        _commonTiles = LoadImage("res://assets/oracle/map/tiles_common.png");
        _presentTiles1 = LoadImage("res://assets/oracle/map/tiles_present_1.png");
        _presentTiles2 = LoadImage("res://assets/oracle/map/tiles_present_2.png");
        _pastTiles1 = LoadImage("res://assets/oracle/map/tiles_past_1.png");
        _pastTiles2 = LoadImage("res://assets/oracle/map/tiles_past_2.png");
        _dungeonTiles = LoadImage("res://assets/oracle/map/tiles_dungeon.png");
        _spriteTiles = LoadImage("res://assets/oracle/map/sprites.png");
        _presentPalette = LoadPalette("res://assets/oracle/map/palette_present.bin", 8, 0);
        _pastPalette = LoadPalette("res://assets/oracle/map/palette_past.bin", 8, 0);
        _dungeonPalette = LoadPalette("res://assets/oracle/map/palette_dungeon.bin", 4, 2);
        _spritePalette = LoadPalette("res://assets/oracle/map/palette_sprites.bin", 8, 0);
    }

    public void Initialize(RoomSession rooms)
    {
        _rooms = rooms;
    }

    public void Open()
    {
        _frameCounter = 0.0;
        int dungeon = _rooms.World.GetDungeonIndex(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        if (dungeon >= 0)
            PrepareDungeon(dungeon);
        else
            PrepareOverworld();
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
    }

    public void Update(double delta)
    {
        if (!Visible)
            return;
        _frameCounter += delta * 60.0;
        QueueRedraw();
    }

    public void HandleDirectionInput()
    {
        if (Mode == MapMode.Dungeon)
        {
            if (Input.IsActionJustPressed("move_up"))
                TrySelectDungeonFloor(_dungeonFloor + 1);
            else if (Input.IsActionJustPressed("move_down"))
                TrySelectDungeonFloor(_dungeonFloor - 1);
            return;
        }

        if (Input.IsActionJustPressed("move_right"))
            MoveOverworldCursor(Vector2I.Right);
        else if (Input.IsActionJustPressed("move_left"))
            MoveOverworldCursor(Vector2I.Left);
        else if (Input.IsActionJustPressed("move_up"))
            MoveOverworldCursor(Vector2I.Up);
        else if (Input.IsActionJustPressed("move_down"))
            MoveOverworldCursor(Vector2I.Down);
    }

    internal void MoveOverworldCursor(Vector2I direction)
    {
        if (Mode == MapMode.Dungeon)
            return;
        int x = _cursorRoom & 0x0f;
        int y = (_cursorRoom >> 4) & 0x0f;
        x = (x + direction.X + OverworldWidth) % OverworldWidth;
        y = (y + direction.Y + OverworldHeight) % OverworldHeight;
        _cursorRoom = (y << 4) | x;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _background == null)
            return;
        DrawTexture(_background, Vector2.Zero);
        if (Mode == MapMode.Dungeon)
            DrawDungeonMarkers();
        else
            DrawOverworldMarkers();
    }

    private void PrepareOverworld()
    {
        Mode = _rooms.MinimapGroup == 1 ? MapMode.Past : MapMode.Present;
        _dungeonIndex = -1;
        _cursorRoom = _rooms.MinimapRoom;
        byte[] map = ReadBytes($"res://assets/oracle/map/map_{Mode.ToString().ToLowerInvariant()}.bin", 576);
        byte[] flags = ReadBytes($"res://assets/oracle/map/flags_{Mode.ToString().ToLowerInvariant()}.bin", 576);
        int group = Mode == MapMode.Past ? 1 : 0;
        for (int y = 0; y < OverworldHeight; y++)
        for (int x = 0; x < OverworldWidth; x++)
        {
            int room = (y << 4) | x;
            if (_rooms.HasVisited(group, room))
                continue;
            int offset = (OverworldStartY + y) * TilemapStride + OverworldStartX + x;
            map[offset] = 0x04;
            flags[offset] = 0x0a;
        }
        _background = BuildBackground(map, flags);
    }

    private void PrepareDungeon(int dungeon)
    {
        Mode = MapMode.Dungeon;
        _dungeonIndex = dungeon;
        DungeonMapDatabase.DungeonInfo info = _rooms.DungeonMaps.GetDungeon(dungeon);
        if (!info.TryGetRoom(_rooms.CurrentRoom.Id, out _dungeonLinkCell))
            throw new InvalidOperationException(
                $"Dungeon {dungeon:x2} does not place room {_rooms.CurrentRoom.Id:x2} on its floor map.");
        _dungeonFloor = _dungeonLinkCell.Floor;
        _cursorRoom = _rooms.CurrentRoom.Id;
        RebuildDungeonBackground(info);
    }

    private void TrySelectDungeonFloor(int floor)
    {
        DungeonMapDatabase.DungeonInfo info = _rooms.DungeonMaps.GetDungeon(_dungeonIndex);
        if (floor < 0 || floor >= info.FloorCount || !IsFloorVisited(info, floor))
            return;
        _dungeonFloor = floor;
        RebuildDungeonBackground(info);
        QueueRedraw();
    }

    private bool IsFloorVisited(DungeonMapDatabase.DungeonInfo info, int floor)
    {
        foreach (DungeonMapDatabase.DungeonCell cell in info.Cells)
        {
            if (cell.Floor == floor && _rooms.HasVisited(info.Group, cell.Room))
                return true;
        }
        return false;
    }

    private void RebuildDungeonBackground(DungeonMapDatabase.DungeonInfo info)
    {
        byte[] map = ReadBytes("res://assets/oracle/map/map_dungeon.bin", 576);
        byte[] flags = ReadBytes("res://assets/oracle/map/flags_dungeon.bin", 576);
        DrawFloorList(map, flags, info);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int offset = (5 + y) * TilemapStride + 10 + x;
            if (!info.TryGetCell(_dungeonFloor, x, y, out DungeonMapDatabase.DungeonCell cell))
            {
                map[offset] = 0xac;
                flags[offset] = 0x00;
                continue;
            }
            bool visited = _rooms.HasVisited(info.Group, cell.Room);
            bool hidden = cell.Properties is 0x60 or 0x70;
            map[offset] = visited && !hidden
                ? (byte)(0xb0 + (cell.Properties & 0x0f))
                : (byte)0xac;
            flags[offset] = visited && !hidden ? (byte)0x05 : (byte)0x00;
        }
        Image blurb = LoadImage($"res://assets/oracle/map/blurb_{DungeonBlurbs[_dungeonIndex]}.png");
        _background = BuildBackground(map, flags, blurb);
    }

    private static void DrawFloorList(byte[] map, byte[] flags, DungeonMapDatabase.DungeonInfo info)
    {
        int listOffset = info.Index < FloorListOffsets.Length ? FloorListOffsets[info.Index] : 0x80;
        int offset = 0xa0 + listOffset;
        for (int floor = info.FloorCount - 1; floor >= 0; floor--)
        {
            int name = Mathf.Clamp(info.BaseFloor + floor, 0, 10);
            byte first = name < 3 ? (byte)0x9b : (byte)0x80;
            byte second = (byte)(name < 3 ? 0x93 - name : 0x91 + name - 3);
            map[offset] = first;
            map[offset + 1] = second;
            map[offset + 2] = 0x9c;
            map[offset + 4] = 0xaa;
            map[offset + 5] = 0xab;
            flags[offset] = flags[offset + 1] = flags[offset + 2] = 0x02;
            flags[offset + 4] = flags[offset + 5] = 0x04;
            offset += TilemapStride;
        }
    }

    private Texture2D BuildBackground(byte[] map, byte[] flags, Image? blurb = null)
    {
        Image output = Image.CreateEmpty(160, 144, false, Image.Format.Rgba8);
        Color[,] palette = Mode switch
        {
            MapMode.Present => _presentPalette,
            MapMode.Past => _pastPalette,
            _ => _dungeonPalette
        };
        for (int row = 0; row < ScreenRows; row++)
        for (int column = 0; column < ScreenColumns; column++)
        {
            int offset = row * TilemapStride + column;
            DrawTile(output, SelectTileSource(map[offset], blurb, out int sourceTile),
                sourceTile, flags[offset], palette, column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Image SelectTileSource(byte tile, Image? blurb, out int sourceTile)
    {
        if (Mode == MapMode.Dungeon)
        {
            if (tile >= 0xc0 && blurb != null)
            {
                sourceTile = tile - 0xc0;
                return blurb;
            }
            sourceTile = tile - 0x80;
            return _dungeonTiles;
        }
        if (tile < 0x60)
        {
            sourceTile = tile;
            return _commonTiles;
        }
        if (tile < 0x80)
        {
            sourceTile = tile - 0x60;
            return Mode == MapMode.Present ? _presentTiles2 : _pastTiles2;
        }
        sourceTile = tile - 0x80;
        return Mode == MapMode.Present ? _presentTiles1 : _pastTiles1;
    }

    private static void DrawTile(Image output, Image source, int sourceTile, byte flags,
        Color[,] palette, int destinationX, int destinationY)
    {
        int tileX = sourceTile % 16 * 8;
        int tileY = sourceTile / 16 * 8;
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = Mathf.Clamp(flags & 0x07, 0, 7);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int readX = tileX + (flipX ? 7 - x : x);
            int readY = tileY + (flipY ? 7 - y : y);
            Color sourceColor = source.GetPixel(readX, readY);
            int shade = Mathf.Clamp(Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
            output.SetPixel(destinationX + x, destinationY + y, palette[paletteIndex, shade]);
        }
    }

    private void DrawOverworldMarkers()
    {
        Vector2 cursor = OverworldCellPosition(_cursorRoom);
        DrawMapSprite(0x88, 6, cursor + new Vector2(-4, -4));
        DrawMapSprite(0x88, 6, cursor + new Vector2(4, -4), flipX: true);
        if (LocationArrowVisible)
        {
            Vector2 current = OverworldCellPosition(_rooms.MinimapRoom);
            DrawMapSprite(0x0e, 7, current + new Vector2(0, -10));
        }
    }

    private void DrawDungeonMarkers()
    {
        Vector2 cell = new(10 * 8 + _dungeonLinkCell.X * 8, 5 * 8 + _dungeonLinkCell.Y * 8);
        if (_dungeonLinkCell.Floor == _dungeonFloor && !LocationArrowVisible)
        {
            DrawMapSprite(0x80, 0, cell + new Vector2(0, -8));
        }
        else if (_dungeonLinkCell.Floor == _dungeonFloor)
        {
            DrawMapSprite(0x88, 4, cell + new Vector2(-4, -4));
            DrawMapSprite(0x88, 4, cell + new Vector2(4, -4), flipX: true);
        }

        DungeonMapDatabase.DungeonInfo info = _rooms.DungeonMaps.GetDungeon(_dungeonIndex);
        int symbolY = GetDungeonSymbolY(info.Index);
        int selectedFloorIndex = info.FloorCount - 1 - _dungeonFloor;
        DrawMapSprite(0x84, 4, new Vector2(22, symbolY + selectedFloorIndex * 8 - 16));
        int linkFloorIndex = info.FloorCount - 1 - _dungeonLinkCell.Floor;
        DrawMapSprite(0x80, 0, new Vector2(36, symbolY + linkFloorIndex * 8 - 16));
    }

    private static Vector2 OverworldCellPosition(int room) => new(
        (OverworldStartX + (room & 0x0f)) * 8,
        (OverworldStartY + ((room >> 4) & 0x0f)) * 8);

    private static int GetDungeonSymbolY(int dungeon)
    {
        int[] positions = { 0x50, 0x50, 0x50, 0x50, 0x50, 0x50, 0x50,
            0x48, 0x40, 0x48, 0x48, 0x48, 0x50, 0x50 };
        return dungeon < positions.Length ? positions[dungeon] : 0x50;
    }

    private void DrawMapSprite(int tile, int palette, Vector2 position,
        bool flipX = false, bool flipY = false)
    {
        Image source;
        int sourceTile;
        bool deinterleavedSprite;
        if (tile >= 0x80)
        {
            source = _dungeonTiles;
            sourceTile = tile - 0x80;
            deinterleavedSprite = false;
        }
        else
        {
            source = _spriteTiles;
            sourceTile = tile;
            deinterleavedSprite = true;
        }
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int spriteY = flipY ? 15 - y : y;
            int readX;
            int readY;
            if (deinterleavedSprite)
            {
                int cell = (sourceTile & 0xfe) / 2;
                readX = cell % 16 * 8 + (flipX ? 7 - x : x);
                readY = cell / 16 * 16 + spriteY;
            }
            else
            {
                int rawTile = (sourceTile & 0xfe) + spriteY / 8;
                readX = rawTile % 16 * 8 + (flipX ? 7 - x : x);
                readY = rawTile / 16 * 8 + spriteY % 8;
            }
            if (readY >= source.GetHeight())
                continue;
            Color sourceColor = source.GetPixel(readX, readY);
            if (sourceColor.R < 0.1f)
                continue;
            int shade = sourceColor.R < 0.5f ? 1 : sourceColor.R < 0.9f ? 2 : 3;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _spritePalette[palette, shade]);
        }
    }

    private static Image LoadImage(string path)
    {
        Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image == null || image.IsEmpty())
            throw new InvalidOperationException($"Could not load map image {path}.");
        return image;
    }

    private static byte[] ReadBytes(string path, int expectedLength)
    {
        byte[] data = FileAccess.GetFileAsBytes(path);
        if (data.Length != expectedLength)
            throw new InvalidOperationException($"{path} should contain {expectedLength} bytes, got {data.Length}.");
        return data;
    }

    private static Color[,] LoadPalette(string path, int count, int firstPalette)
    {
        byte[] bytes = ReadBytes(path, count * 4 * 3);
        var result = new Color[8, 4];
        Color[] fallback = LoadCommonPalette();
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            result[palette, shade] = fallback[shade];
        for (int palette = 0; palette < count; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            result[firstPalette + palette, shade] = GbcColor(
                bytes[offset], bytes[offset + 1], bytes[offset + 2]);
        }
        return result;
    }

    private static Color[] LoadCommonPalette()
    {
        byte[] bytes = ReadBytes("res://assets/oracle/metadata/commonBgPalette0.bin", 12);
        var result = new Color[4];
        for (int shade = 0; shade < 4; shade++)
            result[shade] = GbcColor(bytes[shade * 3], bytes[shade * 3 + 1], bytes[shade * 3 + 2]);
        return result;
    }

    private static Color GbcColor(int r, int g, int b) => new(r / 31.0f, g / 31.0f, b / 31.0f);
}
