using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static Color ReadValidationPixel(Image image, byte[]? rgba, int width, int x, int y)
    {
        // Generated sprite images are RGBA8. Preserve native GetPixel for any
        // other format rather than quantizing float/HDR images or changing the
        // existing comparison tolerance.
        if (rgba is null) return image.GetPixel(x, y);
        int at = (y * width + x) * 4;
        return new(rgba[at] / 255f, rgba[at + 1] / 255f, rgba[at + 2] / 255f, rgba[at + 3] / 255f);
    }

    private static void ValidateSpritePixelSnapshots()
    {
        var pixels = new byte[16 * 16 * 4];
        for (int value = 0; value < 256; value++)
        {
            pixels[value * 4] = (byte)value;
            pixels[value * 4 + 1] = (byte)(255 - value);
            pixels[value * 4 + 2] = (byte)(value * 37);
            pixels[value * 4 + 3] = (byte)(value * 13);
        }
        using Image image = Image.CreateFromData(16, 16, false, Image.Format.Rgba8, pixels);
        byte[] snapshot = image.GetData();
        OracleImagePixels runtimePixels = OracleGraphicsCache.GetImagePixels(image);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            FailIf(ReadValidationPixel(image, snapshot, 16, x, y) != image.GetPixel(x, y) ||
                runtimePixels.GetPixel(x, y) != image.GetPixel(x, y),
                $"Bulk sprite pixels changed native byte normalization at ({x},{y}).");
        using Image hdr = Image.CreateEmpty(1, 1, false, Image.Format.Rgbaf);
        hdr.SetPixel(0, 0, new(1.5f, -.25f, .125f, .5f));
        FailIf(ReadValidationPixel(hdr, null, 1, 0, 0) != hdr.GetPixel(0, 0) ||
            OracleGraphicsCache.GetImagePixels(hdr).GetPixel(0, 0) != hdr.GetPixel(0, 0),
            "Non-RGBA8 snapshots must preserve native floating-point channels without conversion.");
        image.SetPixel(0, 0, Colors.Magenta);
        FailIf(OracleGraphicsCache.GetImagePixels(image).GetPixel(0, 0) != Colors.Magenta ||
            runtimePixels.GetPixel(0, 0) == Colors.Magenta,
            "Caller-owned mutable graphics retained stale shared-cache pixels.");
        Image shared = OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + "gfx/spr_impa.png");
        FailIf(!ReferenceEquals(OracleGraphicsCache.GetImagePixels(shared), OracleGraphicsCache.GetImagePixels(shared)),
            "Immutable sprite art did not reuse its CPU snapshot.");
    }
}
