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

    public override void _Ready()
    {
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
        DrawItemIcon(_treasures.GetButtonDisplay(EquippedB, _inventory), new Vector2(8, 0));
        DrawItemIcon(_treasures.GetButtonDisplay(EquippedA, _inventory), new Vector2(48, 0));
    }

    public void Refresh()
    {
        _background = BuildBackgroundTexture();
        QueueRedraw();
    }

    private Texture2D BuildBackgroundTexture()
    {
        Image tiles = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/gfx_hud.png");
        Image partialHearts = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/gfx_partial_hearts.png");
        byte[] map = Godot.FileAccess.GetFileAsBytes("res://assets/oracle/hud/map_hud_normal.bin");
        byte[] flags = Godot.FileAccess.GetFileAsBytes("res://assets/oracle/hud/flg_hud_normal.bin");
        if (map.Length != 64 || flags.Length != 64)
            throw new InvalidOperationException("Normal HUD maps must contain 64 bytes.");

        // updateStatusBar_body always restores the overworld rupee icon.
        map[0x0a] = 0x04;
        WriteRupeeDigits(map);
        WriteHearts(map);

        Image output = Image.CreateEmpty(160, 16, false, Image.Format.Rgba8);
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < VisibleColumns; column++)
        {
            int mapOffset = row * MapStride + column;
            DrawHudTile(
                output,
                tiles,
                partialHearts,
                HealthQuarters % 4,
                map[mapOffset],
                flags[mapOffset],
                column * 8,
                row * 8);
        }
        return ImageTexture.CreateFromImage(output);
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
        byte tile,
        byte flags,
        int destinationX,
        int destinationY)
    {
        Image tileSource = source;
        int tileX = tile % 16 * 8;
        int tileY = tile / 16 * 8;
        if (tile == 0x0b && partialQuarters is >= 1 and <= 3)
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

        DrawItemSprite(display.LeftSprite, display.LeftPalette, position);
        if (display.RightSprite != 0)
            DrawItemSprite(display.RightSprite, display.RightPalette, position + new Vector2(8, 0));
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
