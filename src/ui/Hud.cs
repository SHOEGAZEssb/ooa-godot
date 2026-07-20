using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Reconstructs the normal two-tile Oracle status bar from gfx_hud,
/// map_hud_normal, and the updateStatusBar_body tile positions.
/// </summary>
public partial class Hud : Node2D
{
    private const int MapStride = 32;
    private const int VisibleColumns = 20;

    private Texture2D _background = null!;
    private Image _hudTiles = null!;
    private Image _keyTile = null!;
    private Image _itemIcons1 = null!;
    private Image _itemIcons2 = null!;
    private Image _itemIcons3 = null!;
    private Color[,] _itemPalettes = null!;
    private TreasureDatabase? _treasures;
    private InventoryState? _inventory;

    public int Rupees { get; set; }
    public int HealthQuarters { get; set; } = 12;
    public int MaxHealthQuarters { get; set; } = 12;
    public int EquippedB { get; set; }
    public int EquippedA { get; set; }
    public int DungeonIndex { get; set; } = -1;
    public byte TilesetFlags { get; set; }

    public override void _Ready()
    {
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _keyTile = LoadPng("res://assets/oracle/gfx/gfx_key.png");
        _itemIcons1 = LoadPng("res://assets/oracle/gfx/spr_item_icons_1.png");
        _itemIcons2 = LoadPng("res://assets/oracle/gfx/spr_item_icons_2.png");
        _itemIcons3 = LoadPng("res://assets/oracle/gfx/spr_item_icons_3.png");
        _itemPalettes = ItemIconAtlas.LoadStandardSpritePalettes();
        Refresh();
    }

    public void Initialize(TreasureDatabase treasures, InventoryState inventory)
    {
        _treasures = treasures;
        _inventory = inventory;
        Refresh();
    }

    public override void _Draw()
    {
        DrawTexture(_background, Vector2.Zero);

        if (_treasures == null || _inventory == null)
            return;

        // wInventoryB is the left button slot in original RAM; wInventoryA is
        // the right slot. Item sprites are drawn over the tilemap status bar.
        TreasureDatabase.DisplayRecord equippedB =
            _treasures.GetButtonDisplay(EquippedB, _inventory);
        TreasureDatabase.DisplayRecord equippedA =
            _treasures.GetButtonDisplay(EquippedA, _inventory);
        DrawItemIcon(equippedB, new Vector2(8, 0));
        DrawItemIcon(equippedA, new Vector2(48, 0));
        // drawTreasureExtraTiles writes attribute $80. Nonzero BG pixels
        // therefore have priority over the overlapping equipped-item OAM.
        DrawItemExtra(equippedB, new Vector2(16, 8));
        DrawItemExtra(equippedA, new Vector2(56, 8));
    }

    public void Refresh()
    {
        _background = BuildBackgroundTexture();
        QueueRedraw();
    }

    private Texture2D BuildBackgroundTexture()
    {
        Image partialHearts = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/gfx_partial_hearts.png");
        byte[] map = BuildStatusMap();
        byte[] flags = Godot.FileAccess.GetFileAsBytes("res://assets/oracle/hud/flg_hud_normal.bin");
        if (flags.Length != 64)
            throw new InvalidOperationException("Normal HUD maps must contain 64 bytes.");

        Image output = Image.CreateEmpty(160, 16, false, Image.Format.Rgba8);
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < VisibleColumns; column++)
        {
            int mapOffset = row * MapStride + column;
            DrawHudTile(
                output,
                _hudTiles,
                partialHearts,
                HealthQuarters % 4,
                DungeonKeyDisplayActive && mapOffset == 0x0a ? _keyTile : null,
                map[mapOffset],
                flags[mapOffset],
                column * 8,
                row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private byte[] BuildStatusMap()
    {
        byte[] map = Godot.FileAccess.GetFileAsBytes(
            "res://assets/oracle/hud/map_hud_normal.bin");
        if (map.Length != 64)
            throw new InvalidOperationException("Normal HUD map must contain 64 bytes.");

        map[0x0a] = 0x04;
        // updateStatusBar_body writes the displayed rupee digits at $2a-$2c
        // independently of the dungeon-only key field at $0a-$0c.
        WriteRupeeDigits(map);
        if (DungeonKeyDisplayActive)
        {
            // A real dungeon dynamically replaces HUD tile $04 with gfx_key,
            // then writes the X and current-dungeon key digit alongside it.
            map[0x0b] = 0x1b;
            map[0x0c] = (byte)(0x10 + Mathf.Clamp(
                _inventory?.GetDungeonSmallKeys(DungeonIndex) ?? 0, 0, 9));
        }
        WriteHearts(map);
        return map;
    }

    private void WriteRupeeDigits(byte[] map)
    {
        int value = Mathf.Clamp(Rupees, 0, 999);
        map[0x2a] = (byte)(0x10 + value / 100);
        map[0x2b] = (byte)(0x10 + value / 10 % 10);
        map[0x2c] = (byte)(0x10 + value % 10);
    }

    private void WriteHearts(byte[] map)
    {
        int containers = Mathf.Clamp((MaxHealthQuarters + 3) / 4, 0, 7);
        int fullHearts = Mathf.Clamp(HealthQuarters / 4, 0, containers);
        int partialQuarters = Mathf.Clamp(HealthQuarters % 4, 0, 3);
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

    private static void DrawHudTile(
        Image output,
        Image source,
        Image partialHearts,
        int partialQuarters,
        Image? tileOverride,
        byte tile,
        byte flags,
        int destinationX,
        int destinationY)
    {
        Image tileSource = tileOverride ?? source;
        int tileX = tileOverride is null ? tile % 16 * 8 : 0;
        int tileY = tileOverride is null ? tile / 16 * 8 : 0;
        if (tileOverride is null && tile == 0x0b && partialQuarters is >= 1 and <= 3)
        {
            // updateStatusBar_body dynamically loads one of the first three
            // tiles from gfx_partial_hearts into HUD tile $0b.
            tileSource = partialHearts;
            tileX = (partialQuarters - 1) * 8;
            tileY = 0;
        }
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;

        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int readX = tileX + (flipX ? 7 - x : x);
            int readY = tileY + (flipY ? 7 - y : y);
            Color sourceColor = tileSource.GetPixel(readX, readY);
            int shade = Mathf.Clamp(Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
            output.SetPixel(destinationX + x, destinationY + y, HudPalette[shade]);
        }
    }

    private void DrawItemIcon(TreasureDatabase.DisplayRecord display, Vector2 position)
    {
        if (!display.HasIcon)
            return;

        DrawItemSprite(display.LeftSprite,
            ItemIconAtlas.EquippedLeftPalette(display.LeftSprite, display.LeftPalette),
            position);
        if (display.RightSprite != 0)
            DrawItemSprite(display.RightSprite, display.RightPalette, position + new Vector2(8, 0));
    }

    private void DrawItemExtra(TreasureDatabase.DisplayRecord display, Vector2 position)
    {
        if (_inventory == null)
            return;

        if (display.ExtraMode == 1)
        {
            int amount = _inventory.BcdAmountForInventoryDisplay(display.TreasureId);
            DrawHudOverlayTile(0x10 + ((amount >> 4) & 0x0f), position);
            DrawHudOverlayTile(0x10 + (amount & 0x0f), position + new Vector2(8, 0));
            return;
        }

        int level = display.ExtraMode == 0
            ? _inventory.LevelForInventoryDisplay(display.TreasureId)
            : 0;
        if (level <= 0)
            return;

        // updateStatusBar uses drawTreasureExtraTiles with c=$80. Palette 0
        // matches the tan HUD and bit 7 places nonzero BG pixels above item OAM.
        DrawHudOverlayTile(0x1a, position);
        DrawHudOverlayTile(0x10 + (level & 0x0f), position + new Vector2(8, 0));
    }

    private void DrawHudOverlayTile(int tile, Vector2 position)
    {
        int sourceX = tile % 16 * 8;
        int sourceY = tile / 16 * 8;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = _hudTiles.GetPixel(sourceX + x, sourceY + y);
            int shade = Mathf.Clamp(
                Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                HudPalette[shade]);
        }
    }

    internal (int SymbolTile, int DigitTile, Vector2 Position)?
        LevelOverlayForValidation(int item, bool isA)
    {
        if (_treasures == null || _inventory == null)
            return null;
        TreasureDatabase.DisplayRecord display = _treasures.GetButtonDisplay(item, _inventory);
        int level = display.ExtraMode == 0
            ? _inventory.LevelForInventoryDisplay(display.TreasureId)
            : 0;
        return level > 0
            ? (0x1a, 0x10 + (level & 0x0f), isA ? new Vector2(56, 8) : new Vector2(16, 8))
            : null;
    }

    internal (int TensTile, int OnesTile, Vector2 Position)?
        QuantityOverlayForValidation(int item, bool isA)
    {
        if (_treasures == null || _inventory == null)
            return null;
        TreasureDatabase.DisplayRecord display = _treasures.GetButtonDisplay(item, _inventory);
        if (display.ExtraMode != 1)
            return null;
        int amount = _inventory.BcdAmountForInventoryDisplay(display.TreasureId);
        return (0x10 + ((amount >> 4) & 0x0f), 0x10 + (amount & 0x0f),
            isA ? new Vector2(56, 8) : new Vector2(16, 8));
    }

    internal bool DungeonKeyDisplayActive =>
        DungeonIndex is >= 0 and < 16 && (TilesetFlags & 0x10) == 0;

    internal byte StatusMapTileForValidation(int offset)
    {
        if (offset is < 0 or >= 64)
            throw new ArgumentOutOfRangeException(nameof(offset));
        return BuildStatusMap()[offset];
    }

    internal ulong ItemIconShadeHashForValidation(int sprite)
    {
        if (!ItemIconAtlas.Select(
                sprite, _itemIcons1, _itemIcons2, _itemIcons3,
                out Image source, out int cell))
        {
            return 0;
        }
        return ItemIconAtlas.DecodedCellHash(source, cell);
    }

    private void DrawItemSprite(int sprite, int palette, Vector2 position)
    {
        if (!ItemIconAtlas.Select(
            sprite, _itemIcons1, _itemIcons2, _itemIcons3,
            out Image source, out int cell))
        {
            return;
        }

        palette = Mathf.Clamp(palette & 0x07, 0, 5);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int shade = ItemIconAtlas.ShadeFromPng(
                source.GetPixel(cell * 8 + x, y), out bool transparent);
            if (!transparent)
                DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                    _itemPalettes[palette, shade]);
        }
    }

    private static Image LoadPng(string path)
    {
        return OracleGraphicsCache.LoadImage(path);
    }

    // paletteData48e0, background palette 0 used by the status bar.
    private static readonly Color[] HudPalette =
    {
        GbcColor(0x0d, 0x01, 0x05),
        GbcColor(0x1d, 0x01, 0x03),
        GbcColor(0x1f, 0x1a, 0x11),
        GbcColor(0x00, 0x00, 0x00)
    };

    private static Color GbcColor(int r, int g, int b) => Color.Color8(
        (byte)Mathf.RoundToInt(r * 255.0f / 31.0f),
        (byte)Mathf.RoundToInt(g * 255.0f / 31.0f),
        (byte)Mathf.RoundToInt(b * 255.0f / 31.0f));
}
