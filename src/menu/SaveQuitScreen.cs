using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Original non-game-over save menu: Continue, Save and Continue, Save and Quit.
/// </summary>
public partial class SaveQuitScreen : Node2D
{
    private const int MapStride = 32;
    private Texture2D _background = null!;
    private Image _fileSprites = null!;
    private Color[,] _spritePalette = null!;
    private int _delayCounter;

    public int Cursor { get; private set; }
    public int DelayCounter
    {
        get => _delayCounter;
        set
        {
            _delayCounter = Math.Max(0, value);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        _fileSprites = LoadPng("res://assets/oracle/menu/spr_fileselect_decorations.png");
        _spritePalette = LoadPalette("res://assets/oracle/menu/palette_file_sprites.bin");
        _background = BuildBackground();
    }

    public void Open()
    {
        Cursor = 0;
        DelayCounter = 0;
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        DelayCounter = 0;
    }

    public void Move(int direction)
    {
        int next = Cursor + Math.Sign(direction);
        if (next is < 0 or > 2)
            return;
        Cursor = next;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible)
            return;
        DrawTexture(_background, Vector2.Zero);
        DrawFileDecorations();
        if ((DelayCounter & 0x04) == 0)
            DrawOamTile(0x28, 4, new Vector2(33, 56 + Cursor * 24));
    }

    private Texture2D BuildBackground()
    {
        byte[] map = new byte[MapStride * 18];
        byte[] flags = new byte[MapStride * 18];
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_file_menu_top.bin", 160), 0);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_file_menu_top.bin", 160), 0);
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_save_menu_middle.bin", 320), 0xa0);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_save_menu_middle.bin", 320), 0xa0);
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_save_menu_bottom.bin", 128), 0x1e0, 96);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_save_menu_bottom.bin", 128), 0x1e0, 96);

        Color[,] palette = LoadPalette("res://assets/oracle/menu/palette_file_bg.bin");
        var sources = new (Image Image, int FirstTile, int Bank, bool Interleaved)[]
        {
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 0, false),
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 1, false),
            (LoadPng("res://assets/oracle/menu/gfx_savescreen.png"), 0x80, 1, true),
            (LoadPng("res://assets/oracle/menu/gfx_fileselect.png"), 0x20, 1, false)
        };
        Image output = Image.CreateEmpty(160, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int offset = row * MapStride + column;
            int bank = flags[offset] >> 3 & 1;
            (Image Image, int FirstTile, int Bank, bool Interleaved)? selected = null;
            foreach (var source in sources)
            {
                int count = source.Image.GetWidth() / 8 * (source.Image.GetHeight() / 8);
                if (source.Bank == bank && map[offset] >= source.FirstTile &&
                    map[offset] < source.FirstTile + count)
                    selected = source;
            }
            if (selected is not { } tileSource)
                continue;
            DrawBackgroundTile(output, tileSource.Image, map[offset] - tileSource.FirstTile,
                flags[offset], palette, column * 8, row * 8, tileSource.Interleaved);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private void DrawFileDecorations()
    {
        int[,] sprites = {
            {0x23,0x0a,0x20,5},{0x23,0x12,0x22,5},{0x33,0x06,0x20,5},
            {0x33,0x0e,0x22,5},{0x0f,0x07,0x26,5},{0x3b,0x16,0x20,0x25},
            {0x3b,0x0e,0x22,0x25},{0x17,0x0a,0x24,0x25},{0x21,0x96,0x20,5},
            {0x21,0x9e,0x22,5},{0x17,0x9b,0x26,0x65},{0x14,0x9d,0x24,5},
            {0x31,0xa2,0x20,0x25},{0x31,0x9a,0x22,0x25},{0x39,0x92,0x20,5},
            {0x39,0x9a,0x22,5}
        };
        for (int index = 0; index < sprites.GetLength(0); index++)
        {
            int attributes = sprites[index, 3];
            DrawOamTile(sprites[index, 2], attributes & 7,
                new Vector2(sprites[index, 1] - 8, sprites[index, 0] - 16),
                (attributes & 0x20) != 0, (attributes & 0x40) != 0);
        }
    }

    private void DrawOamTile(int tile, int palette, Vector2 position,
        bool flipX = false, bool flipY = false)
    {
        int sourceTile = tile - 0x20;
        int columns = _fileSprites.GetWidth() / 8;
        int cell = sourceTile / 2;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sy = flipY ? 15 - y : y;
            Color pixel = _fileSprites.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x), cell / columns * 16 + sy);
            int color = Shade(pixel);
            if (pixel.A < 0.1f || color == 0)
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                _spritePalette[palette, color]);
        }
    }

    private static void DrawBackgroundTile(Image output, Image source, int sourceTile,
        byte flags, Color[,] palette, int destinationX, int destinationY, bool interleaved)
    {
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
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = flags & 7;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(tileX + (flipX ? 7 - x : x), tileY + (flipY ? 7 - y : y));
            output.SetPixel(destinationX + x, destinationY + y, palette[paletteIndex, Shade(pixel)]);
        }
    }

    private static int Shade(Color pixel) =>
        Math.Clamp(Mathf.RoundToInt((1.0f - pixel.R) * 3.0f), 0, 3);

    private static Image LoadPng(string path)
    {
        Image image = new();
        Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load save-menu graphics {path}: {error}.");
        return image;
    }

    private static byte[] ReadBytes(string path, int expected)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length != expected)
            throw new InvalidOperationException($"{path} should contain {expected} bytes, got {bytes.Length}.");
        return bytes;
    }

    private static Color[,] LoadPalette(string path)
    {
        byte[] bytes = ReadBytes(path, 8 * 4 * 3);
        var result = new Color[8, 4];
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            result[palette, shade] = new Color(
                bytes[offset] / 31.0f, bytes[offset + 1] / 31.0f, bytes[offset + 2] / 31.0f);
        }
        return result;
    }

    private static void Overlay(byte[] destination, byte[] source, int offset, int? count = null) =>
        Array.Copy(source, 0, destination, offset, count ?? source.Length);
}
