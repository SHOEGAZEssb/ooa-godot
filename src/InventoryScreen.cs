using Godot;
using System;

namespace oracleofages;

/// <summary>
/// First inventory subscreen: 16 stored item slots at the original
/// inventorySubscreen0_drawStoredItems positions, with A/B equip swaps.
/// </summary>
public partial class InventoryScreen : Node2D
{
    private const int TilemapStride = 32;
    private const int ScreenColumns = 20;
    private const int ScreenRows = 18;

    private static readonly Vector2[] SlotPositions =
    {
        Slot(0x063), Slot(0x067), Slot(0x06b), Slot(0x06f),
        Slot(0x0c3), Slot(0x0c7), Slot(0x0cb), Slot(0x0cf),
        Slot(0x123), Slot(0x127), Slot(0x12b), Slot(0x12f),
        Slot(0x183), Slot(0x187), Slot(0x18b), Slot(0x18f)
    };

    private Texture2D _background = null!;
    private Image _hudTiles = null!;
    private Image _partialHearts = null!;
    private Image _inventoryHud1 = null!;
    private Image _presentPastSymbols = null!;
    private Image _inventoryHud2 = null!;
    private Image _itemIcons1 = null!;
    private Image _itemIcons2 = null!;
    private Image _itemIcons3 = null!;
    private Color[,] _bgPalette = null!;
    private Color[,] _spritePalette = null!;
    private TreasureDatabase _treasures = null!;
    private InventoryState _inventory = null!;
    private int _cursor;

    public int Cursor => _cursor;

    public override void _Ready()
    {
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _partialHearts = LoadPng("res://assets/oracle/gfx/gfx_partial_hearts.png");
        _inventoryHud1 = LoadPng("res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        _presentPastSymbols = LoadPng("res://assets/oracle/inventory/spr_present_past_symbols.png");
        _inventoryHud2 = LoadPng("res://assets/oracle/inventory/gfx_inventory_hud_2.png");
        _itemIcons1 = LoadPng("res://assets/oracle/gfx/spr_item_icons_1.png");
        _itemIcons2 = LoadPng("res://assets/oracle/gfx/spr_item_icons_2.png");
        _itemIcons3 = LoadPng("res://assets/oracle/gfx/spr_item_icons_3.png");
        _bgPalette = LoadPalette("res://assets/oracle/inventory/palette_bg.bin", 8, 0);
        _spritePalette = LoadPalette("res://assets/oracle/inventory/palette_sprites.bin", 6, 0);
    }

    public void Initialize(TreasureDatabase treasures, InventoryState inventory)
    {
        _treasures = treasures;
        _inventory = inventory;
    }

    public void Open()
    {
        _cursor = 0;
        _background = BuildBackgroundTexture();
        Visible = true;
        QueueRedraw();
    }

    public void Close() => Visible = false;

    public void MoveCursor(Vector2I direction)
    {
        int offset = direction switch
        {
            { X: 1 } => 1,
            { X: -1 } => -1,
            { Y: -1 } => -4,
            { Y: 1 } => 4,
            _ => 0
        };
        _cursor = (_cursor + offset) & 0x0f;
        QueueRedraw();
    }

    public void EquipToA()
    {
        _inventory.SwapStorageSlotWithButton(_cursor, isA: true);
        QueueRedraw();
    }

    public void EquipToB()
    {
        _inventory.SwapStorageSlotWithButton(_cursor, isA: false);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || _inventory == null || _treasures == null)
            return;

        DrawTexture(_background, Vector2.Zero);
        DrawEquippedItem(_treasures.GetButtonDisplay(_inventory.EquippedB, _inventory), new Vector2(8, 0));
        DrawEquippedItem(_treasures.GetButtonDisplay(_inventory.EquippedA, _inventory), new Vector2(48, 0));

        for (int index = 0; index < InventoryState.InventoryCapacity; index++)
        {
            Vector2 position = SlotPositions[index];
            DrawStoredItem(_treasures.GetButtonDisplay(_inventory.StorageItemAt(index), _inventory), position);
        }

        DrawCursor(SlotPositions[_cursor]);
    }

    private Texture2D BuildBackgroundTexture()
    {
        byte[] map = new byte[TilemapStride * ScreenRows];
        byte[] flags = new byte[TilemapStride * ScreenRows];
        Overlay(map, ReadBytes("res://assets/oracle/hud/map_hud_normal.bin", 64), 0x000);
        Overlay(flags, ReadBytes("res://assets/oracle/hud/flg_hud_normal.bin", 64), 0x000);
        Overlay(map, ReadBytes("res://assets/oracle/inventory/map_inventory_screen_1.bin", 416), 0x040);
        Overlay(flags, ReadBytes("res://assets/oracle/inventory/flg_inventory_screen_1.bin", 416), 0x040);
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
            int tile;
            if (row < 2)
            {
                SelectHudTile(map[offset], out Image tileSource, out tile);
                DrawTileToImage(output, tileSource, tile, flags[offset], _bgPalette, column * 8, row * 8);
            }
            else
            {
                Image source = SelectInventoryTile(map[offset], flags[offset], out tile, out bool spriteBankTile);
                if (spriteBankTile)
                {
                    DrawSpriteBankTileToImage(output, source, tile, flags[offset], _bgPalette,
                        column * 8, row * 8);
                }
                else
                {
                    DrawTileToImage(output, source, tile, flags[offset], _bgPalette, column * 8, row * 8);
                }
            }
        }
        return ImageTexture.CreateFromImage(output);

        Image SelectHudTile(byte tileId, out Image source, out int sourceTile)
        {
            source = _hudTiles;
            sourceTile = tileId;
            if (tileId == 0x0b && _inventory.HealthQuarters % 4 is >= 1 and <= 3)
            {
                source = _partialHearts;
                sourceTile = _inventory.HealthQuarters % 4 - 1;
            }
            return source;
        }
    }

    private void WriteRupeeDigits(byte[] map)
    {
        int value = Mathf.Clamp(_inventory.Rupees, 0, 999);
        map[0x2a] = (byte)(0x10 + value / 100);
        map[0x2b] = (byte)(0x10 + value / 10 % 10);
        map[0x2c] = (byte)(0x10 + value % 10);
    }

    private void WriteHearts(byte[] map)
    {
        int containers = Mathf.Clamp((_inventory.MaxHealthQuarters + 3) / 4, 0, 7);
        int fullHearts = Mathf.Clamp(_inventory.HealthQuarters / 4, 0, containers);
        int partialQuarters = Mathf.Clamp(_inventory.HealthQuarters % 4, 0, 3);
        int position = 0x0d;

        for (int heart = 0; heart < containers; heart++)
        {
            map[position + heart] = heart < fullHearts ? (byte)0x0a
                : heart == fullHearts && partialQuarters > 0 ? (byte)0x0b
                : (byte)0x09;
        }
        for (int heart = containers; heart < 7; heart++)
            map[position + heart] = 0x00;
    }

    private void DrawCursor(Vector2 position)
    {
        DrawOamTile(_inventoryHud1, 0x0c, 2, position + new Vector2(-8, 0), flipX: true);
        DrawOamTile(_inventoryHud1, 0x0c, 2, position + new Vector2(24, 0), flipX: false);
    }

    private void DrawStoredItem(TreasureDatabase.DisplayRecord display, Vector2 position)
    {
        if (!display.HasIcon)
            return;

        DrawItemBackgroundSprite(display.LeftSprite, display.LeftPalette + 2, position);
        if (display.RightSprite != 0)
            DrawItemBackgroundSprite(display.RightSprite, display.RightPalette + 2, position + new Vector2(8, 0));
    }

    private void DrawEquippedItem(TreasureDatabase.DisplayRecord display, Vector2 position)
    {
        if (!display.HasIcon)
            return;

        DrawItemOamSprite(display.LeftSprite, EquippedLeftSpritePalette(display.LeftSprite, display.LeftPalette),
            position);
        if (display.RightSprite != 0)
            DrawItemOamSprite(display.RightSprite, display.RightPalette & 0x07, position + new Vector2(8, 0));
    }

    private void DrawItemOamSprite(int sprite, int palette, Vector2 position)
    {
        if (!SelectItemIcon(sprite, out Image source, out int cell))
            return;

        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(cell * 8 + x, y);
            int shade = ItemIconShade(sourceColor, out bool transparent);
            if (transparent)
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _spritePalette[palette & 0x07, shade]);
        }
    }

    private void DrawItemBackgroundSprite(int sprite, int palette, Vector2 position)
    {
        if (!SelectItemIcon(sprite, out Image source, out int cell))
            return;

        palette = Mathf.Clamp(palette, 0, 7);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(cell * 8 + x, y);
            int shade = ItemIconShadeIncludingZero(sourceColor);
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _bgPalette[palette, shade]);
        }
    }

    private void DrawOamTile(Image source, int sourceTile, int palette, Vector2 position,
        bool flipX = false, bool flipY = false)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int spriteY = flipY ? 15 - y : y;
            int rawTile = (sourceTile & 0xfe) + spriteY / 8;
            int columns = Math.Max(1, source.GetWidth() / 8);
            int readX = rawTile % columns * 8 + (flipX ? 7 - x : x);
            int readY = rawTile / columns * 8 + spriteY % 8;
            Color sourceColor = source.GetPixel(readX, readY);
            int shade = SpriteShade(sourceColor, out bool transparent);
            if (transparent)
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _spritePalette[palette & 0x07, shade]);
        }
    }

    private bool SelectItemIcon(int sprite, out Image source, out int cell)
    {
        if (sprite is >= 0x80 and <= 0x8f)
        {
            source = _itemIcons1;
            cell = sprite - 0x80;
            return true;
        }
        if (sprite is >= 0x90 and <= 0x9f)
        {
            source = _itemIcons2;
            cell = sprite - 0x90;
            return true;
        }
        if (sprite is >= 0xa0 and <= 0xaf)
        {
            source = _itemIcons3;
            cell = sprite - 0xa0;
            return true;
        }
        source = _itemIcons1;
        cell = 0;
        return false;
    }

    private Image SelectInventoryTile(byte tile, byte flags, out int sourceTile, out bool spriteBankTile)
    {
        spriteBankTile = (flags & 0x08) != 0;
        if (spriteBankTile)
        {
            if (tile < 0x20)
            {
                sourceTile = tile;
                return _itemIcons1;
            }
            if (tile < 0x40)
            {
                sourceTile = tile - 0x20;
                return _itemIcons2;
            }
            if (tile < 0x60)
            {
                sourceTile = tile - 0x40;
                return _itemIcons3;
            }
            sourceTile = 0;
            return _itemIcons1;
        }

        if (tile < 0x30)
        {
            sourceTile = tile;
            return _inventoryHud1;
        }
        if (tile < 0x40)
        {
            sourceTile = tile - 0x30;
            return _presentPastSymbols;
        }
        if (tile >= 0xe0)
        {
            sourceTile = tile - 0xe0;
            return _inventoryHud2;
        }
        sourceTile = 0;
        return _inventoryHud1;
    }

    private static void DrawSpriteBankTileToImage(Image output, Image source, int sourceTile, byte flags,
        Color[,] palette, int destinationX, int destinationY)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = Mathf.Clamp(flags & 0x07, 0, 7);
        int cell = sourceTile / 2;
        int sourceBaseY = (sourceTile & 1) * 8;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int readX = cell * 8 + (flipX ? 7 - x : x);
            int readY = sourceBaseY + (flipY ? 7 - y : y);
            Color sourceColor = source.GetPixel(readX, readY);
            int shade = SpriteShadeIncludingZero(sourceColor);
            output.SetPixel(destinationX + x, destinationY + y, palette[paletteIndex, shade]);
        }
    }

    private static void DrawTileToImage(Image output, Image source, int sourceTile, byte flags,
        Color[,] palette, int destinationX, int destinationY)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = Mathf.Clamp(flags & 0x07, 0, 7);
        int columns = Math.Max(1, source.GetWidth() / 8);
        int tileX = sourceTile % columns * 8;
        int tileY = sourceTile / columns * 8;
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

    private static void Overlay(byte[] destination, byte[] source, int offset)
    {
        Array.Copy(source, 0, destination, offset, source.Length);
    }

    private static int EquippedLeftSpritePalette(int sprite, int palette)
    {
        if (sprite == 0x8a || sprite < 0x86)
            return ((palette - 3) | 1) & 0x07;
        return palette & 0x07;
    }

    private static int SpriteShade(Color sourceColor, out bool transparent)
    {
        int shade = TwoBitShade(sourceColor);
        transparent = shade == 0;
        if (transparent)
            return 0;
        return shade;
    }

    private static int SpriteShadeIncludingZero(Color sourceColor) => TwoBitShade(sourceColor);

    private static int TwoBitShade(Color sourceColor) =>
        Mathf.Clamp(Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);

    private static int ItemIconShade(Color sourceColor, out bool transparent)
    {
        int shade = ItemIconShadeIncludingZero(sourceColor);
        transparent = shade == 0;
        return shade;
    }

    private static int ItemIconShadeIncludingZero(Color sourceColor) => sourceColor.R < 0.1f ? 0
        : sourceColor.R < 0.5f ? 1
        : sourceColor.R < 0.9f ? 2
        : 3;

    private static Image LoadPng(string path)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        Image image = new();
        Error error = image.LoadPngFromBuffer(bytes);
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
            result[firstPalette + palette, shade] = GbcColor(
                bytes[offset], bytes[offset + 1], bytes[offset + 2]);
        }
        return result;
    }

    private static Vector2 Slot(int tileMapOffset) => new(
        (tileMapOffset & 0x1f) * 8,
        (tileMapOffset >> 5) * 8);

    private static Color GbcColor(int r, int g, int b) => new(r / 31.0f, g / 31.0f, b / 31.0f);
}
