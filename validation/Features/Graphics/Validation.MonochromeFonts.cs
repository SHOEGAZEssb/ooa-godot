using Godot;
using System;
using System.Diagnostics;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateMonochromeFonts()
    {
        Texture2D? previous = null;
        foreach (string path in new[]
        {
            "res://assets/oracle/gfx/gfx_font.png",
            "res://assets/oracle/gfx/gfx_font_jp.png"
        })
        {
            Image source = OracleGraphicsCache.LoadImage(path);
            byte[] original = source.GetData();
            using Texture2D reference = BuildReferenceMonochromeFont(source);
            using Texture2D converted = OracleTileRenderer.BuildMonochromeFontTexture(source);
            Texture2D shared = OracleTileRenderer.BuildMonochromeFontTexture(path);
            using Image expected = reference.GetImage();
            using Image actual = converted.GetImage();
            using Image cached = shared.GetImage();
            FailIf(actual.GetWidth() != source.GetWidth() || actual.GetHeight() != source.GetHeight() ||
                !actual.GetData().AsSpan().SequenceEqual(expected.GetData()) ||
                !cached.GetData().AsSpan().SequenceEqual(expected.GetData()) ||
                !source.GetData().AsSpan().SequenceEqual(original),
                $"Monochrome font conversion changed pixels, dimensions, or source bytes: {path}.");
            FailIf(!ReferenceEquals(shared, OracleTileRenderer.BuildMonochromeFontTexture(path)) ||
                ReferenceEquals(shared, previous),
                $"Monochrome font cache did not reuse or distinguish its source: {path}.");
            previous = shared;
        }

        // Exercise the strict red threshold, independently of the artwork's
        // binary colors. Green, blue and source alpha must not affect the mask.
        using Image threshold = Image.CreateFromData(4, 1, false, Image.Format.Rgba8,
            new byte[] { 0, 255, 255, 255, 127, 255, 255, 255, 128, 0, 0, 0, 255, 0, 0, 0 });
        using Texture2D thresholdTexture = OracleTileRenderer.BuildMonochromeFontTexture(threshold);
        using Image thresholdResult = thresholdTexture.GetImage();
        FailIf(!thresholdResult.GetData().AsSpan().SequenceEqual(new byte[]
            { 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 255, 255, 255, 255, 255 }),
            "Monochrome font threshold changed at red $7f/$80 or used source alpha.");

        // Opt-in timing keeps routine regressions deterministic and inexpensive.
        if (System.Environment.GetEnvironmentVariable("OOA_BENCHMARK_FONTS") == "1")
            BenchmarkMonochromeFonts();
        GD.Print("Validated byte-identical shared dialogue/menu fonts, source immutability, and red $7f/$80 threshold.");
    }

    private static Texture2D BuildReferenceMonochromeFont(Image source)
    {
        using Image output = Image.CreateEmpty(source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);
        // Previous production path, including its native dimension/pixel calls.
        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
            output.SetPixel(x, y, source.GetPixel(x, y).R > 0.5f ? Colors.White : Colors.Transparent);
        return ImageTexture.CreateFromImage(output);
    }

    private static void BenchmarkMonochromeFonts()
    {
        const string path = "res://assets/oracle/gfx/gfx_font.png";
        Image source = OracleGraphicsCache.LoadImage(path);
        Action before = () => { using Texture2D texture = BuildReferenceMonochromeFont(source); };
        Action after = () => { using Texture2D texture = OracleTileRenderer.BuildMonochromeFontTexture(source); };
        Action hit = () => { _ = OracleTileRenderer.BuildMonochromeFontTexture(path); };
        for (int i = 0; i < 20; i++) { before(); after(); hit(); }
        var oldTimes = new double[7];
        var newTimes = new double[7];
        var hitTimes = new double[7];
        long oldBytes = 0, newBytes = 0, hitBytes = 0;
        for (int sample = 0; sample < 7; sample++)
        {
            // Alternate order to reduce systematic warm-up/order bias.
            if ((sample & 1) == 0)
            {
                (oldTimes[sample], oldBytes) = MeasureFontOperation(before, 100);
                (newTimes[sample], newBytes) = MeasureFontOperation(after, 100);
            }
            else
            {
                (newTimes[sample], newBytes) = MeasureFontOperation(after, 100);
                (oldTimes[sample], oldBytes) = MeasureFontOperation(before, 100);
            }
            (hitTimes[sample], hitBytes) = MeasureFontOperation(hit, 100000);
        }
        Array.Sort(oldTimes); Array.Sort(newTimes); Array.Sort(hitTimes);
        GD.Print(FormattableString.Invariant(
            $"FONT_BENCHMARK {source.GetWidth()}x{source.GetHeight()} median_us before={oldTimes[3]:F3} after={newTimes[3]:F3} cached={hitTimes[3]:F3}; managed_bytes/op before={oldBytes} after={newBytes} cached={hitBytes}; 7 samples, 100 builds/sample, 100000 hits/sample, warmed source, headless."));
    }

    private static (double Microseconds, long Bytes) MeasureFontOperation(Action action, int count)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) action();
        long elapsed = Stopwatch.GetTimestamp() - start;
        return (elapsed * 1000000.0 / Stopwatch.Frequency / count,
            (GC.GetAllocatedBytesForCurrentThread() - allocated) / count);
    }
}
