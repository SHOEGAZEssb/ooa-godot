using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Draws imported Link/sparkle 8x16 OBJ compositions with GBC OAM priority,
/// byte wrapping, flips, and standard sprite palettes.
/// </summary>
internal sealed class CutsceneSpriteRenderer
{
    private static readonly Color[,] SpritePalettes =
    {
        { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x02, 0x15, 0x08), GbcColor(0x1f, 0x1a, 0x11) },
        { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x03, 0x10, 0x1f), GbcColor(0x1f, 0x1a, 0x11) },
        { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x01, 0x05), GbcColor(0x1f, 0x1a, 0x11) },
        { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x0f, 0x01), GbcColor(0x1f, 0x1a, 0x11) },
        { Colors.Transparent, GbcColor(0x0e, 0x15, 0x1f), GbcColor(0x00, 0x00, 0x1f), GbcColor(0x00, 0x00, 0x00) },
        { Colors.Transparent, GbcColor(0x1f, 0x16, 0x06), GbcColor(0x1b, 0x00, 0x00), GbcColor(0x00, 0x00, 0x00) }
    };

    private readonly Dictionary<long, Texture2D> _cells = new();
    private readonly Image _source =
        OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_link.png");

    public void DrawScreenFrame(
        CanvasItem target,
        NewGameIntroDatabase.IntroSpriteFrame frame,
        int objectY,
        int objectX)
    {
        // Lower OAM indices cover higher indices, so compose in reverse.
        for (int index = frame.Parts.Length - 1; index >= 0; index--)
        {
            NewGameIntroDatabase.IntroOamPart part = frame.Parts[index];
            int rawY = (objectY + part.Y) & 0xff;
            int rawX = (objectX + part.X) & 0xff;
            if (rawY >= 0xa0 || rawX >= 0xa8)
                continue;

            target.DrawTexture(
                CellTexture(frame, part),
                new Vector2(rawX - 8, rawY - 16));
        }
    }

    public void DrawRelativeFrame(
        CanvasItem target,
        NewGameIntroDatabase.IntroSpriteFrame frame,
        int z)
    {
        // Player.Position is the existing port's sprite center. Subtract the
        // hardware OAM origins so ordinary Link OAM $00 joins its normal pose
        // without moving when the slow fall reaches z=0.
        for (int index = frame.Parts.Length - 1; index >= 0; index--)
        {
            NewGameIntroDatabase.IntroOamPart part = frame.Parts[index];
            target.DrawTexture(
                CellTexture(frame, part),
                new Vector2(ToSignedByte(part.X) - 8, ToSignedByte(part.Y) - 16 + z));
        }
    }

    private Texture2D CellTexture(
        NewGameIntroDatabase.IntroSpriteFrame frame,
        NewGameIntroDatabase.IntroOamPart part)
    {
        int palette = (frame.BasePalette ^ part.Flags) & 0x07;
        bool flipX = (part.Flags & 0x20) != 0;
        bool flipY = (part.Flags & 0x40) != 0;
        long key = (uint)frame.SourceOffset | ((long)(uint)part.Tile << 16) |
            ((long)(uint)palette << 24) |
            (flipX ? 1L << 28 : 0) | (flipY ? 1L << 29 : 0);
        if (_cells.TryGetValue(key, out Texture2D? cached))
            return cached;

        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sourcePixelX = flipX ? 7 - x : x;
            int sourcePixelY = flipY ? 15 - y : y;
            Vector2I sourcePixel = SourcePixelForValidation(
                frame.SourceOffset, part.Tile, sourcePixelX, sourcePixelY);
            output.SetPixel(
                x, y, Recolor(_source.GetPixel(sourcePixel.X, sourcePixel.Y), palette));
        }
        Texture2D texture = ImageTexture.CreateFromImage(output);
        _cells[key] = texture;
        return texture;
    }

    internal static Vector2I SourcePixelForValidation(
        int sourceOffset,
        int tileOffset,
        int x,
        int y)
    {
        int cell = sourceOffset / 32 + (tileOffset & 0xfe) / 2;
        return new Vector2I((cell % 16) * 8 + x, (cell / 16) * 16 + y);
    }

    private static int ToSignedByte(int value) => value >= 0x80 ? value - 0x100 : value;

    private static Color Recolor(Color source, int palette)
    {
        int shade = source.R < 0.1f ? 0
            : source.R < 0.5f ? 1
            : source.R < 0.9f ? 2
            : 3;
        return SpritePalettes[palette, shade];
    }

    private static Color GbcColor(int red, int green, int blue) =>
        new(red / 31.0f, green / 31.0f, blue / 31.0f);
}
