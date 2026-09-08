using Godot;
using System;
using System.Collections.Generic;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;

/// <summary>
/// Renders MENU_MAP from the original 20x18 tilemaps, attributes, graphics,
/// palettes, and 8x8 dungeon floor layouts.
/// </summary>
public partial class MapScreen : Node2D
{

    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int ScreenRows = 18;
    private const int OverworldWidth = 14;
    private const int OverworldHeight = 14;
    private const int OverworldStartX = 3;
    private const int OverworldStartY = 2;
    private const int FirstInteriorGroup = 2;
    private const int LastInteriorGroup = 5;
    private const int InteriorGridSize = 16;
    private const int InteriorCellSize = 7;
    private const int InteriorGridLeft = 24;
    private const int InteriorGridTop = 20;
    private const int PopupFullyOpenSize = 4;
    internal const byte LocationArrowAttributes = 0x47;

    private RoomSession _rooms = null!;
    private InventoryState _inventory = null!;
    private MapDataDatabase _mapData = null!;
    private MenuPresentationDatabase _layouts = null!;
    private MapPresentationState _presentation = null!;
    private readonly GashaSpotDatabase _gashaSpots = new();
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
    private int[] _galeRooms = [];
    private int _interiorGroup = FirstInteriorGroup;
    private readonly int[] _interiorCursors = new int[LastInteriorGroup - FirstInteriorGroup + 1];
    private readonly bool[] _interiorRoomAvailable = new bool[0x100];
    private readonly bool[] _interiorRoomDungeon = new bool[0x100];
    private int _dungeonIndex = -1;
    private int _dungeonFloor;
    private int _dungeonScrollY;
    private int _scrollTimer;
    private int _scrollDirection;
    private bool _dungeonFlicker;
    private int _currentMapRoom;
    private DungeonCell _dungeonLinkCell;
    private Func<int> _frameCounterSource = () => (int)(Input.TimingFrame & 0xff);
    private int _frameCounter;
    private double _popupFrameAccumulator;
    private int _popupState;
    private int _popupTimer;
    private int _popupSize;
    private int _popupAlternate;
    private int _popup1;
    private int _popup2;
    private Vector2 _popupPosition;
    private byte[] _dungeonMapTiles = Array.Empty<byte>();
    private readonly List<MapSprite> _sprites = new();

    public MapMode Mode { get; private set; }
    public int CursorRoom => _cursorRoom;
    public int InteriorGroup => _interiorGroup;
    public int DisplayedDungeonFloor => _dungeonFloor;
    public bool DebugFastTravel { get; private set; }
    public bool LocationArrowVisible => ((_frameCounter >> 5) & 1) == 0;
    internal int PopupSize => _popupSize;
    internal int PopupPrimary => _popup1;
    internal int PopupAlternate => _popupAlternate;
    internal int DungeonScrollY => _dungeonScrollY;
    internal bool IsScrolling => _scrollTimer != 0;
    internal ulong PopupIconPixelHashForValidation(int iconIndex)
    {
        if (iconIndex < 0 || iconIndex >= _layouts.MapIcons.Count)
            throw new ArgumentOutOfRangeException(nameof(iconIndex));
        Image output = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        output.Fill(Colors.Transparent);
        MapIconLayout icon = _layouts.MapIcons[iconIndex];
        BlitMapIconPart(output, icon.Left);
        BlitMapIconPart(output, icon.Right);
        return OracleGraphicsCache.PixelHash(output);
    }
    internal ulong BackgroundRegionPixelHashForValidation(Rect2I region)
    {
        Image background = _background.GetImage();
        if (region.Position.X < 0 || region.Position.Y < 0 ||
            region.End.X > background.GetWidth() ||
            region.End.Y > background.GetHeight())
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }
        return OracleGraphicsCache.PixelHash(background.GetRegion(region));
    }
    internal Vector2 DungeonLinkIconPosition => new(
        10 * 8 + _dungeonLinkCell.X * 8,
        5 * 8 + _dungeonLinkCell.Y * 8 - 8);

    public override void _Ready()
    {
        _commonTiles = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_common.png");
        _presentTiles1 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_present_1.png");
        _presentTiles2 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_present_2.png");
        _pastTiles1 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_past_1.png");
        _pastTiles2 = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_past_2.png");
        _dungeonTiles = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/tiles_dungeon.png");
        _spriteTiles = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/map/sprites.png");
        _dungeonSpriteTiles = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/inventory/spr_map_compass_keys_bookofseals.png");
        _presentPalette = LoadPaletteWithCommonBackgroundFallback(
            "res://assets/oracle/map/palette_present.bin", 8, 0);
        _pastPalette = LoadPaletteWithCommonBackgroundFallback(
            "res://assets/oracle/map/palette_past.bin", 8, 0);
        _dungeonPalette = LoadPaletteWithCommonBackgroundFallback(
            "res://assets/oracle/map/palette_dungeon.bin", 4, 2);
        _spritePalette = LoadPaletteWithCommonBackgroundFallback(
            "res://assets/oracle/map/palette_sprites.bin", 8, 0);
        _dungeonSpritePalette = LoadPaletteWithCommonBackgroundFallback(
            "res://assets/oracle/inventory/palette_sprites.bin", 6, 0);
    }

    public void Initialize(RoomSession rooms, InventoryState inventory, Func<int>? frameCounter = null)
    {
        _rooms = rooms;
        _inventory = inventory;
        _mapData = new MapDataDatabase();
        _layouts = MenuPresentationDatabase.Shared;
        _presentation = new MapPresentationState(rooms.SaveData, inventory);
        _frameCounterSource = frameCounter ?? (() => (int)(Input.TimingFrame & 0xff));
    }

    public void Open(bool debugFastTravel = false)
    {
        _frameCounter = _frameCounterSource();
        _popupFrameAccumulator = 0.0;
        _popupState = 0;
        _popupTimer = 0;
        _popupSize = 0;
        _popupAlternate = 0;
        _scrollTimer = 0;
        _dungeonFlicker = false;
        _popupPosition = new Vector2(-1, -1);
        DebugFastTravel = debugFastTravel;
        if (debugFastTravel)
        {
            MapMode mode = (_rooms.CurrentRoom.TilesetFlags & 0x80) != 0 ? MapMode.Past : MapMode.Present;
            _cursorRoom = _rooms.MinimapRoom;
            Array.Fill(_interiorCursors, _cursorRoom);
            if (_rooms.ActiveGroup is >= FirstInteriorGroup and <= LastInteriorGroup)
                _interiorCursors[_rooms.ActiveGroup - FirstInteriorGroup] = _rooms.CurrentRoom.Id;
            PrepareOverworld(revealAll: true, forcedMode: mode);
            Visible = true;
            QueueRedraw();
            return;
        }

        int dungeon = _rooms.World.GetDungeonIndex(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        if ((_rooms.CurrentRoom.TilesetFlags & 0x18) == 0x08)
            PrepareDungeon(dungeon);
        else
            PrepareOverworld(revealAll: false);
        Visible = true;
        // mapMenu_state0 draws sprites once before starting its fade-in.
        if (Mode != MapMode.Dungeon) UpdatePopupAnimation();
        QueueRedraw();
    }

    public void Close()
    {
        _galeRooms = [];
        DebugFastTravel = false;
        Visible = false;
    }

    public void CycleDebugPage()
    {
        if (!DebugFastTravel)
            return;
        switch (Mode)
        {
            case MapMode.Present:
                PrepareOverworld(revealAll: true, forcedMode: MapMode.Past);
                break;
            case MapMode.Past:
                PrepareInterior(FirstInteriorGroup);
                break;
            case MapMode.Interior when _interiorGroup < LastInteriorGroup:
                PrepareInterior(_interiorGroup + 1);
                break;
            case MapMode.Interior:
                PrepareOverworld(revealAll: true, forcedMode: MapMode.Present);
                break;
            default:
                return;
        }
        QueueRedraw();
    }

    public bool TryGetFastTravelTarget(out int group, out int room)
    {
        group = Mode switch
        {
            MapMode.Present => 0,
            MapMode.Past => 1,
            MapMode.Interior => _interiorGroup,
            _ => -1
        };
        room = _cursorRoom;
        return DebugFastTravel && group >= 0 && _rooms.World.HasRoom(group, room);
    }

    public void Update(double delta)
    {
        if (!Visible)
            return;
        _frameCounter = _frameCounterSource();
        _popupFrameAccumulator += delta * 60.0;
        while (_popupFrameAccumulator >= 1.0)
        {
            _popupFrameAccumulator -= 1.0;
            UpdatePopupAnimation();
        }
        QueueRedraw();
    }

    internal bool HandleDirectionInput(int pressed)
    {
        if (IsScrolling) return false;
        // dungeonMap_scrollingState0 gives Down priority, ignoring horizontal keys.
        if (Mode == MapMode.Dungeon)
            return (pressed & 8) != 0 ? Navigate(Vector2I.Down) :
                (pressed & 4) != 0 && Navigate(Vector2I.Up);
        if ((pressed & 1) != 0)
            return Navigate(Vector2I.Right);
        else if ((pressed & 2) != 0)
            return Navigate(Vector2I.Left);
        else if ((pressed & 4) != 0)
            return Navigate(Vector2I.Up);
        else if ((pressed & 8) != 0)
            return Navigate(Vector2I.Down);
        return false;
    }

    internal void AdvanceDungeonInput()
    {
        if (Mode != MapMode.Dungeon) return;
        if ((_frameCounterSource() & 0x1f) == 0) _dungeonFlicker = !_dungeonFlicker;
        if (_scrollTimer == 0 || --_scrollTimer == 0) return;
        _dungeonScrollY += _scrollDirection;
        RebuildDungeonBackground(_rooms.DungeonMaps.GetDungeon(_dungeonIndex));
    }

    internal bool Navigate(Vector2I direction)
    {
        if (IsScrolling) return false;
        if (Mode == MapMode.Interior)
            return MoveInteriorCursor(direction);
        if (Mode != MapMode.Dungeon)
            return MoveOverworldCursor(direction);
        if (direction == Vector2I.Up)
            return TrySelectDungeonFloor(_dungeonFloor + 1);
        if (direction == Vector2I.Down)
            return TrySelectDungeonFloor(_dungeonFloor - 1);
        return false;
    }

    internal bool MoveOverworldCursor(Vector2I direction)
    {
        if (Mode is MapMode.Dungeon or MapMode.Interior || direction == Vector2I.Zero)
            return false;
        int x = _cursorRoom & 0x0f;
        int y = (_cursorRoom >> 4) & 0x0f;
        x = (x + direction.X + OverworldWidth) % OverworldWidth;
        y = (y + direction.Y + OverworldHeight) % OverworldHeight;
        _cursorRoom = (y << 4) | x;
        LoadPopupData();
        QueueRedraw();
        return true;
    }

    internal void SelectGaleDestination(int room, int[] destinations)
    {
        _galeRooms = destinations;
        _cursorRoom = room;
        LoadPopupData();
        QueueRedraw();
    }

    internal MapText GalePrompt(bool cancel)
    {
        MapText prompt = _mapData.GetText(cancel ? 0x0301 : 0x0300);
        if (!cancel && TryGetSelectedAreaText(out MapText area))
            prompt = prompt with { Message = prompt.Message.Replace("\\call(0xfd)", area.Message) };
        return prompt;
    }

    internal bool MoveInteriorCursor(Vector2I direction)
    {
        if (Mode != MapMode.Interior || direction == Vector2I.Zero)
            return false;
        int x = _cursorRoom & 0x0f;
        int y = (_cursorRoom >> 4) & 0x0f;
        x = (x + direction.X + InteriorGridSize) % InteriorGridSize;
        y = (y + direction.Y + InteriorGridSize) % InteriorGridSize;
        _cursorRoom = (y << 4) | x;
        _interiorCursors[_interiorGroup - FirstInteriorGroup] = _cursorRoom;
        QueueRedraw();
        return true;
    }

    public bool TryGetSelectedAreaText(out MapText text)
    {
        text = default;
        if (Mode is MapMode.Dungeon or MapMode.Interior || DebugFastTravel)
            return false;
        int group = Mode == MapMode.Past ? 1 : 0;
        return _rooms.HasVisited(group, _cursorRoom) &&
            _mapData.TryResolveAreaText(
                _rooms, _presentation, group, _cursorRoom, out text);
    }

    public float SelectedMarkerY => OverworldCellPosition(_cursorRoom).Y;

    public override void _Draw()
    {
        if (!Visible || _background == null)
            return;
        _sprites.Clear();
        DrawTexture(_background, Vector2.Zero);
        if (Mode == MapMode.Dungeon)
            DrawDungeonMarkers();
        else if (Mode == MapMode.Interior)
            DrawInteriorBrowser();
        else
            DrawOverworldMarkers();
        Vector2[] positions = new Vector2[_sprites.Count];
        for (int i = 0; i < positions.Length; i++) positions[i] = _sprites[i].Position;
        ushort[] scanlines = SelectOamScanlines(positions);
        for (int i = 0; i < _sprites.Count; i++) RenderMapSprite(_sprites[i], scanlines[i]);
    }

    private void PrepareOverworld(bool revealAll, MapMode? forcedMode = null)
    {
        Mode = forcedMode ?? ((_rooms.CurrentRoom.TilesetFlags & 0x80) != 0 ? MapMode.Past : MapMode.Present);
        _dungeonIndex = -1;
        _currentMapRoom = (_rooms.CurrentRoom.TilesetFlags & 0x02) != 0 ? 0x38 : _rooms.MinimapRoom;
        if (!DebugFastTravel)
            _cursorRoom = _currentMapRoom;
        if (DebugFastTravel && ((_cursorRoom & 0x0f) >= OverworldWidth ||
            ((_cursorRoom >> 4) & 0x0f) >= OverworldHeight))
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

    private void PrepareInterior(int group)
    {
        if (group is < FirstInteriorGroup or > LastInteriorGroup)
            throw new ArgumentOutOfRangeException(nameof(group));
        if (Mode == MapMode.Interior)
            _interiorCursors[_interiorGroup - FirstInteriorGroup] = _cursorRoom;
        Mode = MapMode.Interior;
        _dungeonIndex = -1;
        _interiorGroup = group;
        _cursorRoom = _interiorCursors[group - FirstInteriorGroup];
        for (int room = 0; room <= 0xff; room++)
        {
            _interiorRoomAvailable[room] = _rooms.World.HasRoom(group, room);
            _interiorRoomDungeon[room] = _interiorRoomAvailable[room] &&
                _rooms.World.GetDungeonIndex(group, room) >= 0;
        }
        _background = BuildInteriorBackground();
        ResetPopupAnimation();
    }

    private void PrepareDungeon(int dungeon)
    {
        Mode = MapMode.Dungeon;
        _dungeonIndex = dungeon;
        DungeonInfo info = _rooms.DungeonMaps.GetDungeon(dungeon);
        bool sideView = (_rooms.CurrentRoom.TilesetFlags & 0x20) != 0;
        if (!info.TryGetRoom(_rooms.CurrentRoom.Id, out DungeonCell activeCell) && !sideView)
            throw new InvalidOperationException(
                $"Dungeon {dungeon:x2} does not place room {_rooms.CurrentRoom.Id:x2} on its floor map.");
        int position = _rooms.SaveData.MinimapDungeonPosition;
        if (_rooms.ActiveGroup == 5 && _rooms.CurrentRoom.Id == 0xf5) position = 0x13;
        _dungeonLinkCell = new DungeonCell(_rooms.SaveData.MinimapDungeonFloor,
            position & 7, position >> 3, _rooms.MinimapRoom, 0);
        _dungeonFloor = sideView
            ? _dungeonLinkCell.Floor : activeCell.Floor;
        _dungeonScrollY = (info.FloorCount - 1 - _dungeonFloor) * 10;
        _cursorRoom = _rooms.CurrentRoom.Id;
        RebuildDungeonBackground(info);
    }

    private bool TrySelectDungeonFloor(int floor)
    {
        DungeonInfo info = _rooms.DungeonMaps.GetDungeon(_dungeonIndex);
        int direction = Math.Sign(floor - _dungeonFloor);
        while (floor >= 0 && floor < info.FloorCount)
        {
            if (CanViewFloor(info, floor))
            {
                _scrollDirection = -direction;
                _scrollTimer = Math.Abs(floor - _dungeonFloor) * 10 + 1;
                _dungeonFloor = floor;
                QueueRedraw();
                return true;
            }
            floor += direction;
        }
        return false;
    }

    private bool CanViewFloor(DungeonInfo info, int floor) =>
        _inventory.HasDungeonMap(info.Index) ||
        (_rooms.SaveData.DungeonVisitedFloors(info.Index) & (1 << floor)) != 0 ||
        (_inventory.HasDungeonCompass(info.Index) &&
            (info.CompassFloors & (1 << floor)) != 0);

    private void RebuildDungeonBackground(DungeonInfo info)
    {
        byte[] map = ReadBytes("res://assets/oracle/map/map_dungeon.bin", 576);
        byte[] flags = ReadBytes("res://assets/oracle/map/flags_dungeon.bin", 576);
        DrawFloorList(map, flags, info);
        DrawSmallKeyCount(map, flags, info.Index);
        // dungeonMap_generateScrollableTilemap: five blank rows, then each
        // floor from top to bottom separated by two rows. updateScroll copies
        // all 18 screen rows, including portions of neighboring floors.
        for (int y = 0; y < 18; y++)
        for (int x = 0; x < 8; x++)
        {
            int offset = y * TilemapStride + 10 + x;
            int sourceY = y + _dungeonScrollY - 5;
            int floor = sourceY < 0 ? -1 : info.FloorCount - 1 - sourceY / 10;
            if (floor < 0 || floor >= info.FloorCount || sourceY % 10 >= 8 ||
                !CanViewFloor(info, floor))
            {
                map[offset] = 0xad;
                flags[offset] = 0;
                continue;
            }
            if (!info.TryGetCell(floor, x, sourceY % 10, out DungeonCell cell) || cell.Room == 0)
            {
                map[offset] = 0xac;
                flags[offset] = 5;
                continue;
            }
            byte roomFlags = _rooms.SaveData.GetRoomFlags(info.Group, cell.Room);
            bool visited = _rooms.HasVisited(info.Group, cell.Room);
            bool hidden = cell.Properties is 0x60 or 0x70;
            int compassTile = GetCompassTile(info.Index, cell.Properties, roomFlags);
            if (hidden)
            {
                map[offset] = 0xac;
                flags[offset] = 0x05;
            }
            else if (compassTile != 0)
            {
                map[offset] = (byte)compassTile;
                flags[offset] = compassTile == 0xae ? (byte)0x02 : (byte)0x00;
            }
            else if (visited)
            {
                map[offset] = (byte)(0xb0 + ((roomFlags | cell.Properties) & 0x0f));
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
                flags[offset] = 0x05;
            }
        }
        _dungeonMapTiles = map;
        DungeonBlurbLayout blurbLayout = _layouts.DungeonBlurb(_dungeonIndex);
        Image blurb = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/map/blurb_{blurbLayout.Asset}.png");
        _background = BuildBackground(map, flags, blurb);
    }

    private void DrawFloorList(byte[] map, byte[] flags, DungeonInfo info)
    {
        int listOffset = _layouts.DungeonFloorListOffset(info.Index);
        int offset = 0xa0 + listOffset;
        for (int floor = info.FloorCount - 1; floor >= 0; floor--)
        {
            if (!CanViewFloor(info, floor))
            {
                offset += TilemapStride;
                continue;
            }
            int name = info.BaseFloor + floor;
            map[offset] = _layouts.DungeonFloorNames[name * 2];
            map[offset + 1] = _layouts.DungeonFloorNames[name * 2 + 1];
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
        map[0x226] = unchecked((byte)(0x90 + keys));
    }

    internal byte DungeonTileAt(int x, int y) =>
        _dungeonMapTiles[(5 + y) * TilemapStride + 10 + x];
    internal byte DungeonScreenTileAt(int x, int y) => _dungeonMapTiles[y * TilemapStride + x];


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
            DrawTileToImage(output,
                SelectTileSource(map[offset], blurb, out int sourceTile),
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

    private void DrawOverworldMarkers()
    {
        for (int i = _galeRooms.Length - 1; i >= 0; i--)
            DrawMapOam("warp", OverworldCellPosition(_galeRooms[i]), (_frameCounter & 0x18) >> 2);
        int portalGroup = Mode == MapMode.Past ? 1 : 0;
        if (_galeRooms.Length == 0 && _presentation.TryGetTimePortalRoom(portalGroup, out int portalRoom))
        {
            int portalFrame = ((_frameCounter >> 3) & 0x03) * 2;
            DrawMapOam("portal", OverworldCellPosition(portalRoom), portalFrame);
        }
        // Reverse the source's OAM submission order: earlier GBC sprites win.
        DrawMapOam("cursor", OverworldCellPosition(_cursorRoom));
        if (LocationArrowVisible)
            DrawMapOam("arrow", OverworldCellPosition(_currentMapRoom));
        DrawPopup();
    }

    private void DrawInteriorBrowser()
    {
        Font font = ThemeDB.FallbackFont;
        Color text = Color.Color8(232, 240, 224);
        Color grid = Color.Color8(80, 104, 112);
        Color ordinaryRoom = Color.Color8(72, 112, 136);
        Color dungeonRoom = Color.Color8(72, 128, 104);
        Color selected = Color.Color8(255, 224, 88);
        Color current = Color.Color8(248, 248, 240);

        DrawString(font, new Vector2(4, 9),
            $"F NEXT   INTERIORS G{_interiorGroup:X1}   ROOM {_cursorRoom:X2}",
            fontSize: 8, modulate: text);

        const string hex = "0123456789ABCDEF";
        for (int index = 0; index < InteriorGridSize; index++)
        {
            DrawString(font,
                new Vector2(InteriorGridLeft + index * InteriorCellSize + 1, InteriorGridTop - 3),
                hex[index].ToString(), fontSize: 7, modulate: text);
            DrawString(font,
                new Vector2(InteriorGridLeft - 8, InteriorGridTop + index * InteriorCellSize + 6),
                hex[index].ToString(), fontSize: 7, modulate: text);
        }

        for (int room = 0; room <= 0xff; room++)
        {
            int x = room & 0x0f;
            int y = room >> 4;
            Vector2 position = new(
                InteriorGridLeft + x * InteriorCellSize,
                InteriorGridTop + y * InteriorCellSize);
            Rect2 cell = new(position, new Vector2(InteriorCellSize - 1, InteriorCellSize - 1));
            if (_interiorRoomAvailable[room])
                DrawRect(cell, _interiorRoomDungeon[room] ? dungeonRoom : ordinaryRoom);
            else
                DrawRect(cell, grid, filled: false, width: 1.0f);

            if (_rooms.ActiveGroup == _interiorGroup && _rooms.CurrentRoom.Id == room)
                DrawCircle(position + new Vector2(3, 3), 1.0f, current);
        }

        Vector2 selectedPosition = new(
            InteriorGridLeft + (_cursorRoom & 0x0f) * InteriorCellSize,
            InteriorGridTop + (_cursorRoom >> 4) * InteriorCellSize);
        DrawRect(new Rect2(selectedPosition - Vector2.One,
            new Vector2(InteriorCellSize + 1, InteriorCellSize + 1)),
            selected, filled: false, width: 1.0f);
        DrawString(font, new Vector2(4, 142), "A WARP   GREEN DUNGEON", fontSize: 8, modulate: text);
    }

    private void DrawDungeonMarkers()
    {
        DungeonInfo info = _rooms.DungeonMaps.GetDungeon(_dungeonIndex);
        int symbolY = _layouts.DungeonSymbols[info.Index * 2];
        int selectedFloorIndex = info.FloorCount - 1 - _dungeonFloor;
        DrawMapOam("floor-cursor", new Vector2(0, symbolY + selectedFloorIndex * 8));
        if (_inventory.HasDungeonCompass(_dungeonIndex))
            DrawMapOam("boss-floor", new Vector2(0, _layouts.DungeonSymbols[info.Index * 2 + 1]));
        if (!IsScrolling)
        {
            if (CanSelectFloor(info, _dungeonFloor - 1)) DrawMapOam("down", Vector2.Zero);
            if (CanSelectFloor(info, _dungeonFloor + 1)) DrawMapOam("up", Vector2.Zero);
            // Source cursor is fixed even when a different floor is selected.
            if (!_dungeonFlicker)
                DrawMapOam("dungeon-cursor", new Vector2(_dungeonLinkCell.X * 8, _dungeonLinkCell.Y * 8));
        }
        int linkFloorIndex = info.FloorCount - 1 - _dungeonLinkCell.Floor;
        DrawMapOam("link-floor", new Vector2(0, symbolY + linkFloorIndex * 8));
        int linkY = linkFloorIndex * 10 + 5 + _dungeonLinkCell.Y - _dungeonScrollY;
        if (_dungeonFlicker && linkY is >= 0 and < 18)
            DrawMapOam("link-map", new Vector2(_dungeonLinkCell.X * 8, (linkY + 1) * 8));
        DrawDungeonItemSprites();
    }

    private void DrawDungeonItemSprites()
    {
        if (_inventory.HasDungeonMap(_dungeonIndex))
            DrawMapOam("map", Vector2.Zero);
        if (_inventory.HasDungeonCompass(_dungeonIndex))
            DrawMapOam("compass", Vector2.Zero);
        if (_inventory.HasDungeonBossKey(_dungeonIndex))
            DrawMapOam("boss-key", Vector2.Zero);
        if (_inventory.GetDungeonSmallKeys(_dungeonIndex) > 0)
            DrawMapOam("small-key", Vector2.Zero);
    }

    private bool CanSelectFloor(DungeonInfo info, int floor)
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

    private static Texture2D BuildInteriorBackground()
    {
        Image image = Image.CreateEmpty(
            OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight, false, Image.Format.Rgba8);
        image.Fill(Color.Color8(24, 40, 48));
        return ImageTexture.CreateFromImage(image);
    }

    private void ApplyOverworldTileSubstitutions(byte[] map, byte[] flags)
    {
        if (Mode == MapMode.Present)
        {
            int companion = _presentation.AnimalCompanion;
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
        if (Mode is MapMode.Dungeon or MapMode.Interior)
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
                return _mapData.TryResolveAreaText(
                    _rooms, _presentation, group, room, out MapText text) &&
                    (text.TextId >> 8) == 0x02 ? 0x08 : 0;
            case 9:
                return _gashaSpots.TryGetSpot(group, room, out SpotRecord spot) &&
                    _rooms.SaveData.IsGashaSpotPlanted(spot.SubId) ? 0x09 : 0;
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

    internal int ResolvePopupTypeForValidation(int type, int group, int room) =>
        ResolvePopupType(type, group, room);

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
                    goto case 3;
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
                    goto case 3;
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
            if (iconIndex > 0 && iconIndex < _layouts.MapIcons.Count)
            {
                MapIconLayout icon = _layouts.MapIcons[iconIndex];
                if (icon.SpriteCount != 0)
                {
                    DrawMapIconPart(icon.Right);
                    DrawMapIconPart(icon.Left);
                }
            }
        }
    }

    private void DrawMapIconPart(MenuOamPart part)
    {
        DrawMapSprite(
            part.Tile,
            part.Attributes & 0x07,
            _popupPosition + new Vector2(part.X + 8, part.Y),
            (part.Attributes & 0x20) != 0,
            (part.Attributes & 0x40) != 0);
    }

    private void BlitMapIconPart(Image output, MenuOamPart part)
    {
        bool flipX = (part.Attributes & 0x20) != 0;
        bool flipY = (part.Attributes & 0x40) != 0;
        int palette = part.Attributes & 0x07;
        int sourceTile = part.Tile;
        int columns = _spriteTiles.GetWidth() / 8;
        int cell = (sourceTile & 0xfe) / 2;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = _spriteTiles.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + (flipY ? 15 - y : y));
            int shade = GetSpriteShade(sourceColor, invertedGrayscale: false);
            if (shade == 0)
                continue;
            output.SetPixel(
                part.X + 8 + x,
                part.Y + y,
                _spritePalette[palette, shade]);
        }
    }

    private void DrawPopupBorder()
    {
        if (_popupSize > 0)
            DrawMapOam($"border{_popupSize}", _popupPosition + new Vector2(16, 16));
    }

    private void DrawMapOam(string layout, Vector2 offset, int tileOffset = 0)
    {
        var parts = _layouts.MapOam(layout);
        for (int index = parts.Count - 1; index >= 0; index--)
        {
            MenuOamPart part = parts[index];
            DrawMapSprite(part.Tile + tileOffset, part.Attributes & 7,
                new Vector2(((int)offset.X + part.X) & 0xff,
                    ((int)offset.Y + part.Y) & 0xff) - new Vector2(8, 16),
                (part.Attributes & 0x20) != 0, (part.Attributes & 0x40) != 0);
        }
    }

    private void DrawMapSprite(int tile, int palette, Vector2 position,
        bool flipX = false, bool flipY = false) =>
        _sprites.Add(new MapSprite(tile, palette, position, flipX, flipY));

    // Sprites are queued in painter order (reverse of original OAM order).
    // The PPU selects at most ten sprites by Y on each scanline, even when
    // a selected sprite is horizontally offscreen or has transparent pixels.
    internal static ushort[] SelectOamScanlines(IReadOnlyList<Vector2> positions)
    {
        var result = new ushort[positions.Count];
        Span<int> counts = stackalloc int[144];
        counts.Clear();
        for (int i = positions.Count - 1; i >= Math.Max(0, positions.Count - 40); i--)
        for (int y = 0; y < 16; y++)
        {
            int line = (int)positions[i].Y + y;
            if (line is >= 0 and < 144 && counts[line]++ < 10)
                result[i] |= (ushort)(1 << y);
        }
        return result;
    }

    private readonly record struct MapSprite(int Tile, int Palette, Vector2 Position, bool FlipX, bool FlipY);

    private void RenderMapSprite(MapSprite sprite, ushort scanlines)
    {
        int tile = sprite.Tile;
        int palette = sprite.Palette;
        Vector2 position = sprite.Position;
        bool flipX = sprite.FlipX, flipY = sprite.FlipY;
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
            if ((scanlines & (1 << y)) == 0) continue;
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

}

public enum MapMode
{
    Present,
    Past,
    Interior,
    Dungeon
}
