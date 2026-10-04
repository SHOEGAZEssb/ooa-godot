using Godot;
using System;
using System.Collections.Generic;
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
    private byte[] _fontPixels = null!;
    private int _fontStride;
    private Texture2D _fontTexture = null!;
    private Color[,] _bgPalette = null!;
    private Color[,] _spritePalette = null!;
    private OracleVramSource[] _appraisalUnsignedBank0 = null!;
    private OracleVramSource[] _appraisalSignedBank0 = null!;
    private OracleVramSource[] _listUnsignedBank0 = null!;
    private OracleVramSource[] _listSignedBank0 = null!;
    private OracleVramSource[] _signedBank1 = null!;
    private Texture2D? _background;
    private ImageTexture? _composedTexture;
    private InventoryState _inventory = null!;
    private MenuPresentationDatabase _layouts = null!;
    private readonly FixedUpdateAccumulator _animationUpdates = new();
    private int _listCursorFlickerCounter;
    private int _boxCursorFlickerCounter;
    private int _transitionPage;
    private int _transitionCursor;
    private int _transitionDirection;
    private int _transitionFrame;
    private bool _transitionInitialized;
    private int _tilemapIndex;
    private readonly int[] _displayedAppraisalRings = new int[InventoryState.UnappraisedRingCapacity];
    private string _ringName = string.Empty;
    private int _ringNumberComparator;
    private int _displayedRing = 0xff;
    private readonly List<MenuOamPart> _spriteOam = new();
    internal IReadOnlyList<MenuOamPart> SpriteOam => _spriteOam;

    internal RingMenuMode Mode { get; private set; }
    internal int Page { get; private set; }
    internal int PageCount { get; private set; } = 1;
    internal int ListCursor { get; private set; }
    internal int BoxCursor { get; private set; }
    internal bool SelectingList { get; private set; }
    internal bool PageTransitionActive => _transitionDirection != 0;
    internal int RequestedPage => PageTransitionActive ? _transitionPage : Page;
    internal int SelectedListRing => PageTransitionActive
        ? _transitionPage * 16 + _transitionCursor : Page * 16 + ListCursor;
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

    private bool _resourcesPrepared;
    public override void _Ready() => PrepareResources();

    internal void PrepareResources()
    {
        if (_resourcesPrepared) return;
        _resourcesPrepared = true;
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _inventoryHud1 = LoadPng(
            "res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        _questItems5 = LoadPng(
            "res://assets/oracle/inventory/spr_quest_items_5.png");
        _ringTiles = LoadPng("res://assets/oracle/inventory/gfx_rings.png");
        _inventoryHud2 = LoadPng(
            "res://assets/oracle/inventory/gfx_inventory_hud_2.png");
        _fontTexture = OracleTileRenderer.BuildMonochromeFontTexture("res://assets/oracle/gfx/gfx_font.png");
        using (Image font = _fontTexture.GetImage())
        {
            _fontPixels = font.GetData();
            _fontStride = font.GetWidth() * 4;
        }
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
            new OracleVramSource(0x00, _inventoryHud1, false),
            new OracleVramSource(0xa0, _ringTiles, true),
            new OracleVramSource(0xe0, _inventoryHud2, false)
        ];
        _appraisalSignedBank0 =
        [
            new OracleVramSource(0x00, _hudTiles, false),
            new OracleVramSource(0xa0, _ringTiles, true),
            new OracleVramSource(0xe0, _inventoryHud2, false)
        ];
        _listUnsignedBank0 =
        [
            new OracleVramSource(0x00, _inventoryHud1, false),
            new OracleVramSource(0x40, _questItems5, true, true),
            new OracleVramSource(0xa0, _ringTiles, true),
            new OracleVramSource(0xe0, _inventoryHud2, false)
        ];
        _listSignedBank0 =
        [
            new OracleVramSource(0x00, _inventoryHud1, false),
            new OracleVramSource(0xa0, _ringTiles, true),
            new OracleVramSource(0xe0, _inventoryHud2, false)
        ];

        // showItemText2 clears w7TextGfxBuffer to $ff, then UNCMP_GFXH_17
        // uploads its 32 tiles to $9201. DialogueBox supplies live glyphs in
        // this port; the backing bank-1 tiles must remain cleared, not alias
        // an unrelated graphics sheet.
        _signedBank1 = [new OracleVramSource(0x20, _emptyTextTiles, true)];
    }

    internal void Initialize(InventoryState inventory)
    {
        if (_inventory is not null)
            _inventory.Changed -= OnInventoryChanged;
        _layouts = MenuPresentationDatabase.Shared;
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
            // bank2.ringMenu_calculateNumPagesForUnappraisedRings returns
            // without writing when empty; the cleared menu union keeps zero.
            : (_inventory.UnappraisedRingCount + 15) / 16;
        ListCursor = 0;
        BoxCursor = 0;
        SelectingList = mode == RingMenuMode.Appraisal;
        _listCursorFlickerCounter = 0;
        _boxCursorFlickerCounter = 0x80;
        _transitionDirection = 0;
        _transitionFrame = 0;
        _transitionInitialized = false;
        _tilemapIndex = 0;
        _ringName = string.Empty;
        _ringNumberComparator = 0xfe;
        _displayedRing = 0xff;
        _spriteOam.Clear();
        _animationUpdates.Reset();
        BuildBackground();
        if (Mode == RingMenuMode.Appraisal) RefreshAppraisalGraphics();
        Visible = true;
        QueueRedraw();
    }

    internal void Close()
    {
        Visible = false;
        _background = null;
        _spriteOam.Clear();
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
        _transitionInitialized = false;
        _animationUpdates.Reset();
        QueueRedraw();
        return true;
    }

    internal bool InitializePageTransition()
    {
        _transitionInitialized = true;
        _tilemapIndex ^= 1;
        if (Mode == RingMenuMode.Appraisal) RefreshAppraisalGraphics();
        QueueRedraw();
        return _tilemapIndex == 0;
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

    internal bool RefreshAppraisalGraphics()
    {
        // ringMenu_drawUnappraisedRings publishes icons at explicit menu
        // boundaries. Removing an entry retains its revealed icon through
        // the result delay or closing fade until the next native redraw.
        for (int index = 0; index < _displayedAppraisalRings.Length; index++)
            _displayedAppraisalRings[index] = _inventory.UnappraisedRingAt(index);
        QueueRedraw();
        return _tilemapIndex == 0;
    }

    internal void SetBoxCursor(int cursor)
    {
        BoxCursor = Math.Clamp(cursor, 0, Math.Max(0, _inventory.RingBoxCapacity - 1));
        QueueRedraw();
    }

    internal void SetSelectingList(bool selectingList, bool resetBoxFlicker = true)
    {
        SelectingList = Mode == RingMenuMode.Appraisal || selectingList;
        if (Mode == RingMenuMode.List && resetBoxFlicker)
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

    internal void SetRingNumberComparator(int comparator) => _ringNumberComparator = comparator;

    internal bool UpdateDisplayedRingNumber(int comparator, int selectedRing)
    {
        // bank2.ringMenu_updateDisplayedRingNumberWithGivenComparator writes
        // retained BG digits only on a changed comparator. Cursor ownership
        // and page completion alone do not refresh those tiles.
        if (_ringNumberComparator == comparator) return false;
        _ringNumberComparator = comparator;
        _displayedRing = selectedRing;
        QueueRedraw();
        // This refresh uploads the entire map. Only $9800 replaces the
        // ring-list IRQ's fixed description background below line $47.
        return _tilemapIndex == 0;
    }

    public override void _Draw()
    {
        if (!Visible || _background is null || _inventory is null)
            return;
        using Image frame = ComposeImage();
        if (_composedTexture is null) _composedTexture = ImageTexture.CreateFromImage(frame);
        else _composedTexture.Update(frame);
        DrawTexture(_composedTexture, Vector2.Zero);
    }

    internal Image ComposeImage()
    {
        Image output = (Image)_background!.GetImage().Duplicate();
        if (Mode == RingMenuMode.List)
            DrawRingBox(output);
        if (PageTransitionActive)
        {
            if (_transitionInitialized)
            {
                using Image current = (Image)_background.GetImage().Duplicate();
                using Image incoming = (Image)_background.GetImage().Duplicate();
                DrawSelectionRings(current, Page, 0);
                DrawSelectionRings(incoming, _transitionPage, 0);
                DrawPageCounter(current, Page, 0);
                DrawPageCounter(incoming, _transitionPage, 0);
                int travel = _transitionFrame * PageScrollPixelsPerUpdate;
                int firstLine = Mode == RingMenuMode.List ? 32 : 24;
                int lastLine = Mode == RingMenuMode.List ? 72 : 88;
                for (int y = firstLine; y < lastLine; y++)
                for (int x = 0; x < 160; x++)
                {
                    // ringMenu_state2 moves BG by 8 and WX through $9f..$07.
                    // WX's hardware bias puts the incoming edge at 152,
                    // while the window wins over the underlying BG page.
                    bool window = x >= (_transitionDirection > 0 ? 152 - travel : travel);
                    Image source = _transitionDirection > 0
                        ? (window ? incoming : current) : (window ? current : incoming);
                    int sourceX = _transitionDirection > 0
                        ? (window ? x - (152 - travel) : x + travel)
                        : (window ? x - travel : x + 152 - travel);
                    output.SetPixel(x, y, source.GetPixel(sourceX, y));
                }
            }
            else
            {
                DrawSelectionRings(output, Page, 0);
                DrawPageCounter(output, Page, 0);
            }
            if (Mode == RingMenuMode.List)
            {
                // lcdInterrupt_ringMenu fixes SCX=$00/LCDC=$87 below $47;
                // both alternating upload headers keep its digits in $9800.
                DrawPageCounter(output, _transitionInitialized ? _transitionPage : Page, 0);
                DrawRingNumber(output);
            }
            // The scroll request retains the published name. Only the next
            // state-2 initialization clears it through showItemText2.
            if (Mode == RingMenuMode.List) DrawRingName(output);
            DrawSubmittedSprites(output);
            return output;
        }
        DrawSelectionRings(output, Page, 0);
        DrawPageCounter(output, Page, 0);
        if (Mode == RingMenuMode.List)
        {
            DrawRingNumber(output);
            DrawRingName(output);
        }
        DrawSubmittedSprites(output);
        return output;
    }

    private void DrawSelectionRings(Image output, int page, float xOffset)
    {
        int first = page * 16;
        for (int index = 0; index < 16; index++)
        {
            int ring;
            if (Mode == RingMenuMode.Appraisal)
            {
                ring = _displayedAppraisalRings[first + index];
                if (ring == 0xff)
                    continue;
            }
            else
            {
                ring = first + index;
                if (!_inventory.HasAppraisedRing(ring))
                    continue;
            }
            DrawRingGraphic(output, ring, ListRingPosition(index) + new Vector2(xOffset, 0));
        }
    }

    private void DrawRingBox(Image output)
    {
        DrawRingGraphic(output, 0x40 + Math.Clamp(_inventory.RingBoxLevel, 1, 3),
            new Vector2(8, 0), preserveBoxGraphic: true);
        for (int slot = 0; slot < 5; slot++)
        {
            int ring = slot < _inventory.RingBoxCapacity
                ? _inventory.RingAt(slot)
                : 0xff;
            if (ring != 0xff)
                DrawRingGraphic(output, ring, BoxRingPosition(slot));
        }
    }

    private void DrawPageCounter(Image output, int page, float xOffset)
    {
        DrawVramBackgroundTile(output, 0, 0x11 + page, 0x07,
            new Vector2(120 + xOffset, 80));
        DrawVramBackgroundTile(output, 0, 0x10 + PageCount, 0x07,
            new Vector2(136 + xOffset, 80));
    }

    private void DrawRingNumber(Image output)
    {
        int selected = _displayedRing;
        if (selected == 0xff)
        {
            DrawVramBackgroundTile(output, 0, 0xe8, 0x07, new Vector2(32, 80));
            DrawVramBackgroundTile(output, 0, 0xe8, 0x07, new Vector2(40, 80));
            return;
        }
        int number = selected + 1;
        DrawVramBackgroundTile(output, 0, 0x10 + number / 10, 0x07, new Vector2(32, 80));
        DrawVramBackgroundTile(output, 0, 0x10 + number % 10, 0x07, new Vector2(40, 80));
    }

    private void DrawRingName(Image output)
    {
        if (_ringName.Length == 0) return;
        Vector2 position = RingNamePosition(_ringName.Length);
        // BuildMonochromeFontTexture produces white glyphs with binary alpha.
        // BG0's ink is opaque, so the original Lerp/SetPixel is an exact RGBA
        // replacement. Keep the composed background and upload once.
        using Image colors = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        colors.SetPixel(0, 0, _bgPalette[0, 2]);
        byte[] ink = colors.GetData();
        byte[] pixels = output.GetData();
        for (int index = 0; index < _ringName.Length; index++)
        {
            int glyph = _ringName[index] <= 0xff ? _ringName[index] : 0x3f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int read = ((glyph >> 4) * 16 + y) * _fontStride + ((glyph & 0x0f) * 8 + x) * 4;
                int destinationX = (int)position.X + index * 8 + x;
                int destinationY = (int)position.Y + y;
                if (_fontPixels[read + 3] == 0 || (uint)destinationX >= 160 || (uint)destinationY >= 144) continue;
                ink.AsSpan().CopyTo(pixels.AsSpan((destinationY * 160 + destinationX) * 4, 4));
            }
        }
        output.SetData(160, 144, false, Image.Format.Rgba8, pixels);
    }

    // Capture OAM at the native dispatch boundary. Box markers precede input;
    // list arrows/cursor follow ordinary list navigation, but not A/B/Select.
    internal void BeginSpriteUpdate(bool scrolling)
    {
        _spriteOam.Clear();
        if (Mode != RingMenuMode.List) return;
        SubmitBoxCursor();
        SubmitEquippedMarker();
        if (!scrolling) SubmitListMarkers();
    }

    private void AddSprite(MenuOamPart part, int yOffset = 0, int xOffset = 0) =>
        _spriteOam.Add(part with { Y = (part.Y + yOffset) & 0xff, X = (part.X + xOffset) & 0xff });

    private void DrawSubmittedSprites(Image output)
    {
        // Earlier OAM slots win pixel priority, so submit painter operations
        // in reverse order. This menu never reaches ten sprites on a scanline.
        for (int index = _spriteOam.Count - 1; index >= 0; index--)
        {
            MenuOamPart part = _spriteOam[index];
            DrawRawOamTile(output, 0, part.Tile, part.Attributes & 7,
                FileMenuPresentation.OamScreenPosition(part),
                flipX: (part.Attributes & 0x20) != 0);
        }
    }

    private void SubmitListMarkers()
    {
        int first = Page * 16;
        for (int slot = 4; slot >= 0; slot--)
        {
            int ring = _inventory.RingAt(slot);
            if (ring < first || ring >= first + 16)
                continue;
            int index = ring - first;
            MenuOamPart marker = _layouts.RingOam("list-box-marker")[0];
            AddSprite(marker, yOffset: index < 8 ? 0x30 : 0x48,
                xOffset: (index & 7) * 16);
        }
    }

    private void SubmitEquippedMarker()
    {
        if (_inventory.ActiveRing == 0xff)
            return;
        for (int slot = 4; slot >= 0; slot--)
        {
            if (_inventory.RingAt(slot) != _inventory.ActiveRing)
                continue;
            MenuOamPart marker = _layouts.RingOam("equipped-marker")[0];
            AddSprite(marker, xOffset: _layouts.RingBoxOffsets[slot].XOffset);
            break;
        }
    }

    private void SubmitBoxCursor()
    {
        if ((_boxCursorFlickerCounter & 0x80) != 0)
        {
            _boxCursorFlickerCounter = (_boxCursorFlickerCounter + 1) & 0xef;
            if ((_boxCursorFlickerCounter & 0x08) != 0) return;
        }
        MenuOamPart cursor = _layouts.RingOam("box-cursor")[0];
        AddSprite(cursor, xOffset: _layouts.RingBoxOffsets[BoxCursor].XOffset);
    }

    internal void SubmitListSprites()
    {
        // ringMenu_drawSprites skips arrows only for exactly one page.
        if (PageCount != 1)
            foreach (MenuOamPart arrow in _layouts.RingOam("page-arrows")) AddSprite(arrow);
        _listCursorFlickerCounter = (_listCursorFlickerCounter + 1) & 0xff;
        if ((_listCursorFlickerCounter & 0x08) == 0)
        {
            // A horizontal wrap writes the incoming cursor before drawing
            // OAM, on the dispatch which selects the page-scroll state.
            int index = PageTransitionActive ? _transitionCursor : ListCursor;
            MenuOamPart cursor = _layouts.RingOam("list-cursor")[0];
            AddSprite(cursor, yOffset: index < 8 ? 0x3e : 0x56,
                xOffset: 0x20 + (index & 7) * 16);
        }
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
        Image output, int graphic, Vector2 position, bool preserveBoxGraphic = false)
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
            DrawVramBackgroundTile(output, (flags & 0x08) != 0 ? 1 : 0, tile, flags,
                position + new Vector2((cell & 1) * 8, (cell >> 1) * 8));
        }
    }

    private void DrawRawOamTile(
        Image output, int bank, int tile, int palette, Vector2 position, bool flipX = false)
    {
        for (int half = 0; half < 2; half++)
        {
            if (!TrySelectVramTile(bank, (tile & 0xfe) + half, out Image source,
                out int sourceTile, out bool interleaved, out _, signedAddressing: false)) continue;
            Vector2I origin = SourceTileOrigin(source, sourceTile, interleaved);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int sx = flipX ? 7 - x : x;
                int shade = TwoBitShade(source.GetPixel(origin.X + sx, origin.Y + y));
                if (shade != 0 && palette < _spritePalette.GetLength(0))
                    SetMenuPixel(output, position + new Vector2(x, half * 8 + y),
                        _spritePalette[palette, shade]);
            }
        }
    }

    private void DrawVramBackgroundTile(Image output, int bank, int tile, int flags, Vector2 position)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int palette = flags & 7;
        if (!TrySelectVramTile(bank, tile, out Image source, out int sourceTile,
            out bool interleaved, out bool spriteEncoding, signedAddressing: false)) return;
        Vector2I origin = SourceTileOrigin(source, sourceTile, interleaved);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(origin.X + (flipX ? 7 - x : x), origin.Y + (flipY ? 7 - y : y));
            SetMenuPixel(output, position + new Vector2(x, y),
                _bgPalette[palette, PaletteShade(pixel, spriteEncoding)]);
        }
    }

    private static void SetMenuPixel(Image output, Vector2 position, Color color)
    {
        int x = (int)position.X, y = (int)position.Y;
        if ((uint)x < 160 && (uint)y < 144) output.SetPixel(x, y, color);
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

    private bool TrySelectVramTile(
        int bank, int tile, out Image source, out int sourceTile,
        out bool interleaved, out bool spriteEncoding, bool signedAddressing)
    {
        OracleVramSource[] candidates = bank == 1
            ? (signedAddressing ? _signedBank1 : [])
            : Mode == RingMenuMode.Appraisal
                ? (signedAddressing
                    ? _appraisalSignedBank0
                    : _appraisalUnsignedBank0)
                : (signedAddressing ? _listSignedBank0 : _listUnsignedBank0);
        if (!OracleTileRenderer.TrySelectVramTile(
            candidates, tile, out OracleVramSource result, out sourceTile))
        {
            source = _inventoryHud1;
            interleaved = false;
            spriteEncoding = false;
            return false;
        }
        source = result.Image;
        interleaved = result.Interleaved;
        spriteEncoding = result.SpriteEncoding;
        return true;
    }

    private static Vector2 ListRingPosition(int index) =>
        new(16 + (index & 7) * 16, index < 8 ? 32 : 56);

    // ringMenu_drawSprites adds its BC row/column offset to the imported
    // @cursorSprite bytes before the hardware OAM X/Y bias is applied.
    private static Vector2 ListCursorPosition(int index)
    {
        MenuOamPart cursor =
            MenuPresentationDatabase.Shared.RingOam("list-cursor")[0];
        return FileMenuPresentation.OamScreenPosition(
            cursor,
            yOffset: index < 8 ? 0x3e : 0x56,
            xOffset: 0x20 + (index & 7) * 16);
    }

    private static Vector2 RingNamePosition(int length) =>
        new(16 + Math.Max(0, (RingNameColumns - length) / 2) * 8, RingNameY);

    private bool UsesSignedBackgroundTiles(int y) => Mode == RingMenuMode.Appraisal
        ? y >= 88 // lcdInterrupt_ringMenu after LYC $57
        : y >= 72; // lcdInterrupt_ringMenu after LYC $47

    private static Vector2 BoxRingPosition(int slot) => new(40 + slot * 24, 0);

    private void OnInventoryChanged() => QueueRedraw();
}

internal enum RingMenuMode
{
    Appraisal,
    List
}
