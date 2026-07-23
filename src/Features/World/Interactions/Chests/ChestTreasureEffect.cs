using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_TREASURE $60 spawn mode $03. The displayed object comes from the
/// treasure record's graphic byte and the interaction graphics/OAM tables,
/// which are distinct from the inventory-button display tables.
/// </summary>
public partial class ChestTreasureEffect : Node2D
{
    public const int RiseFrames = 32;

    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private Vector2 _start;
    private double _frames;

    public bool Finished => _frames >= RiseFrames;
    internal int VisualGraphic { get; private set; }
    internal Texture2D RewardTexture => _texture;

    public void Initialize(
        Vector2 position,
        TreasureObjectVisualRecord visual)
    {
        _start = position;
        Position = position;
        VisualGraphic = visual.Graphic;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation);
        if (definition.Frames.Length == 0)
        {
            throw new System.InvalidOperationException(
                $"Treasure graphic ${visual.Graphic:x2} has no animation frames.");
        }
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            visual.TileBase,
            visual.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
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
        DrawTexture(_texture, _textureOffset);
    }
}
