using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract partial class DungeonInteractionVisualEntity : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;

    protected int AnimationIndex => _animation.AnimationIndex;
    protected int AnimationFrame => _animation.FrameIndex;
    protected int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    internal Vector2 CurrentDrawOffset => _animation.CurrentOffset;

    protected void InitializeVisual(
        DungeonInteractionVisual visual,
        Vector2 position,
        int animation = 0,
        int? palette = null,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides = null,
        bool positionedOam = false)
    {
        Position = position;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            palette ?? visual.Palette,
            paletteOverrides: paletteOverrides,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted,
            positionedOam: positionedOam);
        SetAnimation(animation);
    }

    protected void SetAnimation(int animation) => _animation.SetAnimation(animation);
    protected void AdvanceAnimation() => _animation.Advance();
    protected void SetNativePalette(IReadOnlyDictionary<int, Color[]>? palettes) => _animation.SetPaletteOverride(palettes);
    protected void AddAnimationCounter(int value) => _animation.SetFrameCounter((_animation.RemainingUpdates + value) & 255);

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }
}
