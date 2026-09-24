using Godot;
using System;
using System.Diagnostics;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateBufferedTilemaps()
    {
        const string root = "res://assets/oracle/cutscenes/";
        byte[] map = OracleGraphicsData.ReadBytes(root + "map_nayru_singing_cutscene.bin", 576);
        byte[] flags = OracleGraphicsData.ReadBytes(root + "flags_nayru_singing_cutscene.bin", 576);
        Color[,] palettes = OracleGraphicsData.LoadPalette(root + "nayru_singing_bg_palette.bin");
        var tiles = new OracleVramTileMap();
        // GFXH_NAYRU_SINGING_CUTSCENE ($0c): $8800, $9000, $8801.
        tiles.Map(OracleGraphicsCache.LoadImage(root + "gfx_nayru_singing_cutscene_1.png"), 0x8800, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(root + "gfx_nayru_singing_cutscene_2.png"), 0x9000, 0);
        tiles.Map(OracleGraphicsCache.LoadImage(root + "gfx_nayru_singing_cutscene_3.png"), 0x8800, 1);
        AssertTilemapParity(map, flags, tiles, palettes, 32, 18);

        byte[] rgba = new byte[16 * 16 * 4];
        for (int pixel = 0; pixel < 256; pixel++)
        {
            rgba[pixel * 4] = (byte)pixel;
            rgba[pixel * 4 + 1] = 255;
            rgba[pixel * 4 + 2] = (byte)(255 - pixel);
            rgba[pixel * 4 + 3] = (byte)(pixel % 2 * 255);
        }
        using Image source = Image.CreateFromData(16, 16, false, Image.Format.Rgba8, rgba);
        var syntheticTiles = new OracleVramTileMap();
        syntheticTiles.Map(source, 0x9000, 0);
        syntheticTiles.Map(source, 0x9000, 1);
        byte[] syntheticMap = new byte[256];
        byte[] syntheticFlags = new byte[256];
        var syntheticPalettes = new Color[8, 4];
        for (int i = 0; i < 256; i++)
        {
            syntheticMap[i] = (byte)(i % 4);
            syntheticFlags[i] = (byte)i;
        }
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            syntheticPalettes[palette, shade] = new Color(
                (palette * 4 + shade) / 31.0f, shade / 3.0f, palette / 7.0f, shade / 3.0f);
        syntheticMap[255] = 0xff; // Unmapped cells retain zero RGBA.
        AssertTilemapParity(syntheticMap, syntheticFlags, syntheticTiles, syntheticPalettes, 32, 8);
        FailIf(!source.GetData().AsSpan().SequenceEqual(rgba), "Tilemap rendering mutated source RGBA bytes.");

        source.SetPixel(0, 0, Colors.White);
        syntheticPalettes[0, 0] = new Color(0.3f, 0.4f, 0.5f, 0.6f);
        syntheticTiles.MapTile(source, 3, 0, 1);
        AssertTilemapParity(syntheticMap, syntheticFlags, syntheticTiles, syntheticPalettes, 32, 8);

        // Float inputs near half-shade boundaries must not be quantized to
        // RGBA8 before resolving the original rounded shade.
        using Image floatSource = Image.CreateEmpty(8, 8, false, Image.Format.Rgbf);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            floatSource.SetPixel(x, y, new Color(0.5f + (x - 4) * 0.0001f, 0, 0));
        syntheticTiles.MapTile(floatSource, 0, 0, 1);
        AssertTilemapParity(syntheticMap, syntheticFlags, syntheticTiles, syntheticPalettes, 32, 8);

        if (System.Environment.GetEnvironmentVariable("OOA_BENCHMARK_TILEMAPS") == "1")
            BenchmarkTilemaps(map, flags, tiles, palettes);
        GD.Print("Validated buffered tilemaps against native pixel composition: Nayru $0c graphics, all attributes/red bytes, flips, banks, unmapped cells, palette quantization, source updates and float thresholds.");
    }

    private static void AssertTilemapParity(byte[] map, byte[] flags, OracleVramTileMap tiles,
        Color[,] palettes, int columns, int rows)
    {
        using Texture2D expectedTexture = BuildReferenceTilemap(map, flags, tiles, palettes, columns, rows);
        using Texture2D actualTexture = OracleTileRenderer.BuildTileMapTexture(map, flags, tiles, palettes, columns, rows);
        using Image expected = expectedTexture.GetImage();
        using Image actual = actualTexture.GetImage();
        FailIf(actual.GetWidth() != columns * 8 || actual.GetHeight() != rows * 8 ||
            !actual.GetData().AsSpan().SequenceEqual(expected.GetData()),
            "Buffered tilemap changed dimensions or RGBA bytes relative to native composition.");
    }

    private static Texture2D BuildReferenceTilemap(byte[] map, byte[] flags, OracleVramTileMap tiles,
        Color[,] palettes, int columns = 32, int rows = 18)
    {
        using Image output = Image.CreateEmpty(columns * 8, rows * 8, false, Image.Format.Rgba8);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            int offset = row * columns + column;
            byte attributes = flags[offset];
            if (tiles.TryResolve((attributes >> 3) & 1, map[offset], out Image source, out int sourceTile))
                OracleTileRenderer.DrawBackgroundTile(output, source, sourceTile, attributes, palettes, column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static void BenchmarkTilemaps(byte[] map, byte[] flags, OracleVramTileMap tiles, Color[,] palettes)
    {
        Action before = () => { using Texture2D texture = BuildReferenceTilemap(map, flags, tiles, palettes); };
        Action after = () => { using Texture2D texture = OracleTileRenderer.BuildTileMapTexture(map, flags, tiles, palettes); };
        for (int i = 0; i < 20; i++) { before(); after(); }
        var oldTimes = new double[7];
        var newTimes = new double[7];
        long oldBytes = 0, newBytes = 0;
        for (int sample = 0; sample < 7; sample++)
        {
            if ((sample & 1) == 0)
            {
                (oldTimes[sample], oldBytes) = Measure(before);
                (newTimes[sample], newBytes) = Measure(after);
            }
            else
            {
                (newTimes[sample], newBytes) = Measure(after);
                (oldTimes[sample], oldBytes) = Measure(before);
            }
        }
        Array.Sort(oldTimes); Array.Sort(newTimes);
        GD.Print(FormattableString.Invariant(
            $"TILEMAP_BENCHMARK Nayru 256x144 median_us before={oldTimes[3]:F3} after={newTimes[3]:F3}; managed_bytes/op before={oldBytes} after={newBytes}; 7 samples, 100 builds/sample, warmed sources, headless."));

        static (double Microseconds, long Bytes) Measure(Action action)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 100; i++) action();
            long elapsed = Stopwatch.GetTimestamp() - start;
            return (elapsed * 10000.0 / Stopwatch.Frequency,
                (GC.GetAllocatedBytesForCurrentThread() - allocated) / 100);
        }
    }
}
