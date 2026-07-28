using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Shared 8x8 background and 8x16 OAM tile addressing/rendering.</summary>
internal static class OracleTileRenderer
{
    public static bool TrySelectVramTile(
        IReadOnlyList<OracleVramSource> sources,
        int tile,
        out OracleVramSource selected,
        out int sourceTile)
    {
        OracleVramSource? match = null;
        foreach (OracleVramSource candidate in sources)
        {
            if (tile >= candidate.FirstTile &&
                tile < candidate.FirstTile + candidate.TileCount)
            {
                match = candidate;
            }
        }
        if (match is not OracleVramSource result)
        {
            selected = default;
            sourceTile = 0;
            return false;
        }
        selected = result;
        sourceTile = tile - result.FirstTile;
        return true;
    }

    public static bool TryGetVramPixel(
        IReadOnlyList<OracleVramSource> sources,
        int tile,
        int x,
        int y,
        out Color pixel,
        out bool spriteEncoding)
    {
        if (!TrySelectVramTile(sources, tile, out OracleVramSource source,
            out int sourceTile))
        {
            pixel = Colors.Transparent;
            spriteEncoding = false;
            return false;
        }
        Vector2I origin = SourceTileOrigin(
            source.Image, sourceTile, source.Interleaved);
        pixel = source.Image.GetPixel(origin.X + x, origin.Y + y);
        spriteEncoding = source.SpriteEncoding;
        return true;
    }

    public static Texture2D BuildMonochromeFontTexture(string path)
    {
        Image source = OracleGraphicsCache.LoadImage(path);
        Image output = Image.CreateEmpty(
            source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);
        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
        {
            output.SetPixel(
                x, y,
                source.GetPixel(x, y).R > 0.5f
                    ? Colors.White
                    : Colors.Transparent);
        }
        return ImageTexture.CreateFromImage(output);
    }

    public static Texture2D BuildTileMapTexture(
        byte[] map,
        byte[] flags,
        OracleVramTileMap tiles,
        Color[,] palettes,
        int columns = 32,
        int rows = 18)
    {
        int required = columns * rows;
        if (map.Length != required || flags.Length != required)
        {
            throw new ArgumentException(
                $"A {columns}x{rows} tilemap requires {required} map and flag bytes.");
        }
        Image output = Image.CreateEmpty(
            columns * 8, rows * 8, false, Image.Format.Rgba8);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int offset = row * columns + column;
            byte attributes = flags[offset];
            if (!tiles.TryResolve(
                (attributes >> 3) & 1, map[offset],
                out Image source, out int sourceTile))
            {
                continue;
            }
            DrawBackgroundTile(
                output, source, sourceTile, attributes, palettes,
                column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    public static Texture2D GetOamCellTexture(
        Image source,
        int tile,
        byte flags,
        Color[,] palettes,
        bool sourceGrayscaleInverted = true)
    {
        int paletteIndex = flags & 7;
        var palette = new Color[4];
        for (int shade = 0; shade < palette.Length; shade++)
            palette[shade] = palettes[paletteIndex, shade];
        return OracleGraphicsCache.GetOrCreateOamCell(
            source, tile, flags, palette, sourceGrayscaleInverted,
            () => BuildOamCellTexture(
                source, tile, flags, palette, sourceGrayscaleInverted));
    }

    public static void DrawTileToImage(
        Image output,
        Image source,
        int sourceTile,
        byte flags,
        Color[,] palette,
        int destinationX,
        int destinationY,
        bool interleaved = false,
        bool spriteEncoding = false)
    {
        Vector2I origin = SourceTileOrigin(source, sourceTile, interleaved);
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = flags & 7;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(
                origin.X + (flipX ? 7 - x : x),
                origin.Y + (flipY ? 7 - y : y));
            output.SetPixel(destinationX + x, destinationY + y,
                palette[paletteIndex,
                    OracleGraphicsData.PaletteShade(pixel, spriteEncoding)]);
        }
    }

    public static void DrawBackgroundTile(
        Image output,
        Image source,
        int sourceTile,
        byte flags,
        Color[,] palette,
        int destinationX,
        int destinationY,
        bool interleaved = false) =>
        DrawTileToImage(output, source, sourceTile, flags, palette,
            destinationX, destinationY, interleaved);

    public static void DrawOamTile(
        Node2D canvas,
        Image source,
        int tileBase,
        int tile,
        int paletteIndex,
        Vector2 position,
        bool flipX,
        bool flipY,
        Color[,] palette,
        bool inverted = true)
    {
        int sourceTile = tile - tileBase;
        int columns = source.GetWidth() / 8;
        int cell = sourceTile / 2;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sy = flipY ? 15 - y : y;
            Color pixel = source.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + sy);
            int shade = OracleGraphicsData.TwoBitShade(pixel);
            int color = inverted ? 3 - shade : shade;
            if (pixel.A < 0.1f || color == 0)
                continue;
            canvas.DrawRect(
                new Rect2(position + new Vector2(x, y), Vector2.One),
                palette[paletteIndex, color]);
        }
    }

    private static Texture2D BuildOamCellTexture(
        Image source,
        int tile,
        byte flags,
        Color[] palette,
        bool sourceGrayscaleInverted)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int columns = source.GetWidth() / 8;
        int cell = (tile & 0xfe) / 2;
        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(
                cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + (flipY ? 15 - y : y));
            int shade = OracleGraphicsData.TwoBitShade(pixel);
            int color = sourceGrayscaleInverted ? 3 - shade : shade;
            if (pixel.A >= 0.1f && color != 0)
                output.SetPixel(x, y, palette[color]);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Vector2I SourceTileOrigin(
        Image source,
        int sourceTile,
        bool interleaved)
    {
        int columns = source.GetWidth() / 8;
        if (!interleaved)
            return new Vector2I(sourceTile % columns * 8, sourceTile / columns * 8);
        int cell = sourceTile / 2;
        return new Vector2I(
            cell % columns * 8,
            cell / columns * 16 + (sourceTile & 1) * 8);
    }
}

internal readonly record struct OracleVramSource(
    int FirstTile,
    Image Image,
    bool Interleaved,
    bool SpriteEncoding = false)
{
    internal int TileCount =>
        Image.GetWidth() / 8 * (Image.GetHeight() / 8);
}

internal sealed class OracleVramTileMap
{
    private readonly (Image? Source, int Tile)[,] _tiles = new (Image?, int)[2, 256];

    internal void Map(Image source, int destination, int bank)
    {
        if ((uint)bank >= 2)
            throw new ArgumentOutOfRangeException(nameof(bank));
        int firstTile = destination >= 0x9000
            ? (destination - 0x9000) / 16
            : 0x80 + (destination - 0x8800) / 16;
        int count = source.GetWidth() / 8 * (source.GetHeight() / 8);
        for (int tile = 0; tile < count; tile++)
            _tiles[bank, (firstTile + tile) & 0xff] = (source, tile);
    }

    internal bool TryResolve(
        int bank,
        int tile,
        out Image source,
        out int sourceTile)
    {
        (Image? found, int foundTile) = _tiles[bank, tile & 0xff];
        if (found is null)
        {
            source = null!;
            sourceTile = 0;
            return false;
        }
        source = found;
        sourceTile = foundTile;
        return true;
    }
}
