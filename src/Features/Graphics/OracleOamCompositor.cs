using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal static class OracleOamCompositor
{
    internal static Texture2D BuildOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride = null,
        bool sourceGrayscaleInverted = true,
        int sourceOffset = 0)
    {
        OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            sourceOffset,
            paletteOverride,
            paletteOverrides: null,
            sourceGrayscaleInverted,
            CompositionMode.Fixed32,
            () => new OamFrame(
                BuildOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride, paletteOverrides: null,
                    sourceGrayscaleInverted, sourceOffset),
                new Vector2(-16, -16)));
        return frame.Texture;
    }

    internal static Texture2D BuildOamTextureWithPaletteOverrides(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        IReadOnlyDictionary<int, Color[]> paletteOverrides,
        bool sourceGrayscaleInverted = true,
        int sourceOffset = 0)
    {
        OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            sourceOffset,
            paletteOverride: null,
            paletteOverrides,
            sourceGrayscaleInverted,
            CompositionMode.Fixed32,
            () => new OamFrame(
                BuildOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride: null, paletteOverrides,
                    sourceGrayscaleInverted, sourceOffset),
                new Vector2(-16, -16)));
        return frame.Texture;
    }

    internal static Texture2D BuildOamTextureUncachedForValidation(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride = null,
        bool sourceGrayscaleInverted = true,
        int sourceOffset = 0) => BuildOamTextureUncached(
            source, encodedOam, tileBase, basePalette,
            paletteOverride, paletteOverrides: null, sourceGrayscaleInverted,
            sourceOffset);

    private static Texture2D BuildOamTextureUncached(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides,
        bool sourceGrayscaleInverted,
        int sourceOffset)
    {
        // Imported NPC animations may suffix their final OAM frame with the
        // nonzero animation-loop target (`~N`). Consumers that render one
        // frame directly do not need that sequence metadata.
        int loopMarker = encodedOam.LastIndexOf('~');
        if (loopMarker >= 0)
            encodedOam = encodedOam[..loopMarker];
        var output = new OracleImageCanvas(32, 32);
        string[] blocks = encodedOam.Split(';', StringSplitOptions.RemoveEmptyEntries);
        DrawOamCells(output, source, blocks, tileBase, basePalette, paletteOverride,
            paletteOverrides, sourceGrayscaleInverted, sourceOffset, Vector2I.Zero);

        using Image image = output.ToImage();
        return ImageTexture.CreateFromImage(image);
    }

    internal static (Texture2D Texture, Vector2 Offset) BuildPositionedOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted,
        int sourceOffset = 0)
    {
        OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            sourceOffset,
            paletteOverride,
            paletteOverrides: null,
            sourceGrayscaleInverted,
            CompositionMode.Positioned,
            () =>
            {
                (Texture2D texture, Vector2 offset) = BuildPositionedOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride, paletteOverrides: null,
                    sourceGrayscaleInverted, sourceOffset);
                return new OamFrame(texture, offset);
            });
        return (frame.Texture, frame.Offset);
    }

    internal static (Texture2D Texture, Vector2 Offset)
        BuildPositionedOamTextureWithPaletteOverrides(
            Image source,
            string encodedOam,
            int tileBase,
            int basePalette,
            IReadOnlyDictionary<int, Color[]> paletteOverrides,
            bool sourceGrayscaleInverted = true,
            int sourceOffset = 0)
    {
        OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            sourceOffset,
            paletteOverride: null,
            paletteOverrides,
            sourceGrayscaleInverted,
            CompositionMode.Positioned,
            () =>
            {
                (Texture2D texture, Vector2 offset) =
                    BuildPositionedOamTextureUncached(
                        source, encodedOam, tileBase, basePalette,
                        paletteOverride: null, paletteOverrides,
                        sourceGrayscaleInverted, sourceOffset);
                return new OamFrame(texture, offset);
            });
        return (frame.Texture, frame.Offset);
    }

    internal static (Texture2D Texture, Vector2 Offset)
        BuildPositionedOamTextureUncachedForValidation(
            Image source,
            string encodedOam,
            int tileBase,
            int basePalette,
            Color[]? paletteOverride,
            bool sourceGrayscaleInverted,
            int sourceOffset = 0) => BuildPositionedOamTextureUncached(
                source, encodedOam, tileBase, basePalette,
                paletteOverride, paletteOverrides: null,
                sourceGrayscaleInverted, sourceOffset);

    internal static (Texture2D Texture, Vector2 Offset)
        BuildPositionedOamTextureWithPaletteOverridesUncachedForValidation(
            Image source,
            string encodedOam,
            int tileBase,
            int basePalette,
            IReadOnlyDictionary<int, Color[]> paletteOverrides,
            bool sourceGrayscaleInverted = true,
            int sourceOffset = 0) => BuildPositionedOamTextureUncached(
                source, encodedOam, tileBase, basePalette,
                paletteOverride: null, paletteOverrides,
                sourceGrayscaleInverted, sourceOffset);

    private static (Texture2D Texture, Vector2 Offset) BuildPositionedOamTextureUncached(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides,
        bool sourceGrayscaleInverted,
        int sourceOffset)
    {
        string[] blocks = encodedOam.Split(';', StringSplitOptions.RemoveEmptyEntries);
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        foreach (string block in blocks)
        {
            string[] fields = block.Split(',');
            if (fields.Length != 4)
                continue;
            int destinationX = ToSignedByte(int.Parse(fields[1])) + 8;
            int destinationY = ToSignedByte(int.Parse(fields[0]));
            minX = Math.Min(minX, destinationX);
            minY = Math.Min(minY, destinationY);
            maxX = Math.Max(maxX, destinationX + 8);
            maxY = Math.Max(maxY, destinationY + 16);
        }
        if (minX == int.MaxValue)
            return (BuildOamTextureUncached(
                source, encodedOam, tileBase, basePalette, paletteOverride,
                paletteOverrides, sourceGrayscaleInverted, sourceOffset),
                new Vector2(-16, -16));

        var output = new OracleImageCanvas(maxX - minX, maxY - minY);
        DrawOamCells(output, source, blocks, tileBase, basePalette, paletteOverride,
            paletteOverrides, sourceGrayscaleInverted, sourceOffset, new Vector2I(minX, minY));

        // The legacy 32x32 compositor drew at (-16,-16). Keep that coordinate
        // system while allowing large actors such as the Maku Tree to extend
        // above and to either side of the interaction origin.
        using Image image = output.ToImage();
        return (ImageTexture.CreateFromImage(image), new Vector2(minX - 16, minY - 16));
    }

    private static void DrawOamCells(
        OracleImageCanvas output, Image source, string[] blocks, int tileBase,
        int basePalette, Color[]? paletteOverride,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides,
        bool sourceGrayscaleInverted, int sourceOffset, Vector2I origin)
    {
        OracleImagePixels pixels = OracleGraphicsCache.GetImagePixels(source);
        // Lower OAM indices win when sprites overlap. Compose in reverse so
        // the first block has the same priority as it does on the Game Boy.
        for (int blockIndex = blocks.Length - 1; blockIndex >= 0; blockIndex--)
        {
            string block = blocks[blockIndex];
            string[] fields = block.Split(',');
            if (fields.Length != 4)
                continue;

            int oamY = ToSignedByte(int.Parse(fields[0]));
            int oamX = ToSignedByte(int.Parse(fields[1]));
            int tile = int.Parse(fields[2]);
            int flags = int.Parse(fields[3]);
            int cell = SourceCell(sourceOffset, tileBase, tile);
            int cellsPerRow = Math.Max(1, pixels.Width / 8);
            int sourceX = cell % cellsPerRow * 8;
            int sourceY = cell / cellsPerRow * 16;
            int destinationX = oamX + 8 - origin.X;
            int destinationY = oamY - origin.Y;
            bool flipX = (flags & 0x20) != 0;
            bool flipY = (flags & 0x40) != 0;
            int palette = basePalette ^ (flags & 0x07);
            Color[]? blockPalette = paletteOverrides is not null &&
                paletteOverrides.TryGetValue(palette, out Color[]? overridePalette)
                    ? overridePalette
                    : paletteOverride;

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (flipX ? 7 - x : x);
                int readY = sourceY + (flipY ? 15 - y : y);
                int writeX = destinationX + x;
                int writeY = destinationY + y;
                if (readX < 0 || readX >= pixels.Width || readY < 0 || readY >= pixels.Height ||
                    writeX < 0 || writeX >= output.Width || writeY < 0 || writeY >= output.Height)
                    continue;
                Color pixel = RecolorSpritePixel(
                    pixels.GetPixel(readX, readY), palette, blockPalette,
                    sourceGrayscaleInverted);
                if (pixel.A > 0.1f)
                    output.SetPixel(writeX, writeY, pixel);
            }
        }

    }

    private static int SourceCell(int sourceOffset, int tileBase, int tile)
    {
        if (sourceOffset < 0 || (sourceOffset & 0x0f) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceOffset),
                "Object graphics source offsets must be non-negative and 8x8-tile aligned.");
        }
        return sourceOffset / 32 + ((tileBase + tile) & 0xfe) / 2;
    }

    private static int ToSignedByte(int value) => value >= 0x80 ? value - 0x100 : value;

    private static Color RecolorSpritePixel(
        Color source,
        int palette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        int color = Mathf.Clamp(Mathf.RoundToInt(
            (sourceGrayscaleInverted ? source.R : 1.0f - source.R) * 3.0f), 0, 3);
        if (source.A < 0.1f || color == 0)
            return Colors.Transparent;
        if (paletteOverride is not null)
            return paletteOverride[color];
        Color[][] palettes = StandardSpritePalettes;
        if (palette < 0 || palette >= palettes.Length)
            palette = 1;
        return palettes[palette][color];
    }

    private static readonly Color[][] StandardSpritePalettes =
    {
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x02, 0x15, 0x08), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x03, 0x10, 0x1f), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x01, 0x05), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x0f, 0x01), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x0e, 0x15, 0x1f), GbcColor(0x00, 0x00, 0x1f), GbcColor(0x00, 0x00, 0x00) },
        new[] { Colors.Transparent, GbcColor(0x1f, 0x16, 0x06), GbcColor(0x1b, 0x00, 0x00), GbcColor(0x00, 0x00, 0x00) }
    };

    internal static Color[] GetStandardSpritePalette(int palette)
    {
        if (palette < 0 || palette >= StandardSpritePalettes.Length)
            palette = 1;
        return (Color[])StandardSpritePalettes[palette].Clone();
    }

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }
}

internal readonly record struct OamFrame(Texture2D Texture, Vector2 Offset);
