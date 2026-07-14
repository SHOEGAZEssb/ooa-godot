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
    private const int PopupFullyOpenSize = 4;
    internal const byte LocationArrowAttributes = 0x47;

    // mapIconOamTable (Ages). Each entry is the two 8x16 sprites drawn inside
    // the expanding popup border.
    private static readonly MapIcon[] MapIcons =
    {
        default,
        new(0x22, 0x22, 5, true), new(0x34, 0x34, 3, true),
        new(0x32, 0x32, 3, true), new(0x28, 0x2a, 3),
        new(0x36, 0x36, 1, true), new(0x44, 0x46, 2),
        new(0x2c, 0x2e, 3), new(0x20, 0x20, 3, true),
        new(0x26, 0x26, 4, true), new(0x30, 0x30, 3, true, true),
        new(0x38, 0x38, 3, true), new(0x24, 0x24, 3, true),
        new(0x40, 0x42, 0), new(0x54, 0x56, 6),
        new(0x4c, 0x4e, 1), new(0x50, 0x52, 1),
        new(0x3a, 0x3a, 1, true), new(0x3c, 0x3c, 1, true),
        new(0x3e, 0x3e, 1, true), default,
        new(0x58, 0x5a, 4), new(0x5c, 0x5e, 4),
        new(0x60, 0x62, 4), new(0x64, 0x66, 4), new(0x68, 0x6a, 4)
    };

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
    private InventoryState _inventory = null!;
    private MapDataDatabase _mapData = null!;
    private Texture2D _background = null!;
    private Image _commonTiles = null!;
    private Image _presentTiles1 = null!;
    private Image _presentTiles2 = null!;
    private Image _pastTiles1 = null!;
    private Image _pastTiles2 = null!;
    private Image _dungeonTiles = null!;
    private Image _spriteTiles = null!;
    private Image _dungeonSpriteTiles = null!;
    private Color[,] _presentPalette = null!;
    private Color[,] _pastPalette = null!;
    private Color[,] _dungeonPalette = null!;
    private Color[,] _spritePalette = null!;
    private Color[,] _dungeonSpritePalette = null!;
    private int _cursorRoom;
    private int _dungeonIndex = -1;
    private int _dungeonFloor;
    private DungeonMapDatabase.DungeonCell _dungeonLinkCell;
    private double _frameCounter;
    private double _popupFrameAccumulator;
    private int _popupState;
    private int _popupTimer;
    private int _popupSize;
    private int _popupAlternate;
    private int _popup1;
    private int _popup2;
    private Vector2 _popupPosition;
    private byte[] _dungeonMapTiles = Array.Empty<byte>();

    public MapMode Mode { get; private set; }
    public int CursorRoom => _cursorRoom;
    public int DisplayedDungeonFloor => _dungeonFloor;
    public bool DebugFastTravel { get; private set; }
    public bool LocationArrowVisible => (((int)_frameCounter >> 5) & 1) == 0;
    internal int PopupSize => _popupSize;
    internal int PopupPrimary => _popup1;

    public override void _Ready()
    {
        _commonTiles = LoadImage("res://assets/oracle/map/tiles_common.png");
        _presentTiles1 = LoadImage("res://assets/oracle/map/tiles_present_1.png");
        _presentTiles2 = LoadImage("res://assets/oracle/map/tiles_present_2.png");
        _pastTiles1 = LoadImage("res://assets/oracle/map/tiles_past_1.png");
        _pastTiles2 = LoadImage("res://assets/oracle/map/tiles_past_2.png");
        _dungeonTiles = LoadImage("res://assets/oracle/map/tiles_dungeon.png");
        _spriteTiles = LoadImage("res://assets/oracle/map/sprites.png");
        _dungeonSpriteTiles = LoadImage(
            "res://assets/oracle/inventory/spr_map_compass_keys_bookofseals.png");
        _presentPalette = LoadPalette("res://assets/oracle/map/palette_present.bin", 8, 0);
        _pastPalette = LoadPalette("res://assets/oracle/map/palette_past.bin", 8, 0);
        _dungeonPalette = LoadPalette("res://assets/oracle/map/palette_dungeon.bin", 4, 2);
        _spritePalette = LoadPalette("res://assets/oracle/map/palette_sprites.bin", 8, 0);
        _dungeonSpritePalette = LoadPalette(
            "res://assets/oracle/inventory/palette_sprites.bin", 6, 0);
    }

    public void Initialize(RoomSession rooms, InventoryState inventory)
    {
        _rooms = rooms;
        _inventory = inventory;
        _mapData = new MapDataDatabase();
    }

    public void Open(bool debugFastTravel = false)
    {
        _frameCounter = 0.0;
        _popupFrameAccumulator = 0.0;
        _popupState = 0;
        _popupTimer = 0;
        _popupSize = 0;
        _popupAlternate = 0;
        _popupPosition = new Vector2(-1, -1);
        DebugFastTravel = debugFastTravel;
        if (debugFastTravel)
        {
            MapMode mode = _rooms.MinimapGroup == 1 ? MapMode.Past : MapMode.Present;
            _cursorRoom = _rooms.MinimapRoom;
            PrepareOverworld(revealAll: true, forcedMode: mode);
            Visible = true;
            QueueRedraw();
            return;
        }

        int dungeon = _rooms.World.GetDungeonIndex(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        if (dungeon >= 0)
            PrepareDungeon(dungeon);
        else
            PrepareOverworld(revealAll: false);
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        DebugFastTravel = false;
        Visible = false;
    }

    public void ToggleDebugWorld()
    {
        if (!DebugFastTravel)
            return;
        MapMode mode = Mode == MapMode.Present ? MapMode.Past : MapMode.Present;
        PrepareOverworld(revealAll: true, forcedMode: mode);
        QueueRedraw();
    }

    public bool TryGetFastTravelTarget(out int group, out int room)
    {
        group = Mode == MapMode.Past ? 1 : 0;
        room = _cursorRoom;
        return DebugFastTravel && Mode != MapMode.Dungeon && _rooms.World.HasRoom(group, room);
    }

    public void Update(double delta)
    {
        if (!Visible)
            return;
        _frameCounter += delta * 60.0;
        _popupFrameAccumulator += delta * 60.0;
        while (_popupFrameAccumulator >= 1.0)
        {
            _popupFrameAccumulator -= 1.0;
            UpdatePopupAnimation();
        }
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
        LoadPopupData();
        QueueRedraw();
    }

    public bool TryGetSelectedAreaText(out MapDataDatabase.MapText text)
    {
        text = default;
        if (Mode == MapMode.Dungeon || DebugFastTravel)
            return false;
        int group = Mode == MapMode.Past ? 1 : 0;
        return _rooms.HasVisited(group, _cursorRoom) &&
            _mapData.TryResolveAreaText(_rooms, group, _cursorRoom, out text);
    }

    public float SelectedMarkerY => OverworldCellPosition(_cursorRoom).Y;

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

    private void PrepareOverworld(bool revealAll, MapMode? forcedMode = null)
    {
        Mode = forcedMode ?? (_rooms.MinimapGroup == 1 ? MapMode.Past : MapMode.Present);
        _dungeonIndex = -1;
        if (!DebugFastTravel)
            _cursorRoom = _rooms.MinimapRoom;
        if ((_cursorRoom & 0x0f) >= OverworldWidth ||
            ((_cursorRoom >> 4) & 0x0f) >= OverworldHeight)
            _cursorRoom = 0x00;
        byte[] map = ReadBytes($"res://assets/oracle/map/map_{Mode.ToString().ToLowerInvariant()}.bin", 576);
        byte[] flags = ReadBytes($"res://assets/oracle/map/flags_{Mode.ToString().ToLowerInvariant()}.bin", 576);
        ApplyOverworldTileSubstitutions(map, flags);
        int group = Mode == MapMode.Past ? 1 : 0;
        for (int y = 0; y < OverworldHeight; y++)
        for (int x = 0; x < OverworldWidth; x++)
        {
            int room = (y << 4) | x;
            if (revealAll || _rooms.HasVisited(group, room))
                continue;
            int offset = (OverworldStartY + y) * TilemapStride + OverworldStartX + x;
            map[offset] = 0x04;
            flags[offset] = 0x0a;
        }
        _background = BuildBackground(map, flags);
        LoadPopupData();
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
        int direction = Math.Sign(floor - _dungeonFloor);
        while (floor >= 0 && floor < info.FloorCount)
        {
            if (CanViewFloor(info, floor))
            {
                _dungeonFloor = floor;
                RebuildDungeonBackground(info);
                QueueRedraw();
                return;
            }
            floor += direction;
        }
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

    private bool CanViewFloor(DungeonMapDatabase.DungeonInfo info, int floor) =>
        _inventory.HasDungeonMap(info.Index) || IsFloorVisited(info, floor) ||
        (_inventory.HasDungeonCompass(info.Index) &&
            (info.CompassFloors & (1 << floor)) != 0);

    private void RebuildDungeonBackground(DungeonMapDatabase.DungeonInfo info)
    {
        byte[] map = ReadBytes("res://assets/oracle/map/map_dungeon.bin", 576);
        byte[] flags = ReadBytes("res://assets/oracle/map/flags_dungeon.bin", 576);
        DrawFloorList(map, flags, info);
        DrawSmallKeyCount(map, flags, info.Index);
        bool canViewFloor = CanViewFloor(info, _dungeonFloor);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int offset = (5 + y) * TilemapStride + 10 + x;
            if (!canViewFloor ||
                !info.TryGetCell(_dungeonFloor, x, y, out DungeonMapDatabase.DungeonCell cell))
            {
                map[offset] = 0xac;
                flags[offset] = 0x00;
                continue;
            }
            byte roomFlags = _rooms.SaveData.GetRoomFlags(info.Group, cell.Room);
            bool visited = _rooms.HasVisited(info.Group, cell.Room);
            bool hidden = cell.Properties is 0x60 or 0x70;
            int compassTile = GetCompassTile(info.Index, cell.Properties, roomFlags);
            if (hidden)
            {
                map[offset] = 0xac;
                flags[offset] = 0x00;
            }
            else if (compassTile != 0)
            {
                map[offset] = (byte)compassTile;
                flags[offset] = compassTile == 0xae ? (byte)0x02 : (byte)0x00;
            }
            else if (visited)
            {
                map[offset] = (byte)(0xb0 + (cell.Properties & 0x0f));
                flags[offset] = 0x05;
            }
            else if (_inventory.HasDungeonMap(info.Index))
            {
                map[offset] = 0xaf;
                flags[offset] = 0x04;
            }
            else
            {
                map[offset] = 0xac;
                flags[offset] = 0x00;
            }
        }
        _dungeonMapTiles = map;
        Image blurb = LoadImage($"res://assets/oracle/map/blurb_{DungeonBlurbs[_dungeonIndex]}.png");
        _background = BuildBackground(map, flags, blurb);
    }

    private void DrawFloorList(byte[] map, byte[] flags, DungeonMapDatabase.DungeonInfo info)
    {
        int listOffset = info.Index < FloorListOffsets.Length ? FloorListOffsets[info.Index] : 0x80;
        int offset = 0xa0 + listOffset;
        for (int floor = info.FloorCount - 1; floor >= 0; floor--)
        {
            if (!CanViewFloor(info, floor))
            {
                offset += TilemapStride;
                continue;
            }
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

    private int GetCompassTile(int dungeon, byte properties, byte roomFlags)
    {
        if (!_inventory.HasDungeonCompass(dungeon))
            return 0;
        int feature = properties & 0x70;
        if (feature == 0x40)
            return 0x83;
        if (feature is 0x20 or 0x30 && (roomFlags & OracleSaveData.RoomFlagItem) == 0)
            return 0xae;
        return 0;
    }

    private void DrawSmallKeyCount(byte[] map, byte[] flags, int dungeon)
    {
        int keys = _inventory.GetDungeonSmallKeys(dungeon);
        if (keys <= 0)
            return;
        map[0x225] = 0x9a;
        map[0x226] = (byte)(0x90 + Math.Min(keys, 9));
        flags[0x225] = flags[0x226] = 0x02;
    }

    internal byte DungeonTileAt(int x, int y) =>
        _dungeonMapTiles[(5 + y) * TilemapStride + 10 + x];

    internal void SelectDungeonFloorForValidation(int floor) => TrySelectDungeonFloor(floor);

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
        DrawPopup();
        Vector2 cursor = OverworldCellPosition(_cursorRoom);
        DrawMapSprite(0x88, 6, cursor + new Vector2(-4, -4));
        DrawMapSprite(0x88, 6, cursor + new Vector2(4, -4), flipX: true);
        if (LocationArrowVisible)
        {
            int currentGroup = Mode == MapMode.Past ? 1 : 0;
            if (_rooms.MinimapGroup == currentGroup)
            {
                Vector2 current = OverworldCellPosition(_rooms.MinimapRoom);
                DrawMapSprite(
                    0x0e,
                    LocationArrowAttributes & 0x07,
                    current + new Vector2(0, -10),
                    (LocationArrowAttributes & 0x20) != 0,
                    (LocationArrowAttributes & 0x40) != 0);
            }
        }

        int portalGroup = _rooms.SaveData.ReadWramByte(0xc63e);
        if (portalGroup == (Mode == MapMode.Past ? 1 : 0))
        {
            int portalRoom = _rooms.SaveData.ReadWramByte(0xc63f);
            int portalFrame = (((int)_frameCounter >> 3) & 0x03) * 2;
            DrawMapSprite(0x18 + portalFrame, 7,
                OverworldCellPosition(portalRoom) + new Vector2(0, -4));
        }
    }

    private void DrawDungeonMarkers()
    {
        DrawDungeonItemSprites();
        Vector2 cell = new(10 * 8 + _dungeonLinkCell.X * 8, 5 * 8 + _dungeonLinkCell.Y * 8);
        if (_dungeonLinkCell.Floor == _dungeonFloor && !LocationArrowVisible)
        {
            DrawMapSprite(0x80, 0, cell + new Vector2(0, -16));
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

        if (_inventory.HasDungeonCompass(_dungeonIndex))
        {
            int bossY = GetDungeonBossSymbolY(info.Index);
            DrawMapSprite(0x82, 5, new Vector2(48, bossY - 16));
        }
        if (CanSelectFloor(info, _dungeonFloor + 1))
            DrawMapSprite(0x86, 5, new Vector2(108, 20));
        if (CanSelectFloor(info, _dungeonFloor - 1))
            DrawMapSprite(0x86, 5, new Vector2(108, 108), flipY: true);
    }

    private void DrawDungeonItemSprites()
    {
        if (_inventory.HasDungeonMap(_dungeonIndex))
        {
            DrawMapSprite(0x00, 3, new Vector2(8, 110));
            DrawMapSprite(0x02, 3, new Vector2(16, 110));
        }
        if (_inventory.HasDungeonCompass(_dungeonIndex))
        {
            DrawMapSprite(0x04, 1, new Vector2(32, 110));
            DrawMapSprite(0x06, 1, new Vector2(40, 110));
        }
        if (_inventory.HasDungeonBossKey(_dungeonIndex))
        {
            DrawMapSprite(0x08, 5, new Vector2(8, 128));
            DrawMapSprite(0x0a, 5, new Vector2(16, 128));
        }
        if (_inventory.GetDungeonSmallKeys(_dungeonIndex) > 0)
            DrawMapSprite(0x0c, 5, new Vector2(32, 128));
    }

    private bool CanSelectFloor(DungeonMapDatabase.DungeonInfo info, int floor)
    {
        int direction = Math.Sign(floor - _dungeonFloor);
        while (floor >= 0 && floor < info.FloorCount)
        {
            if (CanViewFloor(info, floor))
                return true;
            floor += direction;
        }
        return false;
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

    private static int GetDungeonBossSymbolY(int dungeon)
    {
        int[] positions = { 0x00, 0x50, 0x50, 0x58, 0x58, 0x50, 0x00,
            0x50, 0x58, 0x48, 0x48, 0x48, 0x50, 0x00 };
        return dungeon < positions.Length ? positions[dungeon] : 0x00;
    }

    private void ApplyOverworldTileSubstitutions(byte[] map, byte[] flags)
    {
        if (Mode == MapMode.Present)
        {
            int companion = _rooms.SaveData.ReadWramByte(0xc610);
            if (companion is 0x0c or 0x0d)
                CopyMapRectangle(map, flags, 0x068, companion == 0x0c ? 0x075 : 0x078, 3, 3);
            if (_rooms.SaveData.HasRoomFlag(0, 0x13, OracleSaveData.RoomFlagLayoutSwap))
                CopyMapRectangle(map, flags, 0x045, 0x07b, 3, 2);
        }
        if (_rooms.SaveData.HasRoomFlag(1, 0x41, OracleSaveData.RoomFlagLayoutSwap))
            CopyMapRectangle(map, flags, 0x0c3, 0x0d5, 3, 2);
    }

    private static void CopyMapRectangle(byte[] map, byte[] flags,
        int destination, int source, int width, int height)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int from = source + y * TilemapStride + x;
            int to = destination + y * TilemapStride + x;
            map[to] = map[from];
            flags[to] = flags[from];
        }
    }

    private void LoadPopupData()
    {
        if (Mode == MapMode.Dungeon)
            return;
        int group = Mode == MapMode.Past ? 1 : 0;
        int popupByte = _rooms.HasVisited(group, _cursorRoom)
            ? _mapData.GetPopupByte(group, _cursorRoom)
            : 0;
        _popup2 = ResolvePopupType((popupByte >> 4) & 0x0f, group, _cursorRoom);
        _popup1 = ResolvePopupType(popupByte & 0x0f, group, _cursorRoom);
        if (_popup1 == 0)
            _popup1 = _popup2;
        if (_popup2 == 0)
            _popup2 = _popup1;

        Vector2 position = new(
            (_cursorRoom & 0x0f) >= 8 ? 16 : 112,
            (_cursorRoom & 0xf0) >= 0x80 ? 96 : 16);
        if (position != _popupPosition)
        {
            _popupPosition = position;
            _popupState = 0;
        }
    }

    private int ResolvePopupType(int type, int group, int room)
    {
        switch (type)
        {
            case 0:
                return 0;
            case 1:
            case 2:
            case 3:
            case 5:
            case 6:
                return type;
            case 4:
                if (group == 1)
                    return 0x0b;
                return _rooms.SaveData.HasRoomFlag(0, 0x38, OracleSaveData.RoomFlagLayoutSwap)
                    ? 0x07 : 0x04;
            case 7:
                return room == 0x5d ? 0x0c : 0x0d;
            case 8:
                return _mapData.TryResolveAreaText(_rooms, group, room, out var text) &&
                    (text.TextId >> 8) == 0x02 ? 0x08 : 0;
            case 9:
                // Gasha planting/growth is not yet an active gameplay system.
                return 0;
            case 0x0a:
                return _rooms.SaveData.HasRoomFlag(
                    group, room, OracleSaveData.RoomFlagPortalSpotDiscovered) ? 0x0a : 0;
            case 0x0b:
                return _rooms.HasVisited(1, 0xfe) ? 0x0e : 0;
            case 0x0c:
                return 0x0e;
            case 0x0d:
                return _rooms.SaveData.HasGlobalFlag(0x1a) ? 0x10 : 0x0f;
            case 0x0e:
                if (_rooms.SaveData.HasRoomFlag(0, 0x90, OracleSaveData.RoomFlag40))
                    return 0x13;
                return _rooms.SaveData.HasRoomFlag(0, 0xba, OracleSaveData.RoomFlag40)
                    ? 0x12 : 0x11;
            case 0x0f:
                if (group == 0 && room == 0xac &&
                    !_rooms.SaveData.HasRoomFlag(0, 0xac, OracleSaveData.RoomFlag80))
                    return 0;
                return _mapData.GetTreePopup(group, room);
            default:
                return 0;
        }
    }

    private void UpdatePopupAnimation()
    {
        bool popupExists = (_popup1 | _popup2) != 0;
        switch (_popupState)
        {
            case 0:
                if (!popupExists)
                {
                    ResetPopupAnimation();
                    return;
                }
                _popupState = 1;
                _popupSize = 1;
                _popupTimer = 2;
                return;
            case 1:
                if (!popupExists)
                {
                    _popupState = 3;
                    _popupTimer = 1;
                    return;
                }
                if (--_popupTimer > 0)
                    return;
                _popupTimer = 2;
                _popupSize++;
                if (_popupSize >= PopupFullyOpenSize)
                {
                    _popupSize = PopupFullyOpenSize;
                    _popupState = 2;
                    _popupTimer = 23;
                }
                return;
            case 2:
                if (!popupExists)
                {
                    _popupState = 3;
                    _popupTimer = 1;
                    return;
                }
                if (--_popupTimer > 0)
                    return;
                _popupTimer = 24;
                _popupAlternate ^= 1;
                return;
            case 3:
                if (--_popupTimer > 0)
                    return;
                _popupTimer = 2;
                if (--_popupSize <= 0)
                    ResetPopupAnimation();
                return;
        }
    }

    private void ResetPopupAnimation()
    {
        _popupState = 0;
        _popupTimer = 0;
        _popupSize = 0;
        _popupAlternate = 0;
    }

    private void DrawPopup()
    {
        if (_popupSize <= 0)
            return;

        // maupMenu_drawPopup appends the contents to OAM before the border.
        // Earlier OAM entries win sprite overlap on the GBC, while Godot's
        // later draw calls win, so paint the border first here.
        DrawPopupBorder();
        if (_popupSize == PopupFullyOpenSize)
        {
            int iconIndex = _popupAlternate == 0 ? _popup1 : _popup2;
            if (iconIndex > 0 && iconIndex < MapIcons.Length)
            {
                MapIcon icon = MapIcons[iconIndex];
                DrawMapSprite(icon.LeftTile, icon.Palette, _popupPosition + new Vector2(8, 8));
                DrawMapSprite(icon.RightTile, icon.Palette, _popupPosition + new Vector2(16, 8),
                    icon.RightFlipX, icon.RightFlipY);
            }
        }
    }

    private void DrawPopupBorder()
    {
        if (_popupSize == 1)
        {
            DrawMapSprite(0x00, 6, _popupPosition + new Vector2(12, 8));
            return;
        }
        if (_popupSize == 2)
        {
            DrawMapSprite(0x02, 6, _popupPosition + new Vector2(8, 8));
            DrawMapSprite(0x02, 6, _popupPosition + new Vector2(16, 8), flipX: true);
            return;
        }
        int corner = _popupSize == 3 ? 0x04 : 0x08;
        int edge = _popupSize == 3 ? 0x06 : 0x0a;
        DrawMapSprite(corner, 6, _popupPosition);
        DrawMapSprite(edge, 6, _popupPosition + new Vector2(8, 0));
        DrawMapSprite(edge, 6, _popupPosition + new Vector2(16, 0), flipX: true);
        DrawMapSprite(corner, 6, _popupPosition + new Vector2(24, 0), flipX: true);
        DrawMapSprite(corner, 6, _popupPosition + new Vector2(0, 16), flipY: true);
        DrawMapSprite(edge, 6, _popupPosition + new Vector2(8, 16), flipY: true);
        DrawMapSprite(edge, 6, _popupPosition + new Vector2(16, 16), true, true);
        DrawMapSprite(corner, 6, _popupPosition + new Vector2(24, 16), true, true);
    }

    private void DrawMapSprite(int tile, int palette, Vector2 position,
        bool flipX = false, bool flipY = false)
    {
        Image source;
        int sourceTile;
        bool deinterleavedSprite;
        bool invertedGrayscale;
        if (tile >= 0x80)
        {
            source = _dungeonTiles;
            sourceTile = tile - 0x80;
            deinterleavedSprite = false;
            invertedGrayscale = false;
        }
        else
        {
            source = Mode == MapMode.Dungeon ? _dungeonSpriteTiles : _spriteTiles;
            sourceTile = tile;
            deinterleavedSprite = true;
            invertedGrayscale = UsesInvertedSpriteGrayscale(Mode, tile);
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
            int shade = GetSpriteShade(sourceColor, invertedGrayscale);
            if (shade == 0)
                continue;
            Color[,] paletteData = Mode == MapMode.Dungeon
                ? _dungeonSpritePalette
                : _spritePalette;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                paletteData[palette, shade]);
        }
    }

    // spr_minimap_icons.properties overrides the spr_ default with
    // "invert: false", so it uses white for color 0 just like the dungeon
    // gfx sheet. Only the dungeon item sprite sheet retains the spr_ default
    // where black is color 0.
    internal static bool UsesInvertedSpriteGrayscale(MapMode mode, int tile) =>
        mode == MapMode.Dungeon && tile < 0x80;

    private static int GetSpriteShade(Color sourceColor, bool invertedGrayscale) =>
        Mathf.Clamp(Mathf.RoundToInt(
            (invertedGrayscale ? sourceColor.R : 1.0f - sourceColor.R) * 3.0f), 0, 3);

    internal static int GetSpriteShadeForValidation(
        Color sourceColor, bool invertedGrayscale) =>
        GetSpriteShade(sourceColor, invertedGrayscale);

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

    private readonly record struct MapIcon(
        int LeftTile,
        int RightTile,
        int Palette,
        bool RightFlipX = false,
        bool RightFlipY = false);
}
