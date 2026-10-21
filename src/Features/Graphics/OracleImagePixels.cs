using Godot;

namespace oracleofages;

/// <summary>CPU pixel access without per-pixel native calls for RGBA8 images.</summary>
internal sealed class OracleImagePixels
{
    private readonly Image _source;
    private readonly byte[]? _rgba;
    internal int Width { get; }
    internal int Height { get; }
    internal byte[]? RgbaPixels => _rgba;

    internal OracleImagePixels(Image source)
    {
        _source = source;
        Width = source.GetWidth();
        Height = source.GetHeight();
        if (source.GetFormat() == Image.Format.Rgba8) _rgba = source.GetData();
    }

    internal Color GetPixel(int x, int y)
    {
        // Preserve floating/HDR inputs without introducing an RGBA8 conversion.
        if (_rgba is null) return _source.GetPixel(x, y);
        int offset = (y * Width + x) * 4;
        return new Color(_rgba[offset] / 255f, _rgba[offset + 1] / 255f,
            _rgba[offset + 2] / 255f, _rgba[offset + 3] / 255f);
    }
}
