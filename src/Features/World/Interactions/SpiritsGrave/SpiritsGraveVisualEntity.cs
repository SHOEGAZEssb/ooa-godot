using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract partial class SpiritsGraveVisualEntity : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;

    protected int AnimationIndex => _animation.AnimationIndex;
    protected int AnimationFrame => _animation.FrameIndex;
    protected int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;

    protected void InitializeVisual(
        VisualRecord visual,
        Vector2 position,
        int animation = 0,
        int? palette = null,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides = null)
    {
        Position = position;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            palette ?? visual.Palette,
            paletteOverrides: paletteOverrides,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        SetAnimation(animation);
    }

    protected void SetAnimation(int animation) => _animation.SetAnimation(animation);
    protected void AdvanceAnimation() => _animation.Advance();

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
            DrawTexture(_animation.CurrentTexture, new Vector2(-16, -16) + TransitionDrawOffset);
    }
}
