using Godot;

namespace oracleofages;

public partial class ChestTreasureEffect : Node2D
{
    public const int RiseFrames = 32;

    private Texture2D _texture = null!;
    private Vector2 _start;
    private double _frames;

    public bool Finished => _frames >= RiseFrames;

    public void Initialize(Vector2 position)
    {
        _start = position;
        Position = position;
        _texture = BuildRupeeTexture();
        QueueRedraw();
    }

    public void Advance(double delta)
    {
        _frames = Mathf.Min((float)(_frames + delta * 60.0), RiseFrames);
        // SPEED_40 is one quarter-pixel per frame. Rendering uses the integer
        // object coordinate, so the sprite rises one pixel every four frames.
        Position = _start + new Vector2(0, -Mathf.Floor((float)_frames / 4.0f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_texture, new Vector2(-4, -8));
    }

    private static Texture2D BuildRupeeTexture()
    {
        byte[] bytes = FileAccess.GetFileAsBytes("res://assets/oracle/gfx/spr_common_items.png");
        Image source = new();
        source.LoadPngFromBuffer(bytes);
        Image output = Image.CreateEmpty(8, 16, false, Image.Format.Rgba8);

        // TREASURE_OBJECT_RUPEES uses interaction $60 graphic $2b. Its
        // interaction data selects tile base $06 in spr_common_items.
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(24 + x, y);
            if (pixel.A < 0.1f || pixel.R < 0.1f)
                continue;
            output.SetPixel(x, y, pixel.R < 0.5f
                ? GbcColor(0x0e, 0x15, 0x1f)
                : pixel.R < 0.9f ? GbcColor(0x00, 0x00, 0x1f) : Colors.Black);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Color GbcColor(int red, int green, int blue) =>
        new(red / 31.0f, green / 31.0f, blue / 31.0f);
}
