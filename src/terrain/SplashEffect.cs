using Godot;

namespace oracleofages;

public partial class SplashEffect : Node2D
{
    private const int WaterFrameDuration = 4;
    private const int LavaFrameDuration = 2;
    private Texture2D _texture = null!;
    private double _frames;
    private int _frameCount;
    private int _frameDuration;

    public bool IsLava { get; private set; }
    internal int AnimationFrame => Mathf.Min((int)(_frames / _frameDuration), _frameCount - 1);
    internal int DurationFrames => _frameCount * _frameDuration;

    public void Initialize(Vector2 position, OracleRoomData.HazardType hazard)
    {
        Position = position;
        IsLava = hazard == OracleRoomData.HazardType.Lava;
        _frameCount = IsLava ? LavaParts.Length : WaterParts.Length;
        _frameDuration = IsLava ? LavaFrameDuration : WaterFrameDuration;
        _texture = BuildTexture(IsLava);
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta)
    {
        _frames += delta * 60.0;
        if (_frames >= DurationFrames)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTextureRectRegion(
            _texture,
            new Rect2(-16, -16, 32, 32),
            new Rect2(AnimationFrame * 32, 0, 32, 32));
    }

    private static Texture2D BuildTexture(bool lava)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        SplashPart[][] frames = lava ? LavaParts : WaterParts;
        int sourceCell = lava ? 0x26 / 2 : 0x04 / 2;
        int sourceCellX = (sourceCell % 16) * 8;
        int sourceCellY = (sourceCell / 16) * 16;
        int basePalette = lava ? 2 : 1;
        Image output = Image.CreateEmpty(frames.Length * 32, 32, false, Image.Format.Rgba8);

        for (int frame = 0; frame < frames.Length; frame++)
        foreach (SplashPart part in frames[frame])
        {
            int palette = basePalette ^ part.PaletteXor;
            int destinationX = frame * 32 + part.X + 8;
            int destinationY = part.Y;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceCellX + (part.FlipX ? 7 - x : x);
                Color pixel = RecolorSpritePixel(source.GetPixel(readX, sourceCellY + y), palette);
                if (pixel.A > 0.0f)
                    output.SetPixel(destinationX + x, destinationY + y, pixel);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Color RecolorSpritePixel(Color source, int palette)
    {
        float value = source.R;
        if (value < 0.1f)
            return Colors.Transparent;
        int color = value < 0.5f ? 1 : value < 0.9f ? 2 : 3;
        Vector3I rgb = StandardSpritePalettes[palette, color];
        return new Color(rgb.X / 31.0f, rgb.Y / 31.0f, rgb.Z / 31.0f);
    }

    private readonly record struct SplashPart(
        int Y,
        int X,
        int PaletteXor = 0,
        bool FlipX = false);

    // INTERAC_SPLASH ($03): interactionAnimation5a0bc and OAM records
    // interactionOamData5013f/$50148/$50151. Tile base $04, palette $01.
    private static readonly SplashPart[][] WaterParts =
    {
        new[] { new SplashPart(10, 0), new SplashPart(10, 8, FlipX: true) },
        new[] { new SplashPart(8, -2), new SplashPart(8, 10, FlipX: true) },
        new[] { new SplashPart(6, -4), new SplashPart(6, 12, FlipX: true) }
    };

    // INTERAC_LAVASPLASH ($04): interactionAnimation5a0d1 and OAM records
    // $5015a-$501ab. Attribute palette bits are XORed with base palette $02.
    private static readonly SplashPart[][] LavaParts =
    {
        new[] { new SplashPart(6, 3), new SplashPart(6, 5) },
        new[] { new SplashPart(5, 2, 1), new SplashPart(5, 6, 1) },
        new[] { new SplashPart(4, 1, 7), new SplashPart(4, 7, 7) },
        new[] { new SplashPart(3, 0), new SplashPart(3, 8) },
        new[] { new SplashPart(2, -1, 1), new SplashPart(2, 9, 1) },
        new[] { new SplashPart(3, -2, 7), new SplashPart(3, 10, 7) },
        new[] { new SplashPart(4, -3), new SplashPart(4, 11) },
        new[] { new SplashPart(5, -4, 1), new SplashPart(5, 12, 1) },
        new[] { new SplashPart(6, -5, 7), new SplashPart(6, 13, 7) },
        new[] { new SplashPart(8, -6), new SplashPart(8, 14) }
    };

    // standardSpritePaletteData palettes $00-$05 from Ages paletteData.s.
    private static readonly Vector3I[,] StandardSpritePalettes =
    {
        { new(31, 31, 31), new(0, 0, 0), new(2, 21, 8), new(31, 26, 17) },
        { new(31, 31, 31), new(0, 0, 0), new(3, 16, 31), new(31, 26, 17) },
        { new(31, 31, 31), new(0, 0, 0), new(31, 1, 5), new(31, 26, 17) },
        { new(31, 31, 31), new(0, 0, 0), new(31, 15, 1), new(31, 26, 17) },
        { new(31, 31, 31), new(14, 21, 31), new(0, 0, 31), new(0, 0, 0) },
        { new(31, 31, 31), new(31, 22, 6), new(27, 0, 0), new(0, 0, 0) }
    };
}
