using Godot;

namespace oracleofages;

public partial class NpcCharacter : Node2D
{
    private enum Facing { Up, Right, Down, Left }

    private Texture2D _texture = null!;
    private int _frameBase;
    private Facing _facing = Facing.Down;

    public NpcDatabase.NpcRecord Record { get; private set; }
    public string Message => Record.Message;
    public int TextId => Record.TextId;
    public const float CollisionRadius = 6.0f;
    public const float LinkCollisionRadius = 6.0f;
    public const float LinkBlockingRadius = CollisionRadius + LinkCollisionRadius;
    public Rect2 SpriteBounds => new(Position + new Vector2(-8, -8), new Vector2(16, 16));
    public int CurrentFrameColumn => GetFrameColumn();

    public Rect2 BodyBounds => ObjectCollisionBounds;
    public Rect2 ObjectCollisionBounds => new(
        Position - new Vector2(CollisionRadius, CollisionRadius),
        new Vector2(CollisionRadius * 2.0f, CollisionRadius * 2.0f));
    public Rect2 LinkBlockingBounds => new(
        Position - new Vector2(LinkBlockingRadius, LinkBlockingRadius),
        new Vector2(LinkBlockingRadius * 2.0f, LinkBlockingRadius * 2.0f));
    public Rect2 InteractionBounds => SpriteBounds.Grow(8);

    public void Initialize(NpcDatabase.NpcRecord record)
    {
        Record = record;
        _frameBase = record.FrameBase;
        byte[] bytes = FileAccess.GetFileAsBytes($"res://assets/oracle/gfx/{record.SpriteName}.png");
        Image image = new();
        image.LoadPngFromBuffer(bytes);
        _texture = BuildNpcTexture(image);
        Position = new Vector2(record.X, record.Y);
        QueueRedraw();
    }

    public bool BlocksLinkCenter(Vector2 linkCenter)
    {
        Vector2 delta = linkCenter - Position;
        return Mathf.Abs(delta.X) < LinkBlockingRadius &&
            Mathf.Abs(delta.Y) < LinkBlockingRadius;
    }

    public bool CanTalkTo(Player player)
    {
        Vector2 talkPoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        return InteractionBounds.HasPoint(talkPoint);
    }

    public void FaceToward(Vector2 target)
    {
        Vector2 delta = target - Position;
        if (Mathf.Abs(delta.X) > Mathf.Abs(delta.Y))
            _facing = delta.X > 0 ? Facing.Right : Facing.Left;
        else
            _facing = delta.Y > 0 ? Facing.Down : Facing.Up;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTextureRectRegion(
            _texture,
            new Rect2(-8, -8, 16, 16),
            new Rect2(GetFrameColumn() * 16, 0, 16, 16));
    }

    private int GetFrameColumn()
    {
        return _frameBase + _facing switch
        {
            Facing.Up => 1,
            Facing.Right => 3,
            Facing.Left => 2,
            _ => 0
        };
    }

    private static Texture2D BuildNpcTexture(Image source)
    {
        Image output = Image.CreateEmpty(source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);
        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
            output.SetPixel(x, y, RecolorStandardNpcPixel(source.GetPixel(x, y)));

        return ImageTexture.CreateFromImage(output);
    }

    private static Color RecolorStandardNpcPixel(Color source)
    {
        // INTERAC_MALE_VILLAGER subid $03 resolves through interaction3aSubidData:
        //   gfx $4f (spr_syrup_teenager), oamTileIndexBase $10, palette/default-animation $12.
        // The palette nibble selects standard sprite palette slot 1:
        //   transparent, black outline, blue clothing, skin/light shade.
        float value = source.R;
        return value < 0.1f ? Colors.Transparent
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? GbcColor(0x03, 0x10, 0x1f)
            : GbcColor(0x1f, 0x1a, 0x11);
    }

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }
}
