using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Screen-space presentation for CUTSCENE_PREGAME_INTRO. Link and the two
/// INTERAC_SPARKLE objects are composed from their imported graphics, OAM,
/// palette, animation, and unsigned object-coordinate records.
/// </summary>
public partial class NewGameIntroScreen : Node2D
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
    private NewGameIntroDatabase.NewGameIntroRecord _record;
    private NewGameIntroDatabase.IntroSpriteFrame[] _linkSpin = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _linkVanish = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _orbDescend = null!;
    private NewGameIntroDatabase.IntroSpriteFrame[] _orbVanish = null!;
    private Image _source = null!;
    private int _clock;
    private int _motionClock;
    private int _stageFrame;
    private bool _vanishing;
    private bool _linkVisible = true;

    public DialogueBox Dialogue { get; private set; } = null!;

    public override void _Ready()
    {
        var database = new NewGameIntroDatabase();
        _record = database.Record;
        _linkSpin = database.SpriteFrames("link-spin");
        _linkVanish = database.SpriteFrames("link-vanish");
        _orbDescend = database.SpriteFrames("orb-descend");
        _orbVanish = database.SpriteFrames("orb-vanish");
        _source = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_link.png").GetImage();
        Dialogue = new DialogueBox
        {
            Name = "IntroDialogue",
            ZIndex = 2,
            Visible = false
        };
        AddChild(Dialogue);
        QueueRedraw();
    }

    internal void ShowDialogue() =>
        Dialogue.ShowMessage(_record.Text, 0, _record.TextPosition);

    internal void SetAnimation(
        int clock,
        int motionClock,
        int stageFrame,
        bool vanishing,
        bool linkVisible)
    {
        _clock = clock;
        _motionClock = motionClock;
        _stageFrame = stageFrame;
        _vanishing = vanishing;
        _linkVisible = linkVisible;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 160, 144), Colors.Black);

        int z = LinkZForValidation(
            _motionClock,
            _record.InitialWaitFrames + _record.VoiceWaitFrames,
            _record.DescendOscillation,
            _record.HoverOscillation);
        int objectY = (_record.LinkY + 0x10 + z) & 0xff;
        int objectX = _record.LinkX & 0xff;

        // Both sparkle subids copy Link's complete XYZ position each update.
        // Subid $0d is replaced by subid $06 when Link begins animation $05.
        // Each one flickers on even wFrameCounter values.
        if ((_clock & 1) == 0)
        {
            if (_vanishing)
                DrawObjectFrame(AnimationFrame(_orbVanish, _stageFrame, 2), objectY, objectX);
            else if (_linkVisible)
                DrawObjectFrame(AnimationFrame(_orbDescend, _clock, 0), objectY, objectX);
        }

        if (_linkVisible)
        {
            NewGameIntroDatabase.IntroSpriteFrame frame = _vanishing
                ? AnimationFrame(_linkVanish, _stageFrame, -1)
                : AnimationFrame(_linkSpin, _clock, 0);
            DrawObjectFrame(frame, objectY, objectX);
        }

    }

    private void DrawObjectFrame(
        NewGameIntroDatabase.IntroSpriteFrame frame,
        int objectY,
        int objectX)
    {
        foreach (NewGameIntroDatabase.IntroOamPart part in frame.Parts)
        {
            int rawY = (objectY + part.Y) & 0xff;
            int rawX = (objectX + part.X) & 0xff;
            // These are the exact clipping comparisons in @drawObject.
            if (rawY >= 0xa0 || rawX >= 0xa8)
                continue;

            int palette = (frame.BasePalette ^ part.Flags) & 0x07;
            bool flipX = (part.Flags & 0x20) != 0;
            bool flipY = (part.Flags & 0x40) != 0;
            Texture2D cell = CellTexture(
                frame.SourceOffset, part.Tile, palette, flipX, flipY);
            // GBC OAM stores screen Y+16 and X+8.
            DrawTexture(cell, new Vector2(rawX - 8, rawY - 16));
        }
    }

    private Texture2D CellTexture(
        int sourceOffset,
        int tileOffset,
        int palette,
        bool flipX,
        bool flipY)
    {
        long key = (uint)sourceOffset | ((long)(uint)tileOffset << 16) |
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
                sourceOffset, tileOffset, sourcePixelX, sourcePixelY);
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
        // The disassembly's spr_link PNG is interleaved into 8x16 OBJ cells:
        // 32 source bytes per cell, with 16 cells across the generated sheet.
        // In GBC 8x16 OBJ mode the OAM tile byte selects an even tile, so each
        // increment of two advances to the next interleaved cell.
        int cell = sourceOffset / 32 + (tileOffset & 0xfe) / 2;
        return new Vector2I((cell % 16) * 8 + x, (cell / 16) * 16 + y);
    }

    private static NewGameIntroDatabase.IntroSpriteFrame AnimationFrame(
        NewGameIntroDatabase.IntroSpriteFrame[] frames,
        int clock,
        int loopStart)
    {
        int remaining = Math.Max(0, clock);
        int index = 0;
        while (remaining >= frames[index].Duration)
        {
            remaining -= frames[index].Duration;
            index++;
            if (index >= frames.Length)
            {
                if (loopStart < 0)
                    return frames[^1];
                index = loopStart;
            }
        }
        return frames[index];
    }

    internal static int LinkZForValidation(
        int clock,
        int descendFrames,
        int[] descendDeltas,
        int[] hoverDeltas)
    {
        int z = 0;
        for (int frame = 8; frame <= clock; frame += 8)
        {
            int[] deltas = frame <= descendFrames ? descendDeltas : hoverDeltas;
            z = (z + deltas[(frame & 0x38) >> 3]) & 0xff;
        }
        return z;
    }

    internal static int FirstVisibleLinkFrameForValidation(
        int rawY,
        int descendFrames,
        int[] descendDeltas,
        int[] hoverDeltas,
        int oamY = 0x08)
    {
        for (int frame = 0; frame < 0x1000; frame++)
        {
            int z = LinkZForValidation(
                frame, descendFrames, descendDeltas, hoverDeltas);
            if (((rawY + 0x10 + z + oamY) & 0xff) < 0xa0)
                return frame;
        }
        throw new InvalidOperationException("Link never entered the visible OAM range.");
    }

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
