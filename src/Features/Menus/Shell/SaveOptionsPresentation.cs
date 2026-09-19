using Godot;
using System;
using System.Linq;
using static oracleofages.OracleGraphicsData;

namespace oracleofages;

/// <summary>Port-only labels using native file-menu capital strokes and panels.</summary>
internal static class SaveOptionsPresentation
{
    internal static Texture2D BuildSave(Image original)
    {
        using Image output = ClearChoices(original);
        for (int row = 0; row < 3; row++)
            output.BlitRect(original, new Rect2I(32, 56 + row * 24, 96, 16),
                new Vector2I(32, 48 + row * 24));
        DrawChoice(output, original, "OPTIONS", 120);
        return ImageTexture.CreateFromImage(output);
    }

    internal static Texture2D BuildOptions(Image original, bool noclip, bool overlay)
    {
        using Image output = ClearChoices(original);
        // Retain the title's native top/bottom edges, replacing only its text.
        for (int y = 9; y < 23; y++)
        for (int x = 48; x < 112; x++)
            output.SetPixel(x, y, original.GetPixel(112 + x % 8, y));
        DrawWord(output, "OPTIONS", 8, title: true);
        DrawChoice(output, original, noclip ? "NOCLIP: ON" : "NOCLIP: OFF", 56, left: 44);
        DrawChoice(output, original, overlay ? "ROOM ID: ON" : "ROOM ID: OFF", 80, left: 44);
        return ImageTexture.CreateFromImage(output);
    }

    private static Image ClearChoices(Image original)
    {
        Image output = (Image)original.Duplicate();
        output.FillRect(new Rect2I(32, 56, 96, 64), original.GetPixel(80, 48));
        return output;
    }

    private static void DrawChoice(Image output, Image original, string text, int top, int? left = null)
    {
        // Copy both ends of the actual CONTINUE plank. Only its letter area
        // needs blank wood; do not repeat a left end-cap across the whole row.
        output.BlitRect(original, new Rect2I(32, 56, 96, 16), new Vector2I(32, top));
        for (int y = 3; y < 16; y++)
        for (int x = 16; x < 80; x++)
            output.SetPixel(32 + x, top + y, original.GetPixel(112 + x % 15, 56 + y));
        DrawWord(output, text, top, left: left);
    }

    private static void DrawWord(Image output, string text, int top, bool title = false, int? left = null)
    {
        int pen = left ?? (160 - text.Sum(c => Cell(c).Width + 2)) / 2;
        Color[,] palette = LoadPalette("res://assets/oracle/menu/palette_file_bg.bin");
        foreach (char letter in text)
        {
            var cell = Cell(letter);
            if (letter == ':')
            {
                // Two two-pixel dots, matching the native capitals' stroke.
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    output.SetPixel(pen + 1 + x, top + 6 + y, palette[3, 3]);
                    output.SetPixel(pen + 1 + x, top + 11 + y, palette[3, 3]);
                }
            }
            else if (letter != ' ')
            {
                Image sheet = LoadPng($"res://assets/oracle/menu/{cell.Sheet}.png");
                for (int y = 0; y < 11; y++)
                for (int x = 0; x < cell.Width; x++)
                {
                    // M and R have a duplicated vertical-stem row in their
                    // twelve-row source. Remove only that row, not a cap.
                    int sy = cell.Y + y + (cell.Header && y >= 4 ? 1 : 0);
                    Color pixel = sheet.GetPixel(cell.X + x, sy);
                    bool ink = TwoBitShade(pixel) == 3;
                    // L/F reuse E's exact stem and bars. D uses O's bowl with
                    // a straight left stem. These extension letters keep the
                    // same eleven-row baseline and two-pixel stroke throughout.
                    if (letter == 'L') ink &= x < 2 || y >= 9;
                    if (letter == 'F') ink &= x < 2 || y < 9;
                    if (letter == 'D') ink |= x < 2;
                    // Leave the panel grain continuous instead of pasting
                    // rectangular wood fragments from unrelated letter sheets.
                    if (ink)
                        output.SetPixel(pen + 2 + x, top + 4 + y - (title ? 2 : 0), palette[3, 3]);
                }
            }
            pen += cell.Width + 2;
        }
    }

    private static (string Sheet, int X, int Y, int Width, bool Header) Cell(char c) => c switch
    {
        ' ' or ':' => ("", 0, 0, 2, false),
        'C' => ("gfx_savescreen", 18, 20, 6, false),
        'O' or 'D' => ("gfx_savescreen", 26, 20, 6, false),
        'N' => ("gfx_savescreen", 34, 20, 6, false),
        'T' => ("gfx_savescreen", 42, 20, 6, false),
        'I' => ("gfx_savescreen", 50, 20, 2, false),
        'S' => ("gfx_savescreen", 105, 20, 6, false),
        'P' => ("gfx_copy", 25, 4, 6, false),
        'R' => ("gfx_copywhatwhere", 102, 2, 6, true),
        'M' => ("gfx_name", 21, 2, 7, true),
        'L' or 'F' => ("gfx_savescreen", 70, 20, 6, false),
        _ => throw new InvalidOperationException($"No native file-menu letter cell for '{c}'.")
    };
}
