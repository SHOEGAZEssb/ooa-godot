using Godot;

namespace oracleofages;

/// <summary>Shared 8x8 background and 8x16 OAM tile addressing/rendering.</summary>
internal static class OracleTileRenderer
{
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
