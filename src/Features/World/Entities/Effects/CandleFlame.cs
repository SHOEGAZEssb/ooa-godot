using Godot;
using System;

namespace oracleofages;

internal sealed partial class CandleFlame : TransitionOffsetNode2D
{
    private readonly CandleBehaviorProfile _data = EnemyBehaviorTables.Shared.Candle;
    private readonly Func<CandleCharacter?> _parent;
    private readonly EnemyAnimationPlayer _animation;
    internal int State { get; private set; }
    internal bool Finished { get; private set; }
    internal bool PendingCollision { get; private set; }
    internal int ZHigh => _data.FlameZ;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal int AnimationFrame => _animation.FrameIndex;
    internal Rect2 CollisionBounds => new(Position - Vector2.One * (_data.FlameData[2].Value & 15),
        Vector2.One * ((_data.FlameData[2].Value & 15) * 2));
    internal CandleFlame(EnemyProjectileVisualRecord visual, Func<CandleCharacter?> parent)
    {
        _parent = parent;
        _animation = new(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase,
            visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        Visible = false;
    }
    internal void PublishCollision() => PendingCollision = true;
    internal void UpdateFrame()
    {
        PendingCollision = false;
        var parent = _parent();
        if (parent is null || parent.CollisionMode == _data.ExplosionMode)
        { Finished = true; Visible = false; return; }
        if (State == 0) { State = 1; Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; _animation.SetAnimation(0); }
        if (State == 1 && parent.Speed != _data.BurnSpeed) { State = 2; _animation.SetAnimation(1); }
        Position = parent.Position.Floor();
        _animation.Advance(); QueueRedraw();
    }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTexture,
            _animation.CurrentOffset + Vector2.Down * ZHigh + TransitionDrawOffset);
    }
}
