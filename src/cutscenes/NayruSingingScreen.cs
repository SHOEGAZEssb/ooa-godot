using Godot;
using System;
using System.Collections.Generic;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>Renders GFXH_NAYRU_SINGING_CUTSCENE and bank3f.oamData_7249.</summary>
public partial class NayruSingingScreen : Control
{
    private const int MapStride = 32;
    private readonly Texture2D _background;
    private readonly Image _sprites;
    private readonly NayruIntroEventDatabase _database;
    private readonly Dictionary<int, Texture2D> _spriteTextures = new();
    private int _scrollX;

    public int ScrollX => _scrollX;

    public NayruSingingScreen(NayruIntroEventDatabase database)
    {
        _database = database;
        Name = "NayruSingingScreen";
        Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 14;
        _background = BuildBackground(database.SingingBackgroundPalettes);
        _sprites = LoadPng(
            "res://assets/oracle/cutscenes/spr_nayru_singing_cutscene.png");
    }

    public void SetScrollX(int scrollX)
    {
        _scrollX = Math.Clamp(scrollX, 0, 40);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_background, new Vector2(-_scrollX, 0));
        // Lower OAM indices cover later entries, so draw in reverse order.
        for (int index = _database.SingingOam.Count - 1; index >= 0; index--)
        {
            NayruIntroEventDatabase.SingingOamRecord oam = _database.SingingOam[index];
            Vector2 position = new(oam.X - 8 - _scrollX, oam.Y - 16);
            if (position.X <= -8 || position.X >= OracleRoomData.ViewportWidth ||
                position.Y <= -16 || position.Y >= OracleRoomData.ViewportHeight)
                continue;
            DrawTexture(SpriteTexture(oam), position);
        }
    }

    private Texture2D SpriteTexture(NayruIntroEventDatabase.SingingOamRecord oam)
    {
        int key = oam.Tile | (oam.Flags << 8);
        if (_spriteTextures.TryGetValue(key, out Texture2D? texture))
            return texture;
        bool flipX = (oam.Flags & 0x20) != 0;
        bool flipY = (oam.Flags & 0x40) != 0;
        int palette = oam.Flags & 7;
        int columns = _sprites.GetWidth() / 8;
        int cell = (oam.Tile & 0xfe) / 2;
        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color source = _sprites.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + (flipY ? 15 - y : y));
            int color = source.R < 0.1f ? 0 : source.R < 0.5f ? 1 : source.R < 0.9f ? 2 : 3;
            if (source.A >= 0.1f && color != 0)
                output.SetPixel(x, y, _database.SingingSpritePalettes[palette, color]);
        }
        texture = ImageTexture.CreateFromImage(output);
        _spriteTextures.Add(key, texture);
        return texture;
    }

    private static Texture2D BuildBackground(Color[,] palettes)
    {
        byte[] map = ReadBytes(
            "res://assets/oracle/cutscenes/map_nayru_singing_cutscene.bin", 576);
        byte[] flags = ReadBytes(
            "res://assets/oracle/cutscenes/flags_nayru_singing_cutscene.bin", 576);
        var tiles = new (Image? Source, int Tile)[2, 256];
        AddTiles(tiles, LoadPng(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_1.png"), 0x8800, 0);
        AddTiles(tiles, LoadPng(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_2.png"), 0x9000, 0);
        AddTiles(tiles, LoadPng(
            "res://assets/oracle/cutscenes/gfx_nayru_singing_cutscene_3.png"), 0x8800, 1);

        Image output = Image.CreateEmpty(256, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 32; column++)
        {
            int offset = row * MapStride + column;
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
                int shade = Math.Clamp(Mathf.RoundToInt((1.0f - pixel.R) * 3.0f), 0, 3);
                output.SetPixel(column * 8 + x, row * 8 + y, palettes[palette, shade]);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static void AddTiles(
        (Image? Source, int Tile)[,] tiles, Image source, int destination, int bank)
    {
        int firstTile = destination >= 0x9000
            ? (destination - 0x9000) / 16
            : 0x80 + (destination - 0x8800) / 16;
        int count = source.GetWidth() / 8 * (source.GetHeight() / 8);
        for (int tile = 0; tile < count; tile++)
            tiles[bank, (firstTile + tile) & 0xff] = (source, tile);
    }

}
