using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_SPARKLE $84:$00. Initialization is a setup-only update;
/// subsequent updates delete on parameter $ff before advancing animation.
/// </summary>
internal sealed partial class InteractionSparkleEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal int ElapsedUpdates { get; private set; }
    internal int SourceAngle { get; private set; }
    internal int Subid {get;private set;}
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _initialized ? _animation.CurrentParameter : 0;
    internal Vector2 RenderedTextureOrigin =>
        Position + _animation.CurrentOffset;
    internal Vector2 TextureSize => _animation.CurrentTexture.GetSize();
    internal ulong TexturePixelHash => OracleGraphicsCache.PixelHash(
        _animation.CurrentTexture.GetImage());

    internal void Initialize(
        Vector2 position,
        int sourceAngle,
        InteractionSparkleVisual visual,int subid=0)
    {
        Position = position;
        SourceAngle = sourceAngle;
        Subid=subid;
        if(subid is not(0 or 2 or 9))throw new System.NotSupportedException($"sparkle.s: INTERAC $84:${subid:x2} is not represented by the stationary native sparkle.");
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
            [visual.Animation],
            visual.TileBase,
            visual.Palette);
        _animation.SetAnimation(0);
        Visible = false;
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            QueueRedraw();
            return;
        }
        if (_animation.CurrentParameter == 0xff)
        {
            Finished = true;
            Visible = false;
            return;
        }
        _animation.Advance();
    }

    public override void _Draw()
    {
        if (Visible && !Finished)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }
}

internal readonly record struct InteractionSparkleVisual(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);

internal sealed record InteractionSparkleSpawn(
    Vector2 Position, int SourceAngle, InteractionSparkleVisual Visual,int Subid=0) : RoomEntitySpawn;
