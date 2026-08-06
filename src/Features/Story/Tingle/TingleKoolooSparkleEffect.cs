using Godot;

namespace oracleofages;

/// <summary>
/// The three INTERAC_SPARKLE $84:$00 children created by Tingle's
/// kooloo-limpah animation cue. Their source angle $10 selects
/// objectSetVisible81; the sparkles themselves remain stationary.
/// </summary>
internal sealed partial class TingleKoolooSparkleEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal int ElapsedUpdates { get; private set; }
    internal int SourceAngle { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal Vector2 RenderedTextureOrigin =>
        Position + _animation.CurrentOffset;
    internal Vector2 TextureSize => _animation.CurrentTexture.GetSize();
    internal ulong TexturePixelHash => OracleGraphicsCache.PixelHash(
        _animation.CurrentTexture.GetImage());

    internal void Initialize(
        Vector2 position,
        int sourceAngle,
        TingleKoolooSparkleVisual visual)
    {
        Position = position;
        SourceAngle = sourceAngle;
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

internal readonly record struct TingleKoolooSparkleVisual(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);

internal sealed record TingleKoolooSparkleSpawn(
    Vector2 Position,
    int SourceAngle,
    TingleKoolooSparkleVisual Visual)
    : RoomEntitySpawn(UpdateThisFrame: true);
