using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_CLINK ($07), spawned where a sword's tile probe meets a solid wall.
/// </summary>
public partial class ClinkEffect : Node2D
{
    private const int FrameDuration = 4;
    private const int FrameCount = 2;
    private static Texture2D? _sharedTexture;
    private Texture2D _texture = null!;
    private double _frames;

    internal bool Flickers { get; private set; }
    internal bool Finished { get; private set; }
    internal int AnimationFrame => Mathf.Min((int)(_frames / FrameDuration), FrameCount - 1);
    internal int ElapsedFrames => Mathf.Min((int)_frames, DurationFrames);
    internal int DurationFrames => FrameDuration * FrameCount;
    internal bool EffectVisible => !Flickers || (ElapsedFrames & 1) == 0;

    internal void Initialize(Vector2 position, bool flickers)
    {
        Position = position;
        Flickers = flickers;
        _texture = _sharedTexture ??= BuildTexture();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void AdvanceForValidation(double delta) => Advance(delta);
    internal void AdvanceFrameForEntityManager() => Advance(1.0 / 60.0, false);

    private void Advance(double delta, bool queueFree = true)
    {
        if (Finished)
            return;
        _frames += delta * 60.0;
        if (_frames >= DurationFrames)
        {
            Finished = true;
            Visible = false;
            if (queueFree)
                QueueFree();
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
            new Rect2(-8, -8, 16, 16),
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
