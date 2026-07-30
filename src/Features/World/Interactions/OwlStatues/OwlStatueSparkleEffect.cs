using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_SPARKLE ($84:$00), including its setup-only first update and
/// terminal `$ff animation parameter.
/// </summary>
internal sealed partial class OwlStatueSparkleEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;

    internal void Initialize(
        Vector2 position,
        OwlStatueSparkleRecord visual)
    {
        Position = position;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            source,
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
        if ((_animation.CurrentParameter & 0x80) != 0)
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
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}
