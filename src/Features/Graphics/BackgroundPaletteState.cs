using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Owns the eight live GBC background-palette slots used during gameplay.
/// Palette-header loads replace only their addressed slots, so textbox slot 1
/// survives textbox close while room and darkening writes remain confined to
/// slots 2-7.
/// </summary>
public sealed class BackgroundPaletteState
{
    internal const int PaletteCount = 8;
    internal const int ColorsPerPalette = 4;

    private const int TextboxPaletteCount = 3;
    private readonly Color[,] _colors = new Color[PaletteCount, ColorsPerPalette];
    private readonly Color[,] _textboxPalette1 =
        new Color[TextboxPaletteCount, ColorsPerPalette];

    internal event Action? Changed;

    internal BackgroundPaletteState(
        Color[] commonPalette0,
        Color[,] textboxPalette1)
    {
        if (commonPalette0.Length != ColorsPerPalette)
        {
            throw new ArgumentException(
                "PALH_0f must provide one four-color BG palette.",
                nameof(commonPalette0));
        }
        if (textboxPalette1.GetLength(0) != TextboxPaletteCount ||
            textboxPalette1.GetLength(1) != ColorsPerPalette)
        {
            throw new ArgumentException(
                "Textbox palette data must provide PALH_0e, PALH_0d, and " +
                "PALH_bd as three four-color BG slot-1 palettes.",
                nameof(textboxPalette1));
        }

        for (int palette = 0; palette < PaletteCount; palette++)
        for (int shade = 0; shade < ColorsPerPalette; shade++)
            _colors[palette, shade] = Colors.Black;

        for (int shade = 0; shade < ColorsPerPalette; shade++)
        {
            _colors[0, shade] = commonPalette0[shade];
            for (int palette = 0; palette < TextboxPaletteCount; palette++)
                _textboxPalette1[palette, shade] = textboxPalette1[palette, shade];
        }
    }

    internal Color Resolve(int palette, int shade)
    {
        if (palette is < 0 or >= PaletteCount ||
            shade is < 0 or >= ColorsPerPalette)
        {
            throw new ArgumentOutOfRangeException(nameof(palette));
        }
        return _colors[palette, shade];
    }

    internal void LoadTileset(Color[,] palettes)
    {
        ValidateSixPalettes(palettes, nameof(palettes));
        WriteSixPalettes(
            (palette, shade) => palettes[palette, shade]);
    }

    internal void LoadTextboxPalette(int textboxFlags)
    {
        // initTextbox returns before loadPaletteHeader when NOCOLORS is set.
        if ((textboxFlags & 0x01) != 0)
            return;

        int source = (textboxFlags & 0x10) != 0
            ? 2
            : (textboxFlags & 0x04) != 0
                ? 1
                : 0;
        WritePalette(1, shade => _textboxPalette1[source, shade]);
    }

    internal void LoadPaletteHeader(Color[,,] palettes, int header)
    {
        if (palettes.GetLength(0) != 4 ||
            palettes.GetLength(1) != 6 ||
            palettes.GetLength(2) != ColorsPerPalette ||
            header < 0 || header >= palettes.GetLength(0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(palettes),
                "The palette-header effect requires four headers of six " +
                "four-color BG palettes.");
        }
        WriteSixPalettes(
            (palette, shade) => palettes[header, palette, shade]);
    }

    internal void BlendTileset(Color[,] source, Color[,] destination, float blend)
    {
        ValidateSixPalettes(source, nameof(source));
        ValidateSixPalettes(destination, nameof(destination));
        float amount = Mathf.Clamp(blend, 0.0f, 1.0f);
        WriteSixPalettes(
            (palette, shade) =>
                source[palette, shade].Lerp(destination[palette, shade], amount));
    }

    internal void OffsetTileset(Color[,] palettes, int offset)
    {
        ValidateSixPalettes(palettes, nameof(palettes));
        if (offset is < -31 or > 31)
            throw new ArgumentOutOfRangeException(nameof(offset));
        WriteSixPalettes(
            (palette, shade) => OffsetGbcColor(palettes[palette, shade], offset));
    }

    private void WritePalette(int palette, Func<int, Color> color)
    {
        bool changed = false;
        for (int shade = 0; shade < ColorsPerPalette; shade++)
        {
            Color value = color(shade);
            if (_colors[palette, shade] == value)
                continue;
            _colors[palette, shade] = value;
            changed = true;
        }
        if (changed)
            Changed?.Invoke();
    }

    private void WriteSixPalettes(Func<int, int, Color> color)
    {
        bool changed = false;
        for (int palette = 0; palette < 6; palette++)
        for (int shade = 0; shade < ColorsPerPalette; shade++)
        {
            Color value = color(palette, shade);
            if (_colors[palette + 2, shade] == value)
                continue;
            _colors[palette + 2, shade] = value;
            changed = true;
        }
        if (changed)
            Changed?.Invoke();
    }

    private static void ValidateSixPalettes(Color[,] palettes, string parameter)
    {
        if (palettes.GetLength(0) != 6 ||
            palettes.GetLength(1) != ColorsPerPalette)
        {
            throw new ArgumentException(
                "A room palette write requires BG slots 2-7 as six " +
                "four-color palettes.",
                parameter);
        }
    }

    private static Color OffsetGbcColor(Color color, int offset)
    {
        int red = Mathf.Clamp(
            Mathf.RoundToInt(color.R * 31.0f) + offset, 0, 31);
        int green = Mathf.Clamp(
            Mathf.RoundToInt(color.G * 31.0f) + offset, 0, 31);
        int blue = Mathf.Clamp(
            Mathf.RoundToInt(color.B * 31.0f) + offset, 0, 31);
        return new Color(
            red / 31.0f, green / 31.0f, blue / 31.0f, color.A);
    }
}
