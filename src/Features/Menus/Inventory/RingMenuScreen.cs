using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;

/// <summary>
/// Renderer for bank 2's GFXH_UNAPPRAISED_RING_LIST and
/// GFXH_APPRAISED_RING_LIST screens. The imported maps remain the authority;
/// live ring icons, page/number digits, cursors, and C/E markers are layered
/// at the original tilemap and OAM positions.
/// </summary>
public partial class RingMenuScreen : Node2D
{
    internal const int PageScrollUpdates = 19;
    private const int PageScrollPixelsPerUpdate = 8;
    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int AppraisalRows = 16;
    private const int ListRows = 18;
    private const int RingNameColumns = 16;
    private const int RingNameY = 11 * 8;

    private Image _hudTiles = null!;
    private Image _inventoryHud1 = null!;
    private Image _questItems5 = null!;
    private Image _ringTiles = null!;
    private Image _inventoryHud2 = null!;
    private Image _emptyTextTiles = null!;
    private byte[] _ringMap = null!;
    private Texture2D _fontTexture = null!;
    private Color[,] _bgPalette = null!;
    private Color[,] _spritePalette = null!;
    private VramSource[] _appraisalUnsignedBank0 = null!;
    private VramSource[] _appraisalSignedBank0 = null!;
    private VramSource[] _listUnsignedBank0 = null!;
    private VramSource[] _listSignedBank0 = null!;
    private VramSource[] _signedBank1 = null!;
    private Texture2D? _background;
    private InventoryState _inventory = null!;
    private readonly FixedUpdateAccumulator _animationUpdates = new();
    private int _listCursorFlickerCounter;
    private int _boxCursorFlickerCounter;
    private int _transitionPage;
    private int _transitionCursor;
    private int _transitionDirection;
    private int _transitionFrame;
    private string _ringName = string.Empty;

    internal RingMenuMode Mode { get; private set; }
    internal int Page { get; private set; }
    internal int PageCount { get; private set; } = 1;
    internal int ListCursor { get; private set; }
    internal int BoxCursor { get; private set; }
    internal bool SelectingList { get; private set; }
    internal bool PageTransitionActive => _transitionDirection != 0;
    internal int PageTransitionFrame => _transitionFrame;
    internal ulong BackgroundHashForValidation { get; private set; }
    internal Vector2I BackgroundSizeForValidation => _background is null
        ? Vector2I.Zero
        : new Vector2I(_background.GetWidth(), _background.GetHeight());
    internal float BackgroundAlphaForValidation(Vector2I point)
    {
        if (_background is null || point.X < 0 || point.X >= _background.GetWidth() ||
            point.Y < 0 || point.Y >= _background.GetHeight())
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
        return _background.GetImage().GetPixel(point.X, point.Y).A;
    }
    internal string DisplayedRingNameForValidation => _ringName;
    internal Vector2 ListCursorPositionForValidation => ListCursorPosition(ListCursor);
    internal Vector2 RingNamePositionForValidation => RingNamePosition(_ringName.Length);

    public override void _Ready()
    {
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _inventoryHud1 = LoadPng(
            "res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        _questItems5 = LoadPng(
            "res://assets/oracle/inventory/spr_quest_items_5.png");
        _ringTiles = LoadPng("res://assets/oracle/inventory/gfx_rings.png");
        _inventoryHud2 = LoadPng(
            "res://assets/oracle/inventory/gfx_inventory_hud_2.png");
        _fontTexture = BuildFontTexture("res://assets/oracle/gfx/gfx_font.png");
        _emptyTextTiles = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        _emptyTextTiles.Fill(Colors.Black);
        _ringMap = ReadBytes("res://assets/oracle/inventory/map_rings.bin", 68 * 8);
        _bgPalette = LoadPalette("res://assets/oracle/inventory/palette_bg.bin", 8);
        _spritePalette = ItemIconAtlas.LoadStandardSpritePalettes();

        // GFXH_UNAPPRAISED_RING_LIST and GFXH_APPRAISED_RING_LIST load these
        // records into VRAM bank 0. LCDC bit 4 changes at the textbox boundary:
        // tile $00 then addresses $9000 instead of $8000. Attribute bit 3 is
        // the independent VRAM-bank selector, not part of the destination.
        _appraisalUnsignedBank0 =
        [
            new VramSource(0x00, _inventoryHud1, false),
            new VramSource(0xa0, _ringTiles, true),
            new VramSource(0xe0, _inventoryHud2, false)
        ];
        _appraisalSignedBank0 =
        [
            new VramSource(0x00, _hudTiles, false),
            new VramSource(0xa0, _ringTiles, true),
            new VramSource(0xe0, _inventoryHud2, false)
        ];
        _listUnsignedBank0 =
        [
            new VramSource(0x00, _inventoryHud1, false),
            new VramSource(0x40, _questItems5, true, true),
            new VramSource(0xa0, _ringTiles, true),
            new VramSource(0xe0, _inventoryHud2, false)
        ];
        _listSignedBank0 =
        [
            new VramSource(0x00, _inventoryHud1, false),
            new VramSource(0xa0, _ringTiles, true),
            new VramSource(0xe0, _inventoryHud2, false)
        ];

        // showItemText2 clears w7TextGfxBuffer to $ff, then UNCMP_GFXH_17
        // uploads its 32 tiles to $9201. DialogueBox supplies live glyphs in
        // this port; the backing bank-1 tiles must remain cleared, not alias
        // an unrelated graphics sheet.
        _signedBank1 = [new VramSource(0x20, _emptyTextTiles, true)];
    }

    internal void Initialize(InventoryState inventory)
    {
        if (_inventory is not null)
            _inventory.Changed -= OnInventoryChanged;
        _inventory = inventory;
        _inventory.Changed += OnInventoryChanged;
    }

    public override void _ExitTree()
    {
        if (_inventory is not null)
            _inventory.Changed -= OnInventoryChanged;
    }

    internal void Open(RingMenuMode mode)
    {
        Mode = mode;
        Page = 0;
        PageCount = mode == RingMenuMode.List
            ? 4
            : Math.Max(1, (_inventory.UnappraisedRingCount + 15) / 16);
        ListCursor = 0;
        BoxCursor = 0;
        SelectingList = mode == RingMenuMode.Appraisal;
        _listCursorFlickerCounter = 0;
        _boxCursorFlickerCounter = 0x80;
        _transitionDirection = 0;
        _transitionFrame = 0;
        _ringName = string.Empty;
        _animationUpdates.Reset();
        BuildBackground();
        Visible = true;
        QueueRedraw();
    }

    internal void Close()
    {
        Visible = false;
        _background = null;
        _transitionDirection = 0;
    }

    internal void SetPageAndCursor(int page, int cursor)
    {
        Page = Math.Clamp(page, 0, Math.Max(0, PageCount - 1));
        ListCursor = cursor & 0x0f;
        QueueRedraw();
    }

    internal bool BeginPageTransition(int page, int cursor, int direction)
    {
        if (PageTransitionActive || direction == 0 || page == Page)
            return false;
        _transitionPage = Math.Clamp(page, 0, Math.Max(0, PageCount - 1));
        _transitionCursor = cursor & 0x0f;
        _transitionDirection = Math.Sign(direction);
        _transitionFrame = 0;
        _animationUpdates.Reset();
        QueueRedraw();
        return true;
    }

    /// <summary>
    /// Advances the 8-pixel-per-update bank-2 page scroll. The caller owns
    /// the separate initialization update which starts SND_OPENMENU.
    /// </summary>
    internal bool AdvanceAnimation(double delta, out bool transitionCompleted)
    {
        transitionCompleted = false;
        int updates = _animationUpdates.Consume(delta);
        for (int update = 0; update < updates; update++)
        {
            if (SelectingList)
                _listCursorFlickerCounter = (_listCursorFlickerCounter + 1) & 0xff;
            else if (Mode == RingMenuMode.List)
            {
                _boxCursorFlickerCounter =
                    ((_boxCursorFlickerCounter + 1) & 0xef) | 0x80;
            }

            if (!PageTransitionActive)
                continue;
            _transitionFrame++;
            if (_transitionFrame < PageScrollUpdates)
                continue;
            Page = _transitionPage;
            ListCursor = _transitionCursor;
            _transitionDirection = 0;
            _transitionFrame = 0;
            transitionCompleted = true;
        }
        if (updates > 0)
            QueueRedraw();
        return PageTransitionActive || transitionCompleted;
    }

    internal void RecalculateAppraisalPages()
    {
        if (Mode != RingMenuMode.Appraisal)
            return;
        PageCount = Math.Max(1, (_inventory.UnappraisedRingCount + 15) / 16);
        Page = Math.Min(Page, PageCount - 1);
        QueueRedraw();
    }

    internal void SetBoxCursor(int cursor)
    {
        BoxCursor = Math.Clamp(cursor, 0, Math.Max(0, _inventory.RingBoxCapacity - 1));
        QueueRedraw();
    }

    internal void SetSelectingList(bool selectingList)
    {
        SelectingList = Mode == RingMenuMode.Appraisal || selectingList;
        if (Mode == RingMenuMode.List)
            _boxCursorFlickerCounter = SelectingList ? 0 : 0x80;
        QueueRedraw();
    }

    internal void SetRingName(string? name)
    {
        string sanitized = (name ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        _ringName = Mode == RingMenuMode.List
            ? sanitized[..Math.Min(RingNameColumns, sanitized.Length)]
            : string.Empty;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _background is null || _inventory is null)
            return;
        DrawTexture(_background, Vector2.Zero);
        if (Mode == RingMenuMode.List)
            DrawRingBox();
        if (PageTransitionActive)
        {
            float travel = _transitionFrame * PageScrollPixelsPerUpdate;
            float currentX = -_transitionDirection * travel;
            float incomingX = currentX + _transitionDirection * OracleRoomData.ViewportWidth;
            DrawSelectionRings(Page, currentX);
            DrawSelectionRings(_transitionPage, incomingX);
            DrawPageCounter(Page, currentX);
            DrawPageCounter(_transitionPage, incomingX);
            if (Mode == RingMenuMode.List)
            {
                DrawEquippedMarker();
                DrawBoxCursor();
            }
            return;
        }
        DrawSelectionRings(Page, 0);
        DrawPageCounter(Page, 0);
        if (Mode == RingMenuMode.List)
        {
            DrawRingNumber();
            DrawRingName();
            DrawListMarkers();
            DrawEquippedMarker();
            DrawBoxCursor();
        }
        if (SelectingList)
            DrawListCursorAndArrows();
    }

    private void DrawSelectionRings(int page, float xOffset)
    {
        int first = page * 16;
        for (int index = 0; index < 16; index++)
        {
            int ring;
            if (Mode == RingMenuMode.Appraisal)
            {
                ring = _inventory.UnappraisedRingAt(first + index);
                if (ring == 0xff)
                    continue;
            }
            else
            {
                ring = first + index;
                if (!_inventory.HasAppraisedRing(ring))
                    continue;
            }
            DrawRingGraphic(ring, ListRingPosition(index) + new Vector2(xOffset, 0));
        }
    }

    private void DrawRingBox()
    {
        DrawRingGraphic(0x40 + Math.Clamp(_inventory.RingBoxLevel, 1, 3),
            new Vector2(8, 0), preserveBoxGraphic: true);
        for (int slot = 0; slot < 5; slot++)
        {
            int ring = slot < _inventory.RingBoxCapacity
                ? _inventory.RingAt(slot)
                : 0xff;
            if (ring != 0xff)
                DrawRingGraphic(ring, BoxRingPosition(slot));
        }
    }

    private void DrawPageCounter(int page, float xOffset)
    {
        DrawVramBackgroundTile(0, 0x11 + page, 0x07,
            new Vector2(120 + xOffset, 80));
        DrawVramBackgroundTile(0, 0x10 + PageCount, 0x07,
            new Vector2(136 + xOffset, 80));
    }

    private void DrawRingNumber()
    {
        int selected = SelectingList
            ? Page * 16 + ListCursor
            : _inventory.RingAt(BoxCursor);
        if (selected == 0xff)
        {
            DrawVramBackgroundTile(0, 0xe8, 0x07, new Vector2(32, 80));
            DrawVramBackgroundTile(0, 0xe8, 0x07, new Vector2(40, 80));
            return;
        }
        int number = selected + 1;
        DrawVramBackgroundTile(0, 0x10 + number / 10, 0x07, new Vector2(32, 80));
        DrawVramBackgroundTile(0, 0x10 + number % 10, 0x07, new Vector2(40, 80));
    }

    private void DrawRingName()
    {
        Vector2 position = RingNamePosition(_ringName.Length);
        Color color = _bgPalette[0, 2];
        for (int index = 0; index < _ringName.Length; index++)
        {
            int glyph = _ringName[index] <= 0xff ? _ringName[index] : 0x3f;
            Rect2 source = new((glyph & 0x0f) * 8, (glyph >> 4) * 16, 8, 16);
            Rect2 destination = new(
                position + new Vector2(index * 8, 0), new Vector2(8, 16));
            DrawTextureRectRegion(_fontTexture, destination, source, color);
        }
    }

    private void DrawListMarkers()
    {
        int first = Page * 16;
        for (int slot = 0; slot < _inventory.RingBoxCapacity; slot++)
        {
            int ring = _inventory.RingAt(slot);
            if (ring < first || ring >= first + 16)
                continue;
            int index = ring - first;
            DrawRawOamTile(0, 0xef, 5,
                new Vector2(24 + (index & 7) * 16, index < 8 ? 32 : 56));
        }
    }

    private void DrawEquippedMarker()
    {
        if (_inventory.ActiveRing == 0xff)
            return;
        for (int slot = 0; slot < _inventory.RingBoxCapacity; slot++)
        {
            if (_inventory.RingAt(slot) != _inventory.ActiveRing)
                continue;
            DrawRawOamTile(0, 0xec, 4, new Vector2(48 + slot * 24, 0));
            break;
        }
    }

    private void DrawBoxCursor()
    {
        if (Mode != RingMenuMode.List ||
            (!SelectingList && (_boxCursorFlickerCounter & 0x08) != 0))
            return;
        DrawRawOamTile(0, 0x0e, 3,
            new Vector2(44 + BoxCursor * 24, 14));
    }

    private void DrawListCursorAndArrows()
    {
        if ((_listCursorFlickerCounter & 0x08) == 0)
        {
            DrawRawOamTile(0, 0x0e, 2,
                ListCursorPosition(ListCursor));
        }
        if (PageCount <= 1)
            return;
        DrawRawOamTile(0, 0x08, 4, new Vector2(4, 44));
        DrawRawOamTile(0, 0x08, 4, new Vector2(148, 44), flipX: true);
    }

    private void BuildBackground()
    {
        string kind = Mode == RingMenuMode.Appraisal ? "unappraised" : "appraised";
        int rows = Mode == RingMenuMode.Appraisal ? AppraisalRows : ListRows;
        byte[] map = ReadBytes(
            $"res://assets/oracle/inventory/map_{kind}_ring_list.bin",
            TilemapStride * rows);
        byte[] flags = ReadBytes(
            $"res://assets/oracle/inventory/flg_{kind}_ring_list.bin",
            TilemapStride * rows);

        if (Mode == RingMenuMode.List)
            ApplyRingBoxSlotSubstitution(map, flags);

        Image output = Image.CreateEmpty(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight,
            false, Image.Format.Rgba8);
        // State $0f displays the ordinary status map in the first 16 lines.
        // The global live Hud already occupies that original screen position,
        // so this renderer leaves the aperture transparent and supplies all
        // 16 appraisal rows below it. State $10 replaces those lines with the
        // Ring Box instead.
        output.Fill(Mode == RingMenuMode.Appraisal
            ? Colors.Transparent
            : _bgPalette[0, 0]);

        // Gfx register states $0f/$10 start the window at WY=$10. Once its
        // first one or two rows have been drawn, the LCD interrupt disables
        // it and SCY=$f0 makes BG row 1/2 continue at screen y=$18/$20. Thus
        // the ordinary w4TileMap begins 16 pixels below the top of the LCD.
        int lastScreenRow = 17;
        for (int screenRow = 2; screenRow <= lastScreenRow; screenRow++)
        for (int column = 0; column < ScreenColumns; column++)
        {
            // The second ring-list interrupt at line $87 selects SCY=$80,
            // displaying w4TileMap row 1 again on the final scanline row.
            int sourceRow = Mode == RingMenuMode.List && screenRow == 17
                ? 1
                : screenRow - 2;
            int offset = sourceRow * TilemapStride + column;
            int destinationY = screenRow * 8;
            DrawVramTileToImage(output, (flags[offset] & 0x08) != 0 ? 1 : 0,
                map[offset], flags[offset], column * 8, destinationY,
                UsesSignedBackgroundTiles(destinationY));
        }

        // In MENU_RING_LIST, BG rows 30-31 display the $0200 tilemap segment
        // over screen y=$00-$0f before the window begins. It contains the
        // Ring Box and its capacity-dependent slot layout.
        if (Mode == RingMenuMode.List)
        {
            for (int row = 16; row < 18; row++)
            for (int column = 0; column < ScreenColumns; column++)
            {
                int offset = row * TilemapStride + column;
                DrawVramTileToImage(output, (flags[offset] & 0x08) != 0 ? 1 : 0,
                    map[offset], flags[offset], column * 8, (row - 16) * 8,
                    signedAddressing: false);
            }
        }
        BackgroundHashForValidation = OracleGraphicsCache.PixelHash(output);
        _background = ImageTexture.CreateFromImage(output);
    }

    private void ApplyRingBoxSlotSubstitution(byte[] map, byte[] flags)
    {
        // Ages mapMenu_tileSubstitutionTable entries 2 and 3 copy a 2x13
        // rectangle from off-screen w4TileMap+$213 over the surplus slots.
        // L-3 uses entry 4, whose empty record preserves all five slots.
        int destination = _inventory.RingBoxLevel switch
        {
            <= 1 => 0x207,
            2 => 0x20d,
            _ => -1
        };
        if (destination < 0)
            return;

        const int source = 0x213;
        const int width = 13;
        const int height = 2;
        CopyTilemapRectangle(map, destination, source, width, height);
        CopyTilemapRectangle(flags, destination, source, width, height);
    }

    private static void CopyTilemapRectangle(
        byte[] data, int destination, int source, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            Array.Copy(data, source + row * TilemapStride,
                data, destination + row * TilemapStride, width);
        }
    }

    private void DrawRingGraphic(
        int graphic, Vector2 position, bool preserveBoxGraphic = false)
    {
        int normalized = !preserveBoxGraphic && (graphic & 0x40) != 0
            ? 0x40
            : graphic;
        int offset = normalized * 8;
        if (offset < 0 || offset + 7 >= _ringMap.Length)
            return;
        for (int cell = 0; cell < 4; cell++)
        {
            byte tile = _ringMap[offset + cell * 2];
            byte flags = _ringMap[offset + cell * 2 + 1];
            DrawVramBackgroundTile((flags & 0x08) != 0 ? 1 : 0, tile, flags,
                position + new Vector2((cell & 1) * 8, (cell >> 1) * 8));
        }
    }

    private void DrawRawOamTile(
        int bank, int tile, int palette, Vector2 position, bool flipX = false)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            if (!TryGetVramPixel(bank, (tile & 0xfe) + y / 8, sx, y & 7,
                out Color pixel, out _))
                continue;
            int shade = TwoBitShade(pixel);
            if (shade != 0 && palette < _spritePalette.GetLength(0))
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _spritePalette[palette, shade]);
        }
    }

    private void DrawVramBackgroundTile(int bank, int tile, int flags, Vector2 position)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int palette = flags & 7;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            if (!TryGetVramPixel(bank, tile, flipX ? 7 - x : x,
                flipY ? 7 - y : y, out Color pixel, out bool spriteEncoding))
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _bgPalette[palette, PaletteShade(pixel, spriteEncoding)]);
        }
    }

    private void DrawVramTileToImage(
        Image output, int bank, byte tile, byte flags, int x, int y,
        bool signedAddressing)
    {
        if (!TrySelectVramTile(bank, tile, out Image source, out int sourceTile,
            out bool interleaved, out bool spriteEncoding, signedAddressing))
            return;
        DrawTileToImage(output, source, sourceTile, flags, _bgPalette, x, y,
            interleaved, spriteEncoding);
    }

    private bool TryGetVramPixel(
        int bank, int tile, int x, int y, out Color pixel, out bool spriteEncoding)
    {
        if (!TrySelectVramTile(bank, tile, out Image source, out int sourceTile,
            out bool interleaved, out spriteEncoding, signedAddressing: false))
        {
            pixel = Colors.Transparent;
            return false;
        }
        int columns = source.GetWidth() / 8;
        int readX;
        int readY;
        if (interleaved)
        {
            int cell = sourceTile / 2;
            readX = cell % columns * 8 + x;
            readY = cell / columns * 16 + (sourceTile & 1) * 8 + y;
        }
        else
        {
            readX = sourceTile % columns * 8 + x;
            readY = sourceTile / columns * 8 + y;
        }
        pixel = source.GetPixel(readX, readY);
        return true;
    }

    private bool TrySelectVramTile(
        int bank, int tile, out Image source, out int sourceTile,
        out bool interleaved, out bool spriteEncoding, bool signedAddressing)
    {
        VramSource[] candidates = bank == 1
            ? (signedAddressing ? _signedBank1 : [])
            : Mode == RingMenuMode.Appraisal
                ? (signedAddressing
                    ? _appraisalSignedBank0
                    : _appraisalUnsignedBank0)
                : (signedAddressing ? _listSignedBank0 : _listUnsignedBank0);
        VramSource? selected = null;
        foreach (VramSource candidate in candidates)
        {
            if (tile >= candidate.FirstTile &&
                tile < candidate.FirstTile + candidate.TileCount)
                selected = candidate;
        }
        if (selected is not VramSource result)
        {
            source = _inventoryHud1;
            sourceTile = 0;
            interleaved = false;
            spriteEncoding = false;
            return false;
        }
        source = result.Image;
        sourceTile = tile - result.FirstTile;
        interleaved = result.Interleaved;
        spriteEncoding = result.SpriteEncoding;
        return true;
    }

    private static Vector2 ListRingPosition(int index) =>
        new(16 + (index & 7) * 16, index < 8 ? 32 : 56);

    // ringMenu_drawSprites adds @cursorSprite's -4 Y to $3e/$56, then the
    // Game Boy OAM renderer subtracts its 16-pixel hardware Y bias.
    private static Vector2 ListCursorPosition(int index) =>
        new(20 + (index & 7) * 16, index < 8 ? 42 : 66);

    private static Vector2 RingNamePosition(int length) =>
        new(16 + Math.Max(0, (RingNameColumns - length) / 2) * 8, RingNameY);

    private bool UsesSignedBackgroundTiles(int y) => Mode == RingMenuMode.Appraisal
        ? y >= 88 // lcdInterrupt_ringMenu after LYC $57
        : y >= 72; // lcdInterrupt_ringMenu after LYC $47

    private static Vector2 BoxRingPosition(int slot) => new(40 + slot * 24, 0);

    private static Texture2D BuildFontTexture(string path)
    {
        Image source = LoadPng(path);
        Image output = Image.CreateEmpty(
            source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);
        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
        {
            Color pixel = source.GetPixel(x, y);
            output.SetPixel(x, y,
                pixel.R > 0.5f ? Colors.White : Colors.Transparent);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private void OnInventoryChanged() => QueueRedraw();
}
