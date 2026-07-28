using Godot;
using System;

namespace oracleofages;

/// <summary>Shared validated asset reads and Game Boy palette conversion.</summary>
internal static class OracleGraphicsData
{
    public static Image LoadPng(string path) => OracleGraphicsCache.LoadImage(path);

    public static byte[] ReadBytes(string path, int expectedLength)
    {
        byte[] data = FileAccess.GetFileAsBytes(path);
        if (data.Length != expectedLength)
        {
            throw new InvalidOperationException(
                $"{path} should contain {expectedLength} bytes, got {data.Length}.");
        }
        return data;
    }

    public static Color[,] LoadPalette(string path) => LoadPalette(path, 8, 0);

    public static Color[,] LoadPalette(string path, int count) =>
        LoadPalette(path, count, 0);

    public static Color[,] LoadPalette(string path, int count, int firstPalette)
    {
        if (count < 0 || firstPalette < 0 || firstPalette + count > 8)
            throw new ArgumentOutOfRangeException(nameof(count));
        byte[] bytes = ReadBytes(path, count * 4 * 3);
        var result = new Color[8, 4];
        for (int palette = 0; palette < count; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            result[firstPalette + palette, shade] = new Color(
                bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f,
                bytes[offset + 2] / 31.0f);
        }
        return result;
    }

    public static Color[,] LoadPaletteWithCommonBackgroundFallback(
        string path,
        int count,
        int firstPalette)
    {
        Color[] fallback = LoadCommonBackgroundPalette();
        Color[,] result = LoadPalette(path, count, firstPalette);
        for (int palette = 0; palette < result.GetLength(0); palette++)
        for (int shade = 0; shade < result.GetLength(1); shade++)
        {
            if (palette < firstPalette || palette >= firstPalette + count)
                result[palette, shade] = fallback[shade];
        }
        return result;
    }

    public static Color[] LoadCommonBackgroundPalette()
    {
        byte[] bytes = ReadBytes(
            "res://assets/oracle/metadata/commonBgPalette0.bin", 12);
        var result = new Color[4];
        for (int shade = 0; shade < result.Length; shade++)
        {
            result[shade] = new Color(
                bytes[shade * 3] / 31.0f,
                bytes[shade * 3 + 1] / 31.0f,
                bytes[shade * 3 + 2] / 31.0f);
        }
        return result;
    }

    public static void Overlay(
        byte[] destination,
        byte[] source,
        int offset,
        int? count = null) =>
        Array.Copy(source, 0, destination, offset, count ?? source.Length);

    public static int Shade(Color color) => TwoBitShade(color);

    public static int TwoBitShade(Color color) =>
        Math.Clamp(Mathf.RoundToInt((1.0f - color.R) * 3.0f), 0, 3);

    public static int PaletteShade(Color color, bool spriteEncoding) =>
        spriteEncoding
            ? ItemIconAtlas.ShadeFromPng(color, out _)
            : TwoBitShade(color);
}
