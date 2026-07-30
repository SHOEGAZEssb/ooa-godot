using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;

/// <summary>
/// The three Start-menu inventory subscreens. Backgrounds come from the
/// original GFXH_INVENTORY_SCREEN / GFXH_INVENTORY_SUBSCREEN_* VRAM records;
/// live item, ring, essence, heart-piece, and era state is layered over them.
/// </summary>
public partial class InventoryScreen : Node2D
{
    private enum ItemSubmenuKind
    {
        None,
        SatchelSeeds,
        ShooterSeeds,
        SlingshotSeeds,
        HarpSongs
    }

    public const int PageScrollUpdates = 13;
    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int ScreenRows = 18;
    private const float PageScrollPixelsPerUpdate = 12.0f;
    private const int InventoryTextColumns = 16;
    private const int InventoryTextY = 15 * 8;
    private const int InventoryTextInitialPauseUpdates = 40;
    private const int InventoryTextScrollIntervalUpdates = 8;

    private Texture2D[] _backgrounds = null!;
    private Image _hudTiles = null!;
    private Image _partialHearts = null!;
    private Image _inventoryHud1 = null!;
    private Image _presentPastSymbols = null!;
    private Image _questItems5 = null!;
    private Image _mapCompassItems = null!;
    private Image _saveTiles = null!;
    private Image _blankTiles = null!;
    private Image _ringTiles = null!;
    private Image _inventoryHud2 = null!;
    private Image _itemIcons1 = null!;
    private Image _equippedItemIcons1 = null!;
    private Image _itemIcons2 = null!;
    private Image _itemIcons3 = null!;
    private Image _essenceTiles = null!;
    private Image[] _questItemTiles = null!;
    private Texture2D _fontTexture = null!;
    private OracleVramSource[] _bank0Sources = null!;
    private OracleVramSource[] _bank1Sources = null!;
    private byte[] _ringMap = null!;
    private Color[,] _bgPalette = null!;
    private Color[,] _spritePalette = null!;
    private TreasureDatabase _treasures = null!;
    private MenuPresentationDatabase _layouts = null!;
    private InventoryState _inventory = null!;
    private Func<bool> _isPast = null!;
    private int _itemCursor;
    private int _secondaryCursor;
    private int _essenceCursor;
    private int _rightCursor;
    private bool _rightSide;
    private InventorySubscreen _subscreen;
    private InventorySubscreen _nextSubscreen;
    private float _pageScrollFrame;
    private readonly FixedUpdateAccumulator _inventoryTextUpdates = new();
    private readonly FixedUpdateAccumulator _itemSubmenuUpdates = new();
    private readonly int[] _inventoryTextWindow = new int[InventoryTextColumns];
    private int[] _inventoryTextName = Array.Empty<int>();
    private int[] _inventoryTextDescription = Array.Empty<int>();
    private InventoryTextPhase _inventoryTextPhase;
    private int _inventoryTextKey;
    private int _inventoryTextTimer;
    private int _inventoryTextCursor;
    private int _inventoryTextSpaceCounter;
    private bool _itemSubmenuActive;
    private bool _itemSubmenuReady;
    private bool _itemSubmenuEquipToA;
    private ItemSubmenuKind _itemSubmenuKind;
    private int[] _itemSubmenuOptions = Array.Empty<int>();
    private int _itemSubmenuIndex;
    private int _itemSubmenuWidth;
    private int _itemSubmenuHeight;
    private int _itemSubmenuOpenUpdate;

    public int Cursor => _itemCursor;
    public int ActiveCursor => _subscreen switch
    {
        InventorySubscreen.Items => _itemCursor,
        InventorySubscreen.SecondaryItems => _secondaryCursor,
        _ => _rightSide ? 0x80 | _rightCursor : _essenceCursor
    };
    public InventorySubscreen Subscreen => _subscreen;
    public bool PageTransitionActive => _pageScrollFrame > 0.0f;
    public bool ItemSubmenuActive => _itemSubmenuActive;
    public bool ItemSubmenuReady => _itemSubmenuReady;
    internal int ItemSubmenuIndex => _itemSubmenuIndex;
    internal int ItemSubmenuWidth => _itemSubmenuWidth;
    internal int ItemSubmenuHeight => _itemSubmenuHeight;
    internal int ItemSubmenuOptionForValidation =>
        _itemSubmenuOptions.Length == 0
            ? -1
            : _itemSubmenuOptions[_itemSubmenuIndex];
    public bool SaveAndQuitSelected =>
        _subscreen == InventorySubscreen.EssencesAndSave && _rightSide && _rightCursor == 2;
    internal int ActiveTextKey => _inventoryTextKey;
    internal string VisibleTextForValidation => string.Create(
        InventoryTextColumns,
        _inventoryTextWindow,
        static (characters, glyphs) =>
        {
            for (int index = 0; index < glyphs.Length; index++)
                characters[index] = CharacterForInventoryGlyph(glyphs[index]);
        });
    internal (int SymbolTile, int DigitTile, int Attributes, Vector2 Offset)?
        LevelOverlayForValidation(int item)
    {
        DisplayRecord display = _treasures.GetButtonDisplay(item, _inventory);
        return TryGetLevelOverlay(display, out int level)
            ? (0x1a, 0x10 + (level & 0x0f), 0x07, new Vector2(8, 8))
            : null;
    }
    internal (int TensTile, int OnesTile, int Attributes, Vector2 Offset)?
        QuantityOverlayForValidation(int item)
    {
        DisplayRecord display = _treasures.GetButtonDisplay(item, _inventory);
        if (display.ExtraMode != 1)
            return null;
        int amount = _inventory.BcdAmountForInventoryDisplay(display.TreasureId);
        return (0x10 + ((amount >> 4) & 0x0f), 0x10 + (amount & 0x0f),
            0x07, new Vector2(8, 8));
    }
    internal ulong StoredItemIconSheet1HashForValidation =>
        OracleGraphicsCache.PixelHash(_itemIcons1);
    internal ulong EquippedItemIconSheet1HashForValidation =>
        OracleGraphicsCache.PixelHash(_equippedItemIcons1);
    internal ulong EquippedItemIconShadeHashForValidation(int sprite)
    {
        if (!ItemIconAtlas.Select(
                sprite, _equippedItemIcons1, _itemIcons2, _itemIcons3,
                out Image source, out int cell))
        {
            return 0;
        }
        return ItemIconAtlas.DecodedCellHash(source, cell);
    }
    internal ulong SecondaryCursorPixelHashForValidation(int index)
    {
        if (index < 0 || index >= _layouts.SecondaryCursors.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        Image output = Image.CreateEmpty(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight,
            false,
            Image.Format.Rgba8);
        output.Fill(Colors.Transparent);
        int packed = _layouts.SecondaryCursors[index].Packed;
        int rawY = (packed >> 4) * 8;
        int rawX = (packed & 0x0f) * 8;
        int leftOffset = index == 15 ? 12 : 8;
        int rightOffset = index is 4 or 9 or 14 ? 40
            : index == 15 ? 36 : 32;
        BlitRawOamTile(
            output,
            0,
            0x0c,
            2,
            new Vector2I(rawX + leftOffset - 8, rawY - 16),
            flipX: true);
        BlitRawOamTile(
            output,
            0,
            0x0c,
            2,
            new Vector2I(rawX + rightOffset - 8, rawY - 16),
            flipX: false);
        return OracleGraphicsCache.PixelHash(output);
    }
    internal ulong SeedSubmenuSpritePixelHashForValidation(int seedType)
    {
        if (seedType < 0 ||
            seedType >= _layouts.InventorySeedSubmenuSprites.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(seedType));
        }
        InventorySeedSubmenuSprite seed =
            _layouts.InventorySeedSubmenuSprites[seedType];
        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        output.Fill(Colors.Transparent);
        BlitRawOamTile(
            output,
            seed.VramBank,
            seed.Tile,
            seed.Attributes & 7,
            Vector2I.Zero,
            flipX: (seed.Attributes & 0x20) != 0);
        return OracleGraphicsCache.PixelHash(output);
    }
    internal ulong PassiveTreasurePixelHashForValidation(int treasureId)
    {
        DisplayRecord display =
            _treasures.GetButtonDisplay(treasureId, _inventory);
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        output.Fill(Colors.Transparent);
        if (display.HasIcon)
        {
            BlitLogicalBackgroundSprite(
                output,
                display.LeftSprite,
                TreasureBackgroundAttributes(display.LeftPalette),
                Vector2I.Zero);
            if (display.RightSprite != 0)
            {
                BlitLogicalBackgroundSprite(
                    output,
                    display.RightSprite,
                    TreasureBackgroundAttributes(display.RightPalette),
                    new Vector2I(8, 0));
            }
        }
        return OracleGraphicsCache.PixelHash(output);
    }
    internal Color EquippedLevelSymbolBackgroundColorForValidation =>
        HudBackgroundTileColor(0x1a, 0, 0);
    internal (int NormalAttributes, int FlippedAttributes, Color Shade2, Color Shade3)
        HeartPieceDisplayForValidation
    {
        get
        {
            int normal = TreasureBackgroundAttributes(0x05);
            return (normal, TreasureBackgroundAttributes(0x25),
                _bgPalette[normal & 7, 2], _bgPalette[normal & 7, 3]);
        }
    }

    public override void _Ready()
    {
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _partialHearts = LoadPng("res://assets/oracle/gfx/gfx_partial_hearts.png");
        _inventoryHud1 = LoadPng("res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        _presentPastSymbols = LoadPng("res://assets/oracle/inventory/spr_present_past_symbols.png");
        _questItems5 = LoadPng("res://assets/oracle/inventory/spr_quest_items_5.png");
        _mapCompassItems = LoadPng("res://assets/oracle/inventory/spr_map_compass_keys_bookofseals.png");
        _saveTiles = LoadPng("res://assets/oracle/inventory/gfx_save.png");
        _blankTiles = LoadPng("res://assets/oracle/inventory/gfx_blank.png");
        _ringTiles = LoadPng("res://assets/oracle/inventory/gfx_rings.png");
        _inventoryHud2 = LoadPng("res://assets/oracle/inventory/gfx_inventory_hud_2.png");
        // GFXH_INVENTORY_SCREEN loads the distinct BG-encoded first sheet;
        // equipped A/B sprites continue to copy from spr_item_icons_1.
        _itemIcons1 = LoadPng("res://assets/oracle/gfx/spr_item_icons_1_spr.png");
        _equippedItemIcons1 = LoadPng("res://assets/oracle/gfx/spr_item_icons_1.png");
        _itemIcons2 = LoadPng("res://assets/oracle/gfx/spr_item_icons_2.png");
        _itemIcons3 = LoadPng("res://assets/oracle/gfx/spr_item_icons_3.png");
        _essenceTiles = LoadPng("res://assets/oracle/inventory/spr_essences.png");
        _fontTexture = OracleTileRenderer.BuildMonochromeFontTexture("res://assets/oracle/gfx/gfx_font.png");
        _questItemTiles = new Image[4];
        for (int sheet = 0; sheet < _questItemTiles.Length; sheet++)
            _questItemTiles[sheet] = LoadPng($"res://assets/oracle/inventory/spr_quest_items_{sheet + 1}.png");
        _ringMap = ReadBytes("res://assets/oracle/inventory/map_rings.bin", 68 * 8);
        _bgPalette = LoadPalette("res://assets/oracle/inventory/palette_bg.bin", 8, 0);
        _spritePalette = ItemIconAtlas.LoadStandardSpritePalettes();

        _bank0Sources = new[]
        {
            new OracleVramSource(0x00, _inventoryHud1, false),
            new OracleVramSource(0x30, _presentPastSymbols, true, true),
            new OracleVramSource(0x40, _questItems5, true, true),
            new OracleVramSource(0x60, _mapCompassItems, true, true),
            new OracleVramSource(0x60, _saveTiles, false),
            new OracleVramSource(0x80, _blankTiles, false),
            new OracleVramSource(0xa0, _ringTiles, true),
            new OracleVramSource(0xe0, _inventoryHud2, false)
        };
        _bank1Sources = new[]
        {
            new OracleVramSource(0x00, _itemIcons1, true, true),
            new OracleVramSource(0x20, _itemIcons2, true, true),
            new OracleVramSource(0x40, _itemIcons3, true, true),
            new OracleVramSource(0x60, _essenceTiles, true, true),
            new OracleVramSource(0x80, _questItemTiles[0], true, true),
            new OracleVramSource(0xa0, _questItemTiles[1], true, true),
            new OracleVramSource(0xc0, _questItemTiles[2], true, true),
            new OracleVramSource(0xe0, _questItemTiles[3], true, true)
        };
    }

    public void Initialize(TreasureDatabase treasures, InventoryState inventory, Func<bool>? isPast = null)
    {
        if (_inventory is not null)
            _inventory.Changed -= OnInventoryChanged;
        _treasures = treasures;
        _layouts = MenuPresentationDatabase.Shared;
        _inventory = inventory;
        _isPast = isPast ?? (() => false);
        _inventory.Changed += OnInventoryChanged;
    }

    public override void _ExitTree()
    {
        if (_inventory is not null)
            _inventory.Changed -= OnInventoryChanged;
    }

    public void Open()
    {
        _itemCursor = 0;
        _secondaryCursor = 0;
        _essenceCursor = 0;
        _rightCursor = 0;
        _rightSide = false;
        _subscreen = InventorySubscreen.Items;
        _pageScrollFrame = 0.0f;
        ResetItemSubmenu();
        _inventoryTextKey = -1;
        SetInventoryText(0);
        _backgrounds = new[] { BuildBackgroundTexture(0), BuildBackgroundTexture(1), BuildBackgroundTexture(2) };
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        _pageScrollFrame = 0.0f;
        ResetItemSubmenu();
    }

    public void BeginNextSubscreen()
    {
        if (PageTransitionActive || ItemSubmenuActive)
            return;
        _nextSubscreen = (InventorySubscreen)(((int)_subscreen + 1) % 3);
        _pageScrollFrame = float.Epsilon;
        SetInventoryText(0);
        QueueRedraw();
    }

    public void UpdatePageTransition(double delta)
    {
        if (!PageTransitionActive)
            return;
        _pageScrollFrame += (float)(delta * 60.0);
        if (_pageScrollFrame >= PageScrollUpdates)
        {
            _subscreen = _nextSubscreen;
            _pageScrollFrame = 0.0f;
        }
        QueueRedraw();
    }

    public bool MoveCursor(Vector2I direction)
    {
        if (ItemSubmenuActive)
            return false;
        if (direction is not { X: 1, Y: 0 } and not { X: -1, Y: 0 } and
            not { X: 0, Y: 1 } and not { X: 0, Y: -1 })
        {
            return false;
        }

        switch (_subscreen)
        {
            case InventorySubscreen.Items:
                int offset = direction switch
                {
                    { X: 1 } => 1, { X: -1 } => -1,
                    { Y: -1 } => -4, { Y: 1 } => 4, _ => 0
                };
                _itemCursor = (_itemCursor + offset) & 0x0f;
                break;
            case InventorySubscreen.SecondaryItems:
                MoveSecondaryCursor(direction);
                break;
            case InventorySubscreen.EssencesAndSave:
                MoveEssenceCursor(direction);
                break;
        }
        RefreshSelectedText();
        QueueRedraw();
        return true;
    }

    public bool EquipToA()
    {
        if (_subscreen != InventorySubscreen.Items)
            return false;
        if (TryBeginItemSubmenu(isA: true))
            return false;
        _inventory.SwapStorageSlotWithButton(_itemCursor, isA: true);
        RefreshSelectedText();
        QueueRedraw();
        return true;
    }

    public bool EquipToB()
    {
        if (_subscreen != InventorySubscreen.Items)
            return false;
        if (TryBeginItemSubmenu(isA: false))
            return false;
        _inventory.SwapStorageSlotWithButton(_itemCursor, isA: false);
        RefreshSelectedText();
        QueueRedraw();
        return true;
    }

    public bool EquipSelectedRing()
    {
        if (_subscreen != InventorySubscreen.SecondaryItems || _secondaryCursor < 16)
            return false;
        bool equipped = _inventory.EquipRingAt(_secondaryCursor - 16);
        if (equipped)
            QueueRedraw();
        return equipped;
    }

    public override void _Draw()
    {
        if (!Visible || _inventory == null || _treasures == null)
            return;
        if (PageTransitionActive)
        {
            float pixels = Math.Min(OracleRoomData.ViewportWidth,
                MathF.Ceiling(_pageScrollFrame) * PageScrollPixelsPerUpdate);
            DrawSubscreen(_subscreen, new Vector2(-pixels, 0), drawCursor: false);
            DrawSubscreen(_nextSubscreen,
                new Vector2(OracleRoomData.ViewportWidth - pixels, 0), drawCursor: false);
            return;
        }
        DrawSubscreen(
            _subscreen,
            Vector2.Zero,
            drawCursor: !ItemSubmenuActive);
        if (ItemSubmenuActive)
            DrawItemSubmenu();
    }

    private void DrawSubscreen(InventorySubscreen page, Vector2 drawOffset, bool drawCursor)
    {
        DrawTexture(_backgrounds[(int)page], drawOffset);
        DrawInventoryText(drawOffset);
        DrawTreasure(_treasures.GetButtonDisplay(_inventory.EquippedB, _inventory),
            new Vector2(
                _inventory.EquippedB == InventoryState.ItemHarp ? 16 : 8,
                0) + drawOffset,
            spritePalette: true);
        DrawTreasure(_treasures.GetButtonDisplay(_inventory.EquippedA, _inventory),
            new Vector2(
                _inventory.EquippedA == InventoryState.ItemHarp ? 56 : 48,
                0) + drawOffset,
            spritePalette: true);

        switch (page)
        {
            case InventorySubscreen.Items:
                int harpSlot = -1;
                for (int index = 0; index < InventoryState.InventoryCapacity; index++)
                {
                    int item = _inventory.StorageItemAt(index);
                    DrawTreasure(_treasures.GetButtonDisplay(_inventory.StorageItemAt(index), _inventory),
                        ItemSlotPosition(index) + drawOffset, spritePalette: false);
                    if (item == InventoryState.ItemHarp)
                        harpSlot = index;
                }
                if (harpSlot >= 0)
                    DrawStoredHarpSprite(ItemSlotPosition(harpSlot) + drawOffset);
                if (drawCursor)
                    DrawItemCursor(ItemSlotPosition(_itemCursor));
                break;
            case InventorySubscreen.SecondaryItems:
                DrawPassiveTreasures(drawOffset);
                DrawRings(drawOffset);
                if (drawCursor)
                    DrawSecondaryCursor();
                break;
            case InventorySubscreen.EssencesAndSave:
                DrawEraSymbol(drawOffset);
                DrawHeartPieces(drawOffset);
                if (drawCursor)
                    DrawEssenceCursor();
                break;
        }
    }

    private void DrawPassiveTreasures(Vector2 drawOffset)
    {
        PassiveTreasureLayout?[] selected = SelectPassiveTreasures();
        foreach (PassiveTreasureLayout? treasure in selected)
        {
            if (treasure is not PassiveTreasureLayout value ||
                value.TreasureId == 0x36)
                continue;
            DrawTreasure(_treasures.GetButtonDisplay(
                    value.TreasureId, _inventory),
                Slot(0x62 + (value.Position >> 4) * 0x20 + (value.Position & 0x0f)) + drawOffset,
                spritePalette: false);
        }
    }

    public void UpdateInventoryText(double delta)
    {
        if (PageTransitionActive || RefreshSelectedText())
            return;

        int updates = _inventoryTextUpdates.Consume(delta);
        for (int update = 0; update < updates; update++)
            TickInventoryText();
        if (updates > 0)
            QueueRedraw();
    }

    private bool RefreshSelectedText()
    {
        if (ItemSubmenuActive)
        {
            int submenuKey = 0;
            if (ItemSubmenuReady && _itemSubmenuOptions.Length != 0)
            {
                int option = _itemSubmenuOptions[_itemSubmenuIndex];
                int treasure = _itemSubmenuKind == ItemSubmenuKind.HarpSongs
                    ? TreasureDatabase.TreasureTuneOfEchoes + option - 1
                    : TreasureDatabase.TreasureEmberSeeds + option;
                submenuKey = _treasures
                    .GetButtonDisplay(treasure, _inventory)
                    .TextLow;
                if (_itemSubmenuKind is
                    ItemSubmenuKind.ShooterSeeds or
                    ItemSubmenuKind.SlingshotSeeds)
                {
                    // inventoryMenuState2 adds five to the seed text index for
                    // the Shooter and Slingshot displays.
                    submenuKey += 5;
                }
            }
            return SetInventoryText(submenuKey);
        }
        int key = _subscreen switch
        {
            InventorySubscreen.Items =>
                _treasures.GetButtonDisplay(_inventory.StorageItemAt(_itemCursor), _inventory).TextLow,
            InventorySubscreen.SecondaryItems => SecondaryTextKey(),
            InventorySubscreen.EssencesAndSave => EssenceTextKey(),
            _ => 0
        };
        return SetInventoryText(key);
    }

    public void UpdateItemSubmenu(double delta)
    {
        if (!ItemSubmenuActive || ItemSubmenuReady)
            return;
        int updates = _itemSubmenuUpdates.Consume(delta);
        int maxWidth = _layouts
            .InventoryItemSubmenu(_itemSubmenuOptions.Length)
            .MaxWidth;
        for (int update = 0; update < updates; update++)
        {
            _itemSubmenuOpenUpdate++;
            if ((_itemSubmenuOpenUpdate & 1) == 0)
                continue;
            if (_itemSubmenuWidth < maxWidth)
                _itemSubmenuWidth = Math.Min(maxWidth, _itemSubmenuWidth + 2);
            else if (_itemSubmenuHeight < 4)
                _itemSubmenuHeight++;
            else
            {
                _itemSubmenuReady = true;
                RefreshSelectedText();
                break;
            }
        }
        if (updates > 0)
            QueueRedraw();
    }

    public bool MoveItemSubmenu(int direction)
    {
        if (!ItemSubmenuReady || direction is not (-1 or 1))
            return false;
        _itemSubmenuIndex =
            (_itemSubmenuIndex + direction + _itemSubmenuOptions.Length) %
            _itemSubmenuOptions.Length;
        RefreshSelectedText();
        QueueRedraw();
        return true;
    }

    public bool ConfirmItemSubmenu()
    {
        if (!ItemSubmenuReady || _itemSubmenuOptions.Length == 0)
            return false;
        int option = _itemSubmenuOptions[_itemSubmenuIndex];
        switch (_itemSubmenuKind)
        {
            case ItemSubmenuKind.SatchelSeeds:
                _inventory.SelectSatchelSeeds(option);
                break;
            case ItemSubmenuKind.ShooterSeeds:
                _inventory.SelectShooterSeeds(option);
                break;
            case ItemSubmenuKind.SlingshotSeeds:
                _inventory.SelectSlingshotSeeds(option);
                break;
            case ItemSubmenuKind.HarpSongs:
                _inventory.SelectHarpSong(option);
                break;
            default:
                throw new InvalidOperationException(
                    "Active inventory item submenu has no selection kind.");
        }
        _inventory.SwapStorageSlotWithButton(
            _itemCursor, _itemSubmenuEquipToA);
        ResetItemSubmenu();
        RefreshSelectedText();
        QueueRedraw();
        return true;
    }

    private bool TryBeginItemSubmenu(bool isA)
    {
        int selected;
        int item = _inventory.StorageItemAt(_itemCursor);
        int[] options;
        ItemSubmenuKind kind;
        switch (item)
        {
            case InventoryState.ItemSeedSatchel:
                kind = ItemSubmenuKind.SatchelSeeds;
                options = _inventory.ObtainedSeedTypes();
                selected = _inventory.SatchelSelectedSeeds;
                break;
            case TreasureDatabase.TreasureShooter:
                kind = ItemSubmenuKind.ShooterSeeds;
                options = _inventory.ObtainedSeedTypes();
                selected = _inventory.ShooterSelectedSeeds;
                break;
            case TreasureDatabase.TreasureSlingshot:
                kind = ItemSubmenuKind.SlingshotSeeds;
                options = _inventory.ObtainedSeedTypes();
                selected = _inventory.SlingshotSelectedSeeds;
                break;
            case InventoryState.ItemHarp:
                kind = ItemSubmenuKind.HarpSongs;
                options = _inventory.ObtainedHarpSongs();
                selected = _inventory.SelectedHarpSong;
                break;
            default:
                return false;
        }
        if (options.Length < 2)
            return false;

        _itemSubmenuActive = true;
        _itemSubmenuReady = false;
        _itemSubmenuEquipToA = isA;
        _itemSubmenuKind = kind;
        _itemSubmenuOptions = options;
        _itemSubmenuIndex = Array.IndexOf(options, selected);
        if (_itemSubmenuIndex < 0)
            _itemSubmenuIndex = 0;
        // The equip input only selects inventoryMenuState2. Its first update
        // initializes substate 0 and falls through to draw two columns.
        _itemSubmenuWidth = 0;
        _itemSubmenuHeight = 1;
        _itemSubmenuOpenUpdate = 0;
        _itemSubmenuUpdates.Reset();
        SetInventoryText(0);
        QueueRedraw();
        return true;
    }

    private void ResetItemSubmenu()
    {
        _itemSubmenuActive = false;
        _itemSubmenuReady = false;
        _itemSubmenuEquipToA = false;
        _itemSubmenuKind = ItemSubmenuKind.None;
        _itemSubmenuOptions = Array.Empty<int>();
        _itemSubmenuIndex = 0;
        _itemSubmenuWidth = 0;
        _itemSubmenuHeight = 0;
        _itemSubmenuOpenUpdate = 0;
        _itemSubmenuUpdates.Reset();
    }

    private void DrawItemSubmenu()
    {
        bool above = _itemCursor >= 8;
        float targetY = above ? 32 : 72;
        float width = _itemSubmenuWidth * 8;
        float height = _itemSubmenuHeight * 8;
        Vector2 panelPosition = new(80 - width / 2, targetY);
        int flags = _itemSubmenuHeight == 4 ? 0x01 : 0x81;
        for (int y = 0; y < _itemSubmenuHeight; y++)
        for (int x = 0; x < _itemSubmenuWidth; x++)
        {
            DrawVramBackgroundTile(
                0x01,
                flags,
                panelPosition + new Vector2(x * 8, y * 8));
        }
        if (!ItemSubmenuReady)
            return;

        InventoryItemSubmenuLayout layout =
            _layouts.InventoryItemSubmenu(_itemSubmenuOptions.Length);
        for (int index = 0; index < _itemSubmenuOptions.Length; index++)
        {
            int baseX = layout.Positions[index].RawX * 8;
            int option = _itemSubmenuOptions[index];
            if (_itemSubmenuKind == ItemSubmenuKind.HarpSongs)
            {
                DrawTreasure(
                    _treasures.GetHarpSongDisplay(option),
                    new Vector2(baseX, targetY + 4),
                    spritePalette: true,
                    drawEquippedExtra: false);
                continue;
            }

            InventorySeedSubmenuSprite seed =
                _layouts.InventorySeedSubmenuSprites[option];
            DrawRawOamTile(
                seed.VramBank,
                seed.Tile,
                seed.Attributes & 7,
                new Vector2(
                    baseX + seed.X - 8,
                    targetY + seed.Y - 16));
            int amount = _inventory.BcdAmountForInventoryDisplay(
                TreasureDatabase.TreasureEmberSeeds + option);
            DrawVramBackgroundTile(
                0x20 + ((amount >> 4) & 0x0f),
                0x01,
                new Vector2(baseX, targetY + 16));
            DrawVramBackgroundTile(
                0x20 + (amount & 0x0f),
                0x01,
                new Vector2(baseX + 8, targetY + 16));
        }
        int cursorX = layout.Positions[_itemSubmenuIndex].RawX * 8;
        DrawRawOamTile(
            0,
            0x0e,
            3,
            new Vector2(cursorX + 4, targetY + 24));
    }

    private int SecondaryTextKey()
    {
        if (_secondaryCursor >= 16)
        {
            int ring = _inventory.RingAt(_secondaryCursor - 16);
            return ring == 0xff ? 0 : 0xc0 | (ring & 0x3f);
        }
        if (_secondaryCursor == 15)
            return _inventory.RingBoxLevel == 0 ? 0 : 0x1c + _inventory.RingBoxLevel;

        PassiveTreasureLayout? treasure =
            SelectPassiveTreasures()[_secondaryCursor];
        return treasure is PassiveTreasureLayout value
            ? _treasures.GetButtonDisplay(
                value.TreasureId, _inventory).TextLow
            : 0;
    }

    private int EssenceTextKey()
    {
        if (!_rightSide)
        {
            // inventorySubscreen2_drawTreasures clears w4SubscreenTextIndices
            // for each essence whose wEssencesObtained bit is unset.
            return (_inventory.Essences & (1 << _essenceCursor)) != 0
                ? 0x01 + _essenceCursor
                : 0;
        }
        return _rightCursor switch
        {
            0 => _isPast() ? 0x66 : 0x65,
            1 => 0x61 + Math.Clamp(_inventory.HeartPieces, 0, 4),
            _ => 0x60
        };
    }

    private PassiveTreasureLayout?[] SelectPassiveTreasures()
    {
        var selected = new PassiveTreasureLayout?[15];
        foreach (PassiveTreasureLayout treasure in _layouts.PassiveTreasures)
        {
            if (_inventory.HasTreasure(treasure.TreasureId))
                selected[treasure.Slot] = treasure;
        }
        return selected;
    }

    private bool SetInventoryText(int key)
    {
        if (_inventoryTextKey == key)
            return false;

        _inventoryTextKey = key;
        _inventoryTextUpdates.Reset();
        Array.Fill(_inventoryTextWindow, 0x20);
        InventoryTextRecord record = (key & 0x80) != 0
            ? _treasures.GetRingText(key & 0x3f)
            : _treasures.GetInventoryText(key);
        string message = DialogueBox.PlainText(record.Message).Replace("\r", string.Empty);
        int lineEnd = message.IndexOf('\n');
        string name = lineEnd < 0 ? message : message[..lineEnd];
        string description = lineEnd < 0 ? string.Empty : message[(lineEnd + 1)..];
        _inventoryTextName = InventoryGlyphs(name, InventoryTextColumns);
        _inventoryTextDescription = InventoryGlyphs(description.Replace('\n', ' '));
        _inventoryTextCursor = 0;
        _inventoryTextSpaceCounter = 0;

        if (_inventoryTextName.Length == 0)
        {
            _inventoryTextPhase = InventoryTextPhase.Hidden;
            _inventoryTextTimer = 0;
            QueueRedraw();
            return true;
        }

        int leftPadding = (InventoryTextColumns - _inventoryTextName.Length) / 2;
        Array.Copy(_inventoryTextName, 0, _inventoryTextWindow, leftPadding,
            _inventoryTextName.Length);
        _inventoryTextPhase = InventoryTextPhase.NamePause;
        _inventoryTextTimer = InventoryTextInitialPauseUpdates;
        QueueRedraw();
        return true;
    }

    private void TickInventoryText()
    {
        if (_inventoryTextPhase == InventoryTextPhase.Hidden || --_inventoryTextTimer > 0)
            return;

        if (_inventoryTextPhase == InventoryTextPhase.NamePause)
        {
            _inventoryTextPhase = InventoryTextPhase.Description;
            _inventoryTextCursor = 0;
            _inventoryTextTimer = 1;
            return;
        }

        _inventoryTextTimer = InventoryTextScrollIntervalUpdates;
        switch (_inventoryTextPhase)
        {
            case InventoryTextPhase.Description:
                if (_inventoryTextCursor < _inventoryTextDescription.Length)
                {
                    ShiftInventoryText(_inventoryTextDescription[_inventoryTextCursor++]);
                    return;
                }
                ShiftInventoryText(0x20);
                _inventoryTextSpaceCounter = 16;
                _inventoryTextPhase = InventoryTextPhase.TrailingSpaces;
                return;

            case InventoryTextPhase.TrailingSpaces:
                ShiftInventoryText(0x20);
                if (--_inventoryTextSpaceCounter == 0)
                {
                    _inventoryTextCursor = 0;
                    _inventoryTextPhase = InventoryTextPhase.NameReplay;
                }
                return;

            case InventoryTextPhase.NameReplay:
                if (_inventoryTextCursor < _inventoryTextName.Length)
                {
                    ShiftInventoryText(_inventoryTextName[_inventoryTextCursor++]);
                    return;
                }
                _inventoryTextCursor = 0;
                int spaces = InventoryTextColumns - _inventoryTextName.Length;
                if (spaces == 0)
                {
                    _inventoryTextPhase = InventoryTextPhase.FullNameLeadWait;
                    return;
                }
                ShiftInventoryText(0x20);
                _inventoryTextSpaceCounter = (spaces + 1) / 2;
                _inventoryTextPhase = InventoryTextPhase.NamePadding;
                return;

            case InventoryTextPhase.NamePadding:
                if (--_inventoryTextSpaceCounter > 0)
                {
                    ShiftInventoryText(0x20);
                    return;
                }
                _inventoryTextPhase = InventoryTextPhase.NamePause;
                _inventoryTextTimer = InventoryTextInitialPauseUpdates;
                return;

            case InventoryTextPhase.FullNameLeadWait:
                _inventoryTextPhase = InventoryTextPhase.FullNamePause;
                _inventoryTextTimer = InventoryTextInitialPauseUpdates;
                return;

            case InventoryTextPhase.FullNamePause:
                ShiftInventoryText(0x20);
                _inventoryTextPhase = InventoryTextPhase.Description;
                return;
        }
    }

    private void ShiftInventoryText(int glyph)
    {
        Array.Copy(_inventoryTextWindow, 1, _inventoryTextWindow, 0,
            InventoryTextColumns - 1);
        _inventoryTextWindow[^1] = glyph;
    }

    private void DrawInventoryText(Vector2 drawOffset)
    {
        if (_inventoryTextPhase == InventoryTextPhase.Hidden)
            return;
        Color color = _bgPalette[1, 2];
        for (int column = 0; column < InventoryTextColumns; column++)
        {
            int glyph = _inventoryTextWindow[column];
            if (glyph == 0x20)
                continue;
            Rect2 source = new((glyph & 0x0f) * 8, (glyph >> 4) * 16, 8, 16);
            Rect2 destination = new(
                drawOffset + new Vector2(16 + column * 8, InventoryTextY),
                new Vector2(8, 16));
            DrawTextureRectRegion(_fontTexture, destination, source, color);
        }
    }

    private static int[] InventoryGlyphs(string text, int maximum = int.MaxValue)
    {
        int count = Math.Min(text.Length, maximum);
        var result = new int[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = text[index] switch
            {
                '♥' => 0x14,
                '↑' => 0x15,
                '↓' => 0x16,
                '←' => 0x17,
                '→' => 0x18,
                _ => text[index] <= 0xff ? text[index] : 0x3f
            };
        }
        return result;
    }

    private static char CharacterForInventoryGlyph(int glyph) => glyph switch
    {
        0x14 => '♥',
        0x15 => '↑',
        0x16 => '↓',
        0x17 => '←',
        0x18 => '→',
        _ => (char)glyph
    };

    private void DrawRings(Vector2 drawOffset)
    {
        if (_inventory.RingBoxCapacity == 0)
            return;
        DrawRingGraphic(0x40 + _inventory.RingBoxLevel, Slot(0x182) + drawOffset);
        for (int index = 0; index < _inventory.RingBoxCapacity; index++)
        {
            int ring = _inventory.RingAt(index);
            if (ring == 0xff)
                continue;
            DrawRingGraphic(ring, Slot(0x184 + index * 3) + drawOffset);
            if (ring == _inventory.ActiveRing)
                DrawRawOamTile(0, 0xec, 4, new Vector2(38 + index * 24, 94) + drawOffset);
        }
    }

    private void DrawRingGraphic(int graphic, Vector2 position)
    {
        int offset = graphic * 8;
        if (offset < 0 || offset + 7 >= _ringMap.Length)
            return;
        for (int cell = 0; cell < 4; cell++)
        {
            byte tile = _ringMap[offset + cell * 2];
            byte flags = _ringMap[offset + cell * 2 + 1];
            DrawVramBackgroundTile(tile, flags,
                position + new Vector2(cell % 2 * 8, cell / 2 * 8));
        }
    }

    private void DrawEraSymbol(Vector2 drawOffset)
    {
        int first = _isPast() ? 0x1c : 0x18;
        int attributes = 1 + (_isPast() ? 2 : 0);
        DrawTreasureBackgroundSprite(first, attributes, Slot(0x06e) + drawOffset);
        DrawTreasureBackgroundSprite(first + 1, attributes, Slot(0x06f) + drawOffset);
        DrawTreasureBackgroundSprite(first + 2, attributes, Slot(0x070) + drawOffset);
        DrawTreasureBackgroundSprite(first + 3, attributes, Slot(0x071) + drawOffset);
    }

    private void DrawHeartPieces(Vector2 drawOffset)
    {
        int count = Math.Clamp(_inventory.HeartPieces, 0, 3);
        if (count >= 1)
        {
            DrawTreasureBackgroundSprite(0x78, 0x05, Slot(0x0ce) + drawOffset);
            DrawTreasureBackgroundSprite(0x79, 0x05, Slot(0x0cf) + drawOffset);
        }
        if (count >= 2)
        {
            DrawTreasureBackgroundSprite(0x7a, 0x05, Slot(0x10e) + drawOffset);
            DrawTreasureBackgroundSprite(0x7b, 0x05, Slot(0x10f) + drawOffset);
        }
        if (count >= 3)
        {
            DrawTreasureBackgroundSprite(0x7b, 0x25, Slot(0x110) + drawOffset);
            DrawTreasureBackgroundSprite(0x7a, 0x25, Slot(0x111) + drawOffset);
        }
    }

    private void MoveSecondaryCursor(Vector2I direction)
    {
        int capacity = _inventory.RingBoxCapacity;
        int total = capacity == 0 ? 15 : 16 + capacity;
        if (direction.X != 0)
        {
            _secondaryCursor = (_secondaryCursor + direction.X + total) % total;
            return;
        }
        if (direction.Y == 0)
            return;
        if (_secondaryCursor >= 15)
        {
            int column = _secondaryCursor == 15 ? 0 : _secondaryCursor - 15;
            _secondaryCursor = direction.Y < 0 ? 10 + Math.Clamp(column - 1, 0, 4)
                : Math.Clamp(column - 1, 0, 4);
            return;
        }
        int next = _secondaryCursor + direction.Y * 5;
        if (next < 0)
            next += 15;
        else if (next >= 15)
        {
            if (capacity == 0)
                next -= 15;
            else
            {
                int column = _secondaryCursor - 10;
                int ringSlot = 16 + column;
                next = ringSlot < total ? ringSlot : column;
            }
        }
        _secondaryCursor = Math.Clamp(next, 0, total - 1);
    }

    private void MoveEssenceCursor(Vector2I direction)
    {
        if (direction.X != 0)
        {
            _rightSide = !_rightSide;
            return;
        }
        if (direction.Y == 0)
            return;
        if (_rightSide)
            _rightCursor = (_rightCursor + direction.Y + 3) % 3;
        else
            _essenceCursor = (_essenceCursor + direction.Y + 8) % 8;
    }

    private void DrawItemCursor(Vector2 position)
    {
        DrawRawOamTile(0, 0x0c, 2, position + new Vector2(-8, 0), flipX: true);
        DrawRawOamTile(0, 0x0c, 2, position + new Vector2(24, 0));
    }

    private void DrawSecondaryCursor()
    {
        int packed = _layouts.SecondaryCursors[
            Math.Min(_secondaryCursor, _layouts.SecondaryCursors.Count - 1)].Packed;
        float rawY = (packed >> 4) * 8;
        float rawX = (packed & 0x0f) * 8;
        int leftOffset = _secondaryCursor == 15 ? 12 : 8;
        int rightOffset = _secondaryCursor is 4 or 9 or 14 ? 40
            : _secondaryCursor == 15 ? 36 : 32;
        DrawRawOamTile(0, 0x0c, 2, new Vector2(rawX + leftOffset - 8, rawY - 16), flipX: true);
        DrawRawOamTile(0, 0x0c, 2, new Vector2(rawX + rightOffset - 8, rawY - 16));
    }

    private void DrawEssenceCursor()
    {
        int index = _rightSide ? 8 + _rightCursor : _essenceCursor;
        EssenceCursorLayout raw = _layouts.EssenceCursors[index];
        int rightOffset = _rightSide ? 40 : 24;
        DrawRawOamTile(0, 0x0c, 2,
            new Vector2(raw.RawX - 8, raw.RawY - 16), flipX: true);
        DrawRawOamTile(0, 0x0c, 2,
            new Vector2(raw.RawX + rightOffset - 8, raw.RawY - 16));
    }

    private Texture2D BuildBackgroundTexture(int page)
    {
        byte[] map = new byte[TilemapStride * ScreenRows];
        byte[] flags = new byte[TilemapStride * ScreenRows];
        Overlay(map, ReadBytes("res://assets/oracle/hud/map_hud_normal.bin", 64), 0x000);
        Overlay(flags, ReadBytes("res://assets/oracle/hud/flg_hud_normal.bin", 64), 0x000);
        switch (page)
        {
            case 0:
                Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_screen_1.bin", 416), 0x040);
                Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_screen_1.bin", 416), 0x040);
                break;
            case 1:
                Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_screen_1.bin", 416), 0x040, 32);
                Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_screen_1.bin", 416), 0x040, 32);
                Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_screen_2.bin", 384), 0x060);
                Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_screen_2.bin", 384), 0x060);
                ClearUnusedRingSlots(map, flags);
                break;
            case 2:
                Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_screen_3.bin", 416), 0x040);
                Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_screen_3.bin", 416), 0x040);
                for (int essence = 0;
                    essence < _layouts.EssenceTiles.Count;
                    essence++)
                {
                    if ((_inventory.Essences & (1 << essence)) == 0)
                    {
                        FillRectangle(
                            map,
                            flags,
                            _layouts.EssenceTiles[essence].TilemapOffset,
                            2, 2, 0x00, 0x07);
                    }
                }
                break;
        }
        Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_textbar.bin", 96), 0x1e0);
        Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_textbar.bin", 96), 0x1e0);
        map[0x0a] = 0x04;
        WriteRupeeDigits(map);
        WriteHearts(map);

        Image output = Image.CreateEmpty(160, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < ScreenRows; row++)
        for (int column = 0; column < ScreenColumns; column++)
        {
            int offset = row * TilemapStride + column;
            if (row < 2)
                DrawHudTileToImage(output, map[offset], flags[offset], column * 8, row * 8);
            else if (row is 15 or 16 && column is >= 2 and < 18)
                output.FillRect(new Rect2I(column * 8, row * 8, 8, 8), _bgPalette[1, 3]);
            else
                DrawVramTileToImage(output, (flags[offset] & 0x08) != 0 ? 1 : 0,
                    map[offset], flags[offset], column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private void ClearUnusedRingSlots(byte[] map, byte[] flags)
    {
        if (_inventory.RingBoxLevel >= 3)
            return;
        (int offset, int width) = _inventory.RingBoxLevel switch
        {
            1 => (0x187, 12), 2 => (0x18d, 6), _ => (0x181, 18)
        };
        FillRectangle(map, flags, offset, 3, width, 0xe7, 0x01);
    }

    private void WriteRupeeDigits(byte[] map)
    {
        int value = Math.Clamp(_inventory.Rupees, 0, 999);
        map[0x2a] = (byte)(0x10 + value / 100);
        map[0x2b] = (byte)(0x10 + value / 10 % 10);
        map[0x2c] = (byte)(0x10 + value % 10);
    }

    private void WriteHearts(byte[] map)
    {
        int containers = Math.Clamp((_inventory.MaxHealthQuarters + 3) / 4, 0, 7);
        int fullHearts = Math.Clamp(_inventory.HealthQuarters / 4, 0, containers);
        int partial = Math.Clamp(_inventory.HealthQuarters % 4, 0, 3);
        for (int heart = 0; heart < containers; heart++)
            map[0x0d + heart] = heart < fullHearts ? (byte)0x0a
                : heart == fullHearts && partial > 0 ? (byte)0x0b : (byte)0x09;
        for (int heart = containers; heart < 7; heart++)
            map[0x0d + heart] = 0;
    }

    private void DrawHudTileToImage(Image output, byte tile, byte flags, int x, int y)
    {
        Image source = _hudTiles;
        int sourceTile = tile;
        if (tile == 0x0b && _inventory.HealthQuarters % 4 is >= 1 and <= 3)
        {
            source = _partialHearts;
            sourceTile = _inventory.HealthQuarters % 4 - 1;
        }
        DrawTileToImage(output, source, sourceTile, flags, _bgPalette, x, y,
            interleaved: false, spriteEncoding: false);
    }

    private void DrawVramTileToImage(Image output, int bank, byte tile, byte flags, int x, int y)
    {
        if (!TrySelectVramTile(bank, tile, out Image source, out int sourceTile,
            out bool interleaved, out bool spriteEncoding))
            return;
        DrawTileToImage(output, source, sourceTile, flags, _bgPalette, x, y,
            interleaved, spriteEncoding);
    }

    private void DrawTreasure(
        DisplayRecord display,
        Vector2 position,
        bool spritePalette,
        bool drawEquippedExtra = true)
    {
        if (!display.HasIcon)
            return;
        if (spritePalette)
        {
            DrawLogicalOamSprite(display.LeftSprite,
                ItemIconAtlas.EquippedLeftPalette(display.LeftSprite, display.LeftPalette),
                position);
            if (display.RightSprite != 0)
                DrawLogicalOamSprite(display.RightSprite, display.RightPalette & 7, position + new Vector2(8, 0));
            if (drawEquippedExtra)
            {
                // updateStatusBar shifts only the Harp's OAM cells. Its
                // four fixed BG cells remain at the ordinary button origin.
                Vector2 extraPosition = position + new Vector2(
                    display.ExtraMode == 5 ? 0 : 8,
                    8);
                DrawTreasureLevel(display, extraPosition, equipped: true);
            }
            return;
        }
        DrawTreasureBackgroundSprite(display.LeftSprite, display.LeftPalette, position);
        if (display.RightSprite != 0)
            DrawTreasureBackgroundSprite(display.RightSprite, display.RightPalette,
                position + new Vector2(8, 0));
        // drawTreasureDisplayDataToBg leaves de on the right icon column
        // before adding one tilemap row for the extra display.
        DrawTreasureLevel(display, position + new Vector2(8, 8), equipped: false);
    }

    private void DrawStoredHarpSprite(Vector2 position)
    {
        DisplayRecord display =
            _treasures.GetHarpSongDisplay(_inventory.SelectedHarpSong);
        DrawLogicalOamSprite(
            display.LeftSprite,
            display.LeftPalette & 7,
            position + new Vector2(10, 0));
        DrawLogicalOamSprite(
            display.RightSprite,
            display.RightPalette & 7,
            position + new Vector2(18, 0));

        // The stored Harp is the inventory's one composite BG/OAM item.
        // Its BG priority tiles mask the nonzero portions of the song sprite.
        DrawVramBackgroundTile(0x1c, 0x84, position, priorityOnly: true);
        DrawVramBackgroundTile(
            0x1e, 0x84, position + new Vector2(8, 0), priorityOnly: true);
        DrawVramBackgroundTile(
            0x1d, 0x84, position + new Vector2(0, 8), priorityOnly: true);
        DrawVramBackgroundTile(
            0x1f, 0x84, position + new Vector2(8, 8), priorityOnly: true);
    }

    private void DrawTreasureLevel(
        DisplayRecord display,
        Vector2 position,
        bool equipped)
    {
        if (display.ExtraMode == 5)
        {
            if (equipped)
            {
                DrawHudBackgroundTile(
                    0x0c, position + new Vector2(-8, -8), priorityOnly: true);
                DrawHudBackgroundTile(
                    0x0e, position + new Vector2(0, -8), priorityOnly: true);
                DrawHudBackgroundTile(
                    0x0d, position + new Vector2(-8, 0), priorityOnly: true);
                DrawHudBackgroundTile(
                    0x0f, position, priorityOnly: true);
            }
            else
            {
                DrawVramBackgroundTile(
                    0x1c, 0x84, position + new Vector2(-8, -8));
                DrawVramBackgroundTile(
                    0x1e, 0x84, position + new Vector2(0, -8));
                DrawVramBackgroundTile(
                    0x1d, 0x84, position + new Vector2(-8, 0));
                DrawVramBackgroundTile(0x1f, 0x84, position);
            }
            return;
        }

        if (display.ExtraMode == 1)
        {
            int amount = _inventory.BcdAmountForInventoryDisplay(display.TreasureId);
            int tens = 0x10 + ((amount >> 4) & 0x0f);
            int ones = 0x10 + (amount & 0x0f);
            if (equipped)
            {
                DrawHudBackgroundTile(tens, position);
                DrawHudBackgroundTile(ones, position + new Vector2(8, 0));
            }
            else
            {
                DrawVramBackgroundTile(tens, 0x07, position);
                DrawVramBackgroundTile(ones, 0x07, position + new Vector2(8, 0));
            }
            return;
        }

        if (!TryGetLevelOverlay(display, out int level))
            return;

        // Equipped mode-$00 overlays still reference the common HUD tiles;
        // the inventory VRAM sheet encodes these tile numbers differently.
        if (equipped)
        {
            DrawHudBackgroundTile(0x1a, position);
            DrawHudBackgroundTile(0x10 + (level & 0x0f), position + new Vector2(8, 0));
        }
        else
        {
            DrawVramBackgroundTile(0x1a, 0x07, position);
            DrawVramBackgroundTile(0x10 + (level & 0x0f), 0x07,
                position + new Vector2(8, 0));
        }
    }

    private bool TryGetLevelOverlay(
        DisplayRecord display,
        out int level)
    {
        level = display.ExtraMode == 0
            ? _inventory.LevelForInventoryDisplay(display.TreasureId)
            : 0;
        return level > 0;
    }

    private void DrawLogicalBackgroundSprite(int sprite, int flags, Vector2 position)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int bank = sprite >= 0x80 ? 1 : 0;
        int firstTile = sprite * 2 & 0xff;
        int palette = flags & 7;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            int sy = flipY ? 15 - y : y;
            if (!TryGetVramPixel(bank, (firstTile + sy / 8) & 0xff, sx, sy & 7,
                out Color pixel, out bool spriteEncoding))
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _bgPalette[palette, PaletteShade(pixel, spriteEncoding)]);
        }
    }

    private void BlitLogicalBackgroundSprite(
        Image output,
        int sprite,
        int flags,
        Vector2I position)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int bank = sprite >= 0x80 ? 1 : 0;
        int firstTile = sprite * 2 & 0xff;
        int palette = flags & 7;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            int sy = flipY ? 15 - y : y;
            if (TryGetVramPixel(
                bank,
                (firstTile + sy / 8) & 0xff,
                sx,
                sy & 7,
                out Color pixel,
                out bool spriteEncoding))
            {
                output.SetPixel(
                    position.X + x,
                    position.Y + y,
                    _bgPalette[palette, PaletteShade(pixel, spriteEncoding)]);
            }
        }
    }

    private void DrawTreasureBackgroundSprite(
        int sprite,
        int sourceAttributes,
        Vector2 position) =>
        DrawLogicalBackgroundSprite(
            sprite, TreasureBackgroundAttributes(sourceAttributes), position);

    // bank2.s:drawTreasureDisplayDataToBg increments the source attribute byte
    // twice before writing it to w4AttributeMap, shifting sprite palette 0-5
    // into the inventory's BG palette slots 2-7 while preserving flip bits.
    private static int TreasureBackgroundAttributes(int sourceAttributes) =>
        (sourceAttributes + 2) & 0xff;

    private void DrawLogicalOamSprite(int sprite, int palette, Vector2 position)
    {
        if (!ItemIconAtlas.Select(
                sprite, _equippedItemIcons1, _itemIcons2, _itemIcons3,
                out Image source, out int cell))
        {
            return;
        }
        palette = Math.Clamp(palette & 7, 0, _spritePalette.GetLength(0) - 1);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int shade = ItemIconAtlas.ShadeFromPng(
                source.GetPixel(cell * 8 + x, y), out bool transparent);
            if (!transparent)
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _spritePalette[palette, shade]);
        }
    }

    private void OnInventoryChanged() => QueueRedraw();

    private void DrawRawOamTile(int bank, int tile, int palette, Vector2 position, bool flipX = false)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            if (!TryGetVramPixel(
                    bank,
                    (tile & 0xfe) + y / 8,
                    sx,
                    y & 7,
                    out Color pixel,
                    out bool spriteEncoding))
            {
                continue;
            }
            int shade = PaletteShade(pixel, spriteEncoding);
            if (shade != 0 && palette < _spritePalette.GetLength(0))
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _spritePalette[palette, shade]);
        }
    }

    private void BlitRawOamTile(
        Image output,
        int bank,
        int tile,
        int palette,
        Vector2I position,
        bool flipX)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            if (!TryGetVramPixel(
                    bank,
                    (tile & 0xfe) + y / 8,
                    sx,
                    y & 7,
                    out Color pixel,
                    out bool spriteEncoding) ||
                palette >= _spritePalette.GetLength(0))
            {
                continue;
            }
            int shade = PaletteShade(pixel, spriteEncoding);
            if (shade != 0)
            {
                output.SetPixel(
                    position.X + x,
                    position.Y + y,
                    _spritePalette[palette, shade]);
            }
        }
    }

    private void DrawVramBackgroundTile(
        int tile,
        int flags,
        Vector2 position,
        bool priorityOnly = false)
    {
        int bank = (flags & 0x08) != 0 ? 1 : 0;
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int palette = flags & 7;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            if (!TryGetVramPixel(bank, tile, flipX ? 7 - x : x, flipY ? 7 - y : y,
                out Color pixel, out bool spriteEncoding))
                continue;
            int shade = PaletteShade(pixel, spriteEncoding);
            if (!priorityOnly || shade != 0)
            {
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _bgPalette[palette, shade]);
            }
        }
    }

    private void DrawHudBackgroundTile(
        int tile,
        Vector2 position,
        bool priorityOnly = false)
    {
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color color = HudBackgroundTileColor(tile, x, y);
            int shade = TwoBitShade(_hudTiles.GetPixel(
                tile % (_hudTiles.GetWidth() / 8) * 8 + x,
                tile / (_hudTiles.GetWidth() / 8) * 8 + y));
            if (!priorityOnly || shade != 0)
            {
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    color);
            }
        }
    }

    private Color HudBackgroundTileColor(int tile, int x, int y)
    {
        int columns = _hudTiles.GetWidth() / 8;
        Color pixel = _hudTiles.GetPixel(tile % columns * 8 + x, tile / columns * 8 + y);
        return _bgPalette[0, TwoBitShade(pixel)];
    }

    private bool TryGetVramPixel(int bank, int tile, int x, int y,
        out Color pixel, out bool spriteEncoding)
        => OracleTileRenderer.TryGetVramPixel(
            bank == 0 ? _bank0Sources : _bank1Sources,
            tile, x, y, out pixel, out spriteEncoding);

    private bool TrySelectVramTile(int bank, int tile, out Image source,
        out int sourceTile, out bool interleaved, out bool spriteEncoding)
    {
        if (!OracleTileRenderer.TrySelectVramTile(
            bank == 0 ? _bank0Sources : _bank1Sources,
            tile,
            out OracleVramSource result,
            out sourceTile))
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

    private static void FillRectangle(byte[] map, byte[] flags, int offset,
        int height, int width, byte tile, byte attributes)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            map[offset + y * TilemapStride + x] = tile;
            flags[offset + y * TilemapStride + x] = attributes;
        }
    }

    private static Vector2 Slot(int tileMapOffset) => new(
        (tileMapOffset & 0x1f) * 8, (tileMapOffset >> 5) * 8);

    private Vector2 ItemSlotPosition(int index) =>
        Slot(_layouts.InventoryItemSlots[index].TilemapOffset);
}

public enum InventorySubscreen
{
    Items,
    SecondaryItems,
    EssencesAndSave
}

internal enum InventoryTextPhase
{
    Hidden,
    NamePause,
    Description,
    TrailingSpaces,
    NameReplay,
    NamePadding,
    FullNameLeadWait,
    FullNamePause
}
