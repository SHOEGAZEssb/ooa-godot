using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_CLINK ($07), spawned for sword contact with a solid tile or an
/// armored collision target.
/// </summary>
public partial class ClinkEffect : Node2D
{
    private const int FrameDuration = 4;
    private const int FrameCount = 2;
    private static Texture2D? _sharedTexture;
    private Texture2D _texture = null!;
    private int _frames;
    private Func<int>? _nativeFrame;
    private int _nativeSlot;
    private bool _nativeVisible = true;

    internal bool Flickers { get; private set; }
    internal bool Finished { get; private set; }
    internal int AnimationFrame => Math.Min(Math.Max(0, _frames - 1) / FrameDuration, FrameCount - 1);
    internal int ElapsedFrames => Math.Min(_frames, DurationFrames);
    internal int DurationFrames => FrameDuration * FrameCount + 2;
    internal bool EffectVisible => _nativeVisible;
    internal int ZHigh { get; private set; }
    internal int AnimationParameter => _frames >= DurationFrames - 1 ? 0xff : 0;

    internal void BindNativeTiming(int slot, Func<int> frame)
    {
        _nativeSlot = slot;
        _nativeFrame = frame;
        Visible = false;
    }

    internal void Initialize(Vector2 position, bool flickers, int zHigh = 0)
    {
        Position = position;
        Flickers = flickers;
        ZHigh = unchecked((sbyte)(byte)zHigh);
        _texture = _sharedTexture ??= BuildTexture();
        QueueRedraw();
    }

    internal void AdvanceFrameForEntityManager()
    {
        if (Finished)
            return;
        _frames++;
        // breakTileDebris state0 shows the initial frame without ticking
        // animation. State1 sees terminal $ff on the update after eight
        // animation updates (interactionAnimation5a0c8).
        int frame = (_nativeFrame ?? throw new InvalidOperationException("INTERAC$07 requires native interaction timing."))();
        _nativeVisible = _frames <= 1 || !Flickers || ((frame ^ _nativeSlot) & 1) == 0;
        Visible = _nativeVisible;
        if (_frames >= DurationFrames)
        {
            Finished = true;
            Visible = false;
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!EffectVisible)
            return;
        DrawTextureRectRegion(
            _texture,
            new Rect2(-8, -8 + ZHigh, 16, 16),
            new Rect2(AnimationFrame * 16, 0, 16, 16));
    }

    private static Texture2D BuildTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        Image output = Image.CreateEmpty(FrameCount * 16, 16, false, Image.Format.Rgba8);

        // INTERAC_CLINK uses tile base $10 and palette $01. OAM $50205 uses
        // tile $00 and OAM $502b9 uses tile $02; each mirrors its right half.
        for (int frame = 0; frame < FrameCount; frame++)
        {
            int sourceCell = (0x10 + frame * 2) / 2;
            int sourceCellX = (sourceCell % 16) * 8;
            int sourceCellY = (sourceCell / 16) * 16;
            for (int half = 0; half < 2; half++)
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceCellX + (half == 0 ? x : 7 - x);
                Color pixel = RecolorPalette1(source.GetPixel(readX, sourceCellY + y));
                if (pixel.A > 0.0f)
                    output.SetPixel(frame * 16 + half * 8 + x, y, pixel);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Color RecolorPalette1(Color source)
    {
        float value = source.R;
        return value < 0.1f ? Colors.Transparent
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? new Color(3 / 31.0f, 16 / 31.0f, 1.0f)
            : new Color(1.0f, 26 / 31.0f, 17 / 31.0f);
    }
}
