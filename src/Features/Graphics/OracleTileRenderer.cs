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

    public static Texture2D BuildMonochromeFontTexture(string path) =>
        OracleGraphicsCache.LoadMonochromeFont(path);

    internal static Texture2D BuildMonochromeFontTexture(Image source)
    {
        if (source.GetFormat() != Image.Format.Rgba8)
            throw new ArgumentException("Monochrome fonts require an RGBA8 source image.", nameof(source));
        // GetData returns a copy: preserve the shared source and upload once.
        byte[] pixels = source.GetData();
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            byte value = pixels[offset] > 127 ? (byte)255 : (byte)0;
            // Colors.Transparent retains white RGB beneath alpha zero.
            pixels[offset] = 255;
            pixels[offset + 1] = 255;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = value;
        }
        using Image output = Image.CreateFromData(
            source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8, pixels);
        return ImageTexture.CreateFromImage(output);
    }

    public static Texture2D BuildTileMapTexture(
        byte[] map,
        byte[] flags,
        OracleVramTileMap tiles,
        Color[,] palettes,
        int columns = 32,
        int rows = 18,
        Func<byte, byte, int>? missingTileShade = null)
    {
        int required = columns * rows;
        if (map.Length != required || flags.Length != required)
        {
            throw new ArgumentException(
                $"A {columns}x{rows} tilemap requires {required} map and flag bytes.");
        }
        int width = columns * 8;
        byte[] pixels = new byte[width * rows * 8 * 4];
        // Quantize the palette with Godot, exactly as SetPixel did. Sources
        // and palette snapshots belong to this draw, not a persistent cache:
        // frontend animation can replace tiles or mutate their graphics.
        using Image colors = Image.CreateEmpty(4, palettes.GetLength(0), false, Image.Format.Rgba8);
        for (int palette = 0; palette < palettes.GetLength(0); palette++)
        for (int shade = 0; shade < 4; shade++)
            colors.SetPixel(shade, palette, palettes[palette, shade]);
        byte[] palettePixels = colors.GetData();
        var sources = new Dictionary<Image, (byte[] Shades, int Width)>();
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int offset = row * columns + column;
            byte attributes = flags[offset];
            if (!tiles.TryResolve(
                (attributes >> 3) & 1, map[offset],
                out Image source, out int sourceTile))
            {
                if (missingTileShade is not null)
                {
                    int blank = ((attributes & 7) * 4 + missingTileShade(map[offset], attributes)) * 4;
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        palettePixels.AsSpan(blank, 4).CopyTo(pixels.AsSpan(
                            ((row * 8 + y) * width + column * 8 + x) * 4, 4));
                }
                continue;
            }
            if (!sources.TryGetValue(source, out var captured))
            {
                captured = (CaptureBackgroundShades(source), source.GetWidth());
                sources.Add(source, captured);
            }
            int sourceColumns = captured.Width / 8;
            int sourceX = sourceTile % sourceColumns * 8;
            int sourceY = sourceTile / sourceColumns * 8;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + ((attributes & 0x20) != 0 ? 7 - x : x);
                int readY = sourceY + ((attributes & 0x40) != 0 ? 7 - y : y);
                int shade = captured.Shades[readY * captured.Width + readX];
                int paletteOffset = ((attributes & 7) * 4 + shade) * 4;
                int outputOffset = ((row * 8 + y) * width + column * 8 + x) * 4;
                palettePixels.AsSpan(paletteOffset, 4).CopyTo(pixels.AsSpan(outputOffset, 4));
            }
        }
        using Image output = Image.CreateFromData(width, rows * 8, false, Image.Format.Rgba8, pixels);
        return ImageTexture.CreateFromImage(output);
    }

    private static byte[] CaptureBackgroundShades(Image source)
    {
        int width = source.GetWidth();
        int height = source.GetHeight();
        byte[] shades = new byte[width * height];
        if (source.GetFormat() == Image.Format.Rgba8)
        {
            byte[] rgba = source.GetData();
            for (int pixel = 0; pixel < shades.Length; pixel++)
                shades[pixel] = (byte)((255 - rgba[pixel * 4] + 42) / 85);
        }
        else
        {
            // Preserve the original float threshold for non-RGBA8 images;
            // quantizing them first could change a shade at its boundary.
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                shades[y * width + x] = (byte)OracleGraphicsData.TwoBitShade(source.GetPixel(x, y));
        }
        return shades;
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
        // The hardware OAM tile field is one byte, but callers may add an
        // imported source-sheet offset before reaching this renderer. Clear
        // only the 8x16 pairing bit so offsets such as spr_link+$1c00 retain
        // their high tile-address bits.
        int cell = (tile & ~1) / 2;
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

    internal void MapTile(
        Image source,
        int sourceTile,
        int destinationTile,
        int bank)
    {
        if ((uint)bank >= 2)
            throw new ArgumentOutOfRangeException(nameof(bank));
        int count = source.GetWidth() / 8 * (source.GetHeight() / 8);
        if ((uint)sourceTile >= count)
            throw new ArgumentOutOfRangeException(nameof(sourceTile));
        if ((uint)destinationTile >= 256)
            throw new ArgumentOutOfRangeException(nameof(destinationTile));
        _tiles[bank, destinationTile] = (source, sourceTile);
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
