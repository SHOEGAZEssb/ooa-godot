using Godot;
using System;

namespace oracleofages;

/// <summary>
/// The three Start-menu inventory subscreens. Backgrounds come from the
/// original GFXH_INVENTORY_SCREEN / GFXH_INVENTORY_SUBSCREEN_* VRAM records;
/// live item, ring, essence, heart-piece, and era state is layered over them.
/// </summary>
public partial class InventoryScreen : Node2D
{
    public enum InventorySubscreen { Items, SecondaryItems, EssencesAndSave }

    public const int PageScrollUpdates = 13;
    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int ScreenRows = 18;
    private const float PageScrollPixelsPerUpdate = 12.0f;

    private static readonly Vector2[] SlotPositions =
    {
        Slot(0x063), Slot(0x067), Slot(0x06b), Slot(0x06f),
        Slot(0x0c3), Slot(0x0c7), Slot(0x0cb), Slot(0x0cf),
        Slot(0x123), Slot(0x127), Slot(0x12b), Slot(0x12f),
        Slot(0x183), Slot(0x187), Slot(0x18b), Slot(0x18f)
    };

    private static readonly PassiveTreasure[] PassiveTreasures =
    {
        new(0x2e, 0x01, 0), new(0x4a, 0x01, 0), new(0x2f, 0x04, 1),
        new(0x41, 0x07, 2), new(0x50, 0x0a, 3), new(0x51, 0x0a, 3),
        new(0x4e, 0x0a, 3), new(0x4f, 0x0a, 3), new(0x36, 0x0a, 3),
        new(0x34, 0x0d, 4), new(0x42, 0x31, 5), new(0x52, 0x34, 6),
        new(0x48, 0x34, 6), new(0x54, 0x34, 6), new(0x53, 0x34, 6),
        new(0x4d, 0x37, 7), new(0x4c, 0x37, 7), new(0x49, 0x27, 7),
        new(0x58, 0x47, 7), new(0x43, 0x37, 7), new(0x5b, 0x3a, 8),
        new(0x4b, 0x3d, 9), new(0x5a, 0x61, 10), new(0x59, 0x61, 10),
        new(0x44, 0x61, 10), new(0x5e, 0x64, 11), new(0x5c, 0x64, 11),
        new(0x5d, 0x64, 11), new(0x45, 0x64, 11), new(0x46, 0x67, 12),
        new(0x55, 0x6a, 13), new(0x2d, 0x6d, 14)
    };

    private static readonly int[] EssenceTileOffsets =
        { 0x084, 0x087, 0x0c9, 0x129, 0x167, 0x164, 0x122, 0x0c2 };
    private static readonly byte[] SecondaryCursorData =
        { 0x52, 0x55, 0x58, 0x5b, 0x5e, 0x82, 0x85, 0x88, 0x8b, 0x8e,
          0xb2, 0xb5, 0xb8, 0xbb, 0xbe, 0xe0, 0xe3, 0xe6, 0xe9, 0xec, 0xef };
    private static readonly Vector2I[] EssenceCursorOffsets =
    {
        new(0x30, 0x20), new(0x30, 0x38), new(0x40, 0x48), new(0x58, 0x48),
        new(0x68, 0x38), new(0x68, 0x20), new(0x58, 0x10), new(0x40, 0x10),
        new(0x28, 0x70), new(0x58, 0x70), new(0x70, 0x70)
    };

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
    private Image _itemIcons2 = null!;
    private Image _itemIcons3 = null!;
    private Image _essenceTiles = null!;
    private Image[] _questItemTiles = null!;
    private VramSource[] _bank0Sources = null!;
    private VramSource[] _bank1Sources = null!;
    private byte[] _ringMap = null!;
    private Color[,] _bgPalette = null!;
    private Color[,] _spritePalette = null!;
    private TreasureDatabase _treasures = null!;
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

    public int Cursor => _itemCursor;
    public int ActiveCursor => _subscreen switch
    {
        InventorySubscreen.Items => _itemCursor,
        InventorySubscreen.SecondaryItems => _secondaryCursor,
        _ => _rightSide ? 0x80 | _rightCursor : _essenceCursor
    };
    public InventorySubscreen Subscreen => _subscreen;
    public bool PageTransitionActive => _pageScrollFrame > 0.0f;
    public bool SaveAndQuitSelected =>
        _subscreen == InventorySubscreen.EssencesAndSave && _rightSide && _rightCursor == 2;

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
        _itemIcons1 = LoadPng("res://assets/oracle/gfx/spr_item_icons_1.png");
        _itemIcons2 = LoadPng("res://assets/oracle/gfx/spr_item_icons_2.png");
        _itemIcons3 = LoadPng("res://assets/oracle/gfx/spr_item_icons_3.png");
        _essenceTiles = LoadPng("res://assets/oracle/inventory/spr_essences.png");
        _questItemTiles = new Image[4];
        for (int sheet = 0; sheet < _questItemTiles.Length; sheet++)
            _questItemTiles[sheet] = LoadPng($"res://assets/oracle/inventory/spr_quest_items_{sheet + 1}.png");
        _ringMap = ReadBytes("res://assets/oracle/inventory/map_rings.bin", 68 * 8);
        _bgPalette = LoadPalette("res://assets/oracle/inventory/palette_bg.bin", 8, 0);
        _spritePalette = ItemIconAtlas.LoadStandardSpritePalettes();

        _bank0Sources = new[]
        {
            new VramSource(0x00, _inventoryHud1, false),
            new VramSource(0x30, _presentPastSymbols, true, true),
            new VramSource(0x40, _questItems5, true, true),
            new VramSource(0x60, _mapCompassItems, true, true),
            new VramSource(0x60, _saveTiles, false),
            new VramSource(0x80, _blankTiles, false),
            new VramSource(0xa0, _ringTiles, true),
            new VramSource(0xe0, _inventoryHud2, false)
        };
        _bank1Sources = new[]
        {
            new VramSource(0x00, _itemIcons1, true, true),
            new VramSource(0x20, _itemIcons2, true, true),
            new VramSource(0x40, _itemIcons3, true, true),
            new VramSource(0x60, _essenceTiles, true, true),
            new VramSource(0x80, _questItemTiles[0], true, true),
            new VramSource(0xa0, _questItemTiles[1], true, true),
            new VramSource(0xc0, _questItemTiles[2], true, true),
            new VramSource(0xe0, _questItemTiles[3], true, true)
        };
    }

    public void Initialize(TreasureDatabase treasures, InventoryState inventory, Func<bool>? isPast = null)
    {
        _treasures = treasures;
        _inventory = inventory;
        _isPast = isPast ?? (() => false);
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
        _backgrounds = new[] { BuildBackgroundTexture(0), BuildBackgroundTexture(1), BuildBackgroundTexture(2) };
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        _pageScrollFrame = 0.0f;
    }

    public void BeginNextSubscreen()
    {
        if (PageTransitionActive)
            return;
        _nextSubscreen = (InventorySubscreen)(((int)_subscreen + 1) % 3);
        _pageScrollFrame = float.Epsilon;
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

    public void MoveCursor(Vector2I direction)
    {
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
        QueueRedraw();
    }

    public void EquipToA()
    {
        if (_subscreen != InventorySubscreen.Items)
            return;
        _inventory.SwapStorageSlotWithButton(_itemCursor, isA: true);
        QueueRedraw();
    }

    public void EquipToB()
    {
        if (_subscreen != InventorySubscreen.Items)
            return;
        _inventory.SwapStorageSlotWithButton(_itemCursor, isA: false);
        QueueRedraw();
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
        DrawSubscreen(_subscreen, Vector2.Zero, drawCursor: true);
    }

    private void DrawSubscreen(InventorySubscreen page, Vector2 drawOffset, bool drawCursor)
    {
        DrawTexture(_backgrounds[(int)page], drawOffset);
        DrawTreasure(_treasures.GetButtonDisplay(_inventory.EquippedB, _inventory),
            new Vector2(8, 0) + drawOffset, spritePalette: true);
        DrawTreasure(_treasures.GetButtonDisplay(_inventory.EquippedA, _inventory),
            new Vector2(48, 0) + drawOffset, spritePalette: true);

        switch (page)
        {
            case InventorySubscreen.Items:
                for (int index = 0; index < InventoryState.InventoryCapacity; index++)
                    DrawTreasure(_treasures.GetButtonDisplay(_inventory.StorageItemAt(index), _inventory),
                        SlotPositions[index] + drawOffset, spritePalette: false);
                if (drawCursor)
                    DrawItemCursor(SlotPositions[_itemCursor]);
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
        var selected = new PassiveTreasure?[15];
        foreach (PassiveTreasure treasure in PassiveTreasures)
        {
            if (_inventory.HasTreasure(treasure.Id))
                selected[treasure.Slot] = treasure;
        }
        foreach (PassiveTreasure? treasure in selected)
        {
            if (treasure is not PassiveTreasure value || value.Id == 0x36)
                continue;
            DrawTreasure(_treasures.GetButtonDisplay(value.Id, _inventory),
                Slot(0x62 + (value.Position >> 4) * 0x20 + (value.Position & 0x0f)) + drawOffset,
                spritePalette: false);
        }
    }

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
            DrawVramBackgroundTile((flags & 0x08) != 0 ? 1 : 0, tile, flags,
                position + new Vector2(cell % 2 * 8, cell / 2 * 8));
        }
    }

    private void DrawEraSymbol(Vector2 drawOffset)
    {
        int first = _isPast() ? 0x1c : 0x18;
        DrawLogicalBackgroundSprite(first, 1 + (_isPast() ? 2 : 0), Slot(0x06e) + drawOffset);
        DrawLogicalBackgroundSprite(first + 1, 1 + (_isPast() ? 2 : 0), Slot(0x06f) + drawOffset);
        DrawLogicalBackgroundSprite(first + 2, 1 + (_isPast() ? 2 : 0), Slot(0x070) + drawOffset);
        DrawLogicalBackgroundSprite(first + 3, 1 + (_isPast() ? 2 : 0), Slot(0x071) + drawOffset);
    }

    private void DrawHeartPieces(Vector2 drawOffset)
    {
        int count = Math.Clamp(_inventory.HeartPieces, 0, 3);
        if (count >= 1)
        {
            DrawLogicalBackgroundSprite(0x78, 5, Slot(0x0ce) + drawOffset);
            DrawLogicalBackgroundSprite(0x79, 5, Slot(0x0cf) + drawOffset);
        }
        if (count >= 2)
        {
            DrawLogicalBackgroundSprite(0x7a, 5, Slot(0x10e) + drawOffset);
            DrawLogicalBackgroundSprite(0x7b, 5, Slot(0x10f) + drawOffset);
        }
        if (count >= 3)
        {
            DrawLogicalBackgroundSprite(0x7b, 0x25, Slot(0x110) + drawOffset);
            DrawLogicalBackgroundSprite(0x7a, 0x25, Slot(0x111) + drawOffset);
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
        byte packed = SecondaryCursorData[Math.Min(_secondaryCursor, SecondaryCursorData.Length - 1)];
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
        Vector2I raw = EssenceCursorOffsets[index];
        int rightOffset = _rightSide ? 40 : 24;
        DrawRawOamTile(0, 0x0c, 2, new Vector2(raw.Y - 8, raw.X - 16), flipX: true);
        DrawRawOamTile(0, 0x0c, 2, new Vector2(raw.Y + rightOffset - 8, raw.X - 16));
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
                for (int essence = 0; essence < EssenceTileOffsets.Length; essence++)
                {
                    if ((_inventory.Essences & (1 << essence)) == 0)
                        FillRectangle(map, flags, EssenceTileOffsets[essence], 2, 2, 0x00, 0x07);
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

    private void DrawTreasure(TreasureDatabase.DisplayRecord display, Vector2 position, bool spritePalette)
    {
        if (!display.HasIcon)
            return;
        if (spritePalette)
        {
            DrawLogicalOamSprite(display.LeftSprite,
                EquippedLeftSpritePalette(display.LeftSprite, display.LeftPalette), position);
            if (display.RightSprite != 0)
                DrawLogicalOamSprite(display.RightSprite, display.RightPalette & 7, position + new Vector2(8, 0));
            return;
        }
        DrawLogicalBackgroundSprite(display.LeftSprite, display.LeftPalette + 2, position);
        if (display.RightSprite != 0)
            DrawLogicalBackgroundSprite(display.RightSprite, display.RightPalette + 2,
                position + new Vector2(8, 0));
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

    private void DrawLogicalOamSprite(int sprite, int palette, Vector2 position)
    {
        int bank = sprite >= 0x80 ? 1 : 0;
        int firstTile = sprite * 2 & 0xff;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            if (!TryGetVramPixel(bank, firstTile + y / 8, x, y & 7, out Color pixel, out _))
                continue;
            int shade = ItemIconAtlas.ShadeFromPng(pixel, out bool transparent);
            if (!transparent)
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _spritePalette[palette & 7, shade]);
        }
    }

    private void DrawRawOamTile(int bank, int tile, int palette, Vector2 position, bool flipX = false)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sx = flipX ? 7 - x : x;
            if (!TryGetVramPixel(bank, (tile & 0xfe) + y / 8, sx, y & 7, out Color pixel, out _))
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
            if (!TryGetVramPixel(bank, tile, flipX ? 7 - x : x, flipY ? 7 - y : y,
                out Color pixel, out bool spriteEncoding))
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _bgPalette[palette, PaletteShade(pixel, spriteEncoding)]);
        }
    }

    private bool TryGetVramPixel(int bank, int tile, int x, int y,
        out Color pixel, out bool spriteEncoding)
    {
        if (!TrySelectVramTile(bank, tile, out Image source, out int sourceTile,
            out bool interleaved, out spriteEncoding))
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

    private bool TrySelectVramTile(int bank, int tile, out Image source,
        out int sourceTile, out bool interleaved, out bool spriteEncoding)
    {
        VramSource? selected = null;
        foreach (VramSource candidate in bank == 0 ? _bank0Sources : _bank1Sources)
        {
            if (tile >= candidate.FirstTile && tile < candidate.FirstTile + candidate.TileCount)
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

    private static void DrawTileToImage(Image output, Image source, int sourceTile, byte flags,
        Color[,] palette, int destinationX, int destinationY,
        bool interleaved, bool spriteEncoding)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = flags & 7;
        int columns = source.GetWidth() / 8;
        int tileX;
        int tileY;
        if (interleaved)
        {
            int cell = sourceTile / 2;
            tileX = cell % columns * 8;
            tileY = cell / columns * 16 + (sourceTile & 1) * 8;
        }
        else
        {
            tileX = sourceTile % columns * 8;
            tileY = sourceTile / columns * 8;
        }
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(tileX + (flipX ? 7 - x : x), tileY + (flipY ? 7 - y : y));
            output.SetPixel(destinationX + x, destinationY + y,
                palette[paletteIndex, PaletteShade(pixel, spriteEncoding)]);
        }
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

    private static void Overlay(byte[] destination, byte[] source, int offset, int? count = null) =>
        Array.Copy(source, 0, destination, offset, count ?? source.Length);

    private static int EquippedLeftSpritePalette(int sprite, int palette) =>
        sprite == 0x8a || sprite < 0x86 ? ((palette - 3) | 1) & 7 : palette & 7;

    private static int TwoBitShade(Color sourceColor) =>
        Math.Clamp(Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);

    private static int PaletteShade(Color sourceColor, bool spriteEncoding) =>
        spriteEncoding ? ItemIconAtlas.ShadeFromPng(sourceColor, out _) : TwoBitShade(sourceColor);

    private static Image LoadPng(string path)
    {
        Image image = new();
        Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load inventory graphics {path}: {error}.");
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
        for (int palette = 0; palette < count; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            result[firstPalette + palette, shade] = new Color(
                bytes[offset] / 31.0f, bytes[offset + 1] / 31.0f, bytes[offset + 2] / 31.0f);
        }
        return result;
    }

    private static Vector2 Slot(int tileMapOffset) => new(
        (tileMapOffset & 0x1f) * 8, (tileMapOffset >> 5) * 8);

    private readonly record struct PassiveTreasure(int Id, int Position, int Slot);
    private readonly record struct VramSource(
        int FirstTile, Image Image, bool Interleaved, bool SpriteEncoding = false)
    {
        public int TileCount => Image.GetWidth() / 8 * (Image.GetHeight() / 8);
    }
}
