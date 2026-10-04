using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateBulkPaletteShades()
    {
        using Image source = Image.CreateEmpty(256, 1, false, Image.Format.Rgba8);
        for (int red = 0; red < 256; red++)
            source.SetPixel(red, 0, new Color(red / 255f, 0.25f, 0.75f, red % 2));
        Check();
        source.SetPixel(0, 0, Colors.White);
        source.SetPixel(255, 0, Colors.Black);
        Check(); // Snapshots must observe replaced graphics, rather than cache stale bytes.

        using Image floating = Image.CreateEmpty(9, 1, false, Image.Format.Rgbaf);
        float[] reds = [-0.1f, 0, 1 / 6f, 0.49999f, 0.5f, 0.50001f, 5 / 6f, 1, 1.1f];
        for (int index = 0; index < reds.Length; index++) floating.SetPixel(index, 0, new Color(reds[index], 0, 0));
        foreach (bool sprite in new[] { false, true })
        {
            byte[] shades = OracleTileRenderer.CapturePaletteShades(floating, sprite);
            for (int index = 0; index < reds.Length; index++)
                FailIf(shades[index] != OracleGraphicsData.PaletteShade(floating.GetPixel(index, 0), sprite),
                    $"Bulk shade conversion changed float thresholds or clamping at {reds[index]} (sprite={sprite}).");
        }

        void Check()
        {
            foreach (bool sprite in new[] { false, true })
            {
                byte[] shades = OracleTileRenderer.CapturePaletteShades(source, sprite);
                for (int red = 0; red < 256; red++)
                    FailIf(shades[red] != OracleGraphicsData.PaletteShade(source.GetPixel(red, 0), sprite),
                        $"Bulk shade conversion differs from the native pixel reader at red=${red:x2} (sprite={sprite}).");
            }
        }
    }
}
