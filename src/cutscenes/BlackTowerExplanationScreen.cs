using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Renders GFXH_BLACK_TOWER_STAGE_1_LAYOUT/BASE and OAM $714c.</summary>
internal partial class BlackTowerExplanationScreen : Control
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly BlackTowerEntranceEventDatabase _database;
    private readonly Texture2D _background;
    private readonly Image _sprites;
    private readonly Dictionary<int, Texture2D> _spriteTextures = new();
    private bool _flashWhite;

    internal bool FlashWhite => _flashWhite;
    internal ulong BackgroundPixelHash { get; }

    public BlackTowerExplanationScreen(BlackTowerEntranceEventDatabase database)
    {
        _database = database;
        Name = "BlackTowerExplanationScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 14;
        _background = BuildBackground(database.BackgroundPalettes);
        BackgroundPixelHash = Hash(_background.GetImage());
        _sprites = OracleGraphicsCache.LoadImage(
            Root + "spr_black_tower_scene.png");
    }

    internal void SetFlashWhite(bool white)
    {
        if (_flashWhite == white)
            return;
        _flashWhite = white;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_background, Vector2.Zero);
        for (int index = _database.Oam.Count - 1; index >= 0; index--)
        {
            BlackTowerEntranceEventDatabase.OamRecord oam = _database.Oam[index];
            // func_6f44 supplies b=-SCY ($90 for SCY=$70). OAM coordinates
            // wrap as bytes before the hardware's x-8/y-16 origin offsets.
            int rawY = (oam.Y + 0x90) & 0xff;
            Vector2 position = new(oam.X - 8, rawY - 16);
            if (position.X <= -8 || position.X >= OracleRoomData.ViewportWidth ||
                position.Y <= -16 || position.Y >= OracleRoomData.ScreenHeight)
                continue;
            DrawTexture(SpriteTexture(oam), position);
        }
        if (_flashWhite)
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
    }

    private Texture2D SpriteTexture(BlackTowerEntranceEventDatabase.OamRecord oam)
    {
        int key = oam.Tile | (oam.Flags << 8);
        if (_spriteTextures.TryGetValue(key, out Texture2D? texture))
            return texture;
        int cell = (oam.Tile & 0xfe) / 2;
        int columns = _sprites.GetWidth() / 8;
        bool flipX = (oam.Flags & 0x20) != 0;
        bool flipY = (oam.Flags & 0x40) != 0;
        int palette = oam.Flags & 7;
        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color source = _sprites.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + (flipY ? 15 - y : y));
            int shade = source.R < 0.1f ? 0 : source.R < 0.5f ? 1 : source.R < 0.9f ? 2 : 3;
            if (source.A >= 0.1f && shade != 0)
                output.SetPixel(x, y, _database.SpritePalettes[palette, shade]);
        }
        texture = ImageTexture.CreateFromImage(output);
        _spriteTextures.Add(key, texture);
        return texture;
    }

    private static Texture2D BuildBackground(Color[,] palettes)
    {
        byte[] topMap = ReadBytes(Root + "map_black_tower_stage_1.bin", 384);
        byte[] topFlags = ReadBytes(Root + "flags_black_tower_stage_1.bin", 384);
        byte[] baseMap = ReadBytes(Root + "map_black_tower_base.bin", 192);
        byte[] baseFlags = ReadBytes(Root + "flags_black_tower_base.bin", 192);
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        Array.Copy(topMap, map, topMap.Length);
        Array.Copy(baseMap, 0, map, topMap.Length, baseMap.Length);
        Array.Copy(topFlags, flags, topFlags.Length);
        Array.Copy(baseFlags, 0, flags, topFlags.Length, baseFlags.Length);

        var tiles = new (Image? Source, int Tile)[2, 256];
        AddTiles(tiles, OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_1.png"), 0x8800, 0);
        AddTiles(tiles, OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_2.png"), 0x9000, 0);
        AddTiles(tiles, OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_3.png"), 0x8800, 1);
        AddTiles(tiles, OracleGraphicsCache.LoadImage(
            Root + "gfx_black_tower_scene_4.png"), 0x9000, 1);

        Image output = Image.CreateEmpty(256, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 32; column++)
        {
            int offset = row * 32 + column;
            byte attributes = flags[offset];
            (Image? source, int tile) = tiles[(attributes >> 3) & 1, map[offset]];
            if (source is null)
                continue;
            bool flipX = (attributes & 0x20) != 0;
            bool flipY = (attributes & 0x40) != 0;
            int palette = attributes & 7;
            int sourceColumns = source.GetWidth() / 8;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                Color pixel = source.GetPixel(
                    tile % sourceColumns * 8 + (flipX ? 7 - x : x),
                    tile / sourceColumns * 8 + (flipY ? 7 - y : y));
                int shade = Math.Clamp(
                    Mathf.RoundToInt((1.0f - pixel.R) * 3.0f), 0, 3);
                output.SetPixel(column * 8 + x, row * 8 + y,
                    palettes[palette, shade]);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static void AddTiles(
        (Image? Source, int Tile)[,] tiles,
        Image source,
        int destination,
        int bank)
    {
        int firstTile = destination >= 0x9000
            ? (destination - 0x9000) / 16
            : 0x80 + (destination - 0x8800) / 16;
        int count = source.GetWidth() / 8 * (source.GetHeight() / 8);
        for (int tile = 0; tile < count; tile++)
            tiles[bank, (firstTile + tile) & 0xff] = (source, tile);
    }

    private static byte[] ReadBytes(string path, int expected)
    {
        byte[] result = FileAccess.GetFileAsBytes(path);
        if (result.Length != expected)
            throw new InvalidOperationException(
                $"{path} should contain {expected} bytes, got {result.Length}.");
        return result;
    }

    private static ulong Hash(Image image)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in image.GetData())
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
