using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_SUBTERROR_DIRT $32.</summary>
internal sealed partial class SubterrorDirtEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private Action<int> _playSound = null!;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal Texture2D CurrentAnimationTexture => _animation.CurrentTexture;

    internal void Initialize(
        Vector2 position,
        DungeonInteractionVisual visual,
        IReadOnlyDictionary<int, Color[]> paletteOverrides,
        Action<int> playSound)
    {
        if (visual.Key != "subterror-dirt" || visual.Animations.Length != 1)
        {
            throw new InvalidOperationException(
                "PART_SUBTERROR_DIRT requires its imported one-animation visual.");
        }
        Position = position;
        _playSound = playSound;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            paletteOverrides: paletteOverrides,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
        QueueRedraw();
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(OracleSoundEngine.SndDig);
        }
        _animation.Advance();
        if (_animation.CurrentParameter == 0)
        {
            Finished = true;
            Visible = false;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }
}
