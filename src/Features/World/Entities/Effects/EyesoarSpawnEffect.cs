using Godot;
using System;

namespace oracleofages;

/// <summary>INTERAC_0b:$02; the terminal animation byte is read by the linked enemy.</summary>
internal sealed partial class EyesoarSpawnEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private Action<int> _sound = null!;
    private bool _initialized;
    internal override bool Finished { get; private protected set; }
    internal int Parameter => _animation.CurrentParameter;
    internal void Initialize(Vector2 position, DungeonInteractionVisual visual, Action<int> sound)
    {
        Position = position.Floor(); _sound = sound; ZIndex = 10;
        _animation = new(this, 1);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations,
            visual.TileBase, visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted, positionedOam: true);
        _animation.SetAnimation(0);
    }
    internal override void UpdateFrame()
    {
        if (Finished) return;
        if (!_initialized) { _initialized = true; _sound(OracleSoundEngine.SndUnknown5); return; }
        if ((Parameter & 128) != 0) { Finished = true; Visible = false; return; }
        _animation.Advance();
    }
    public override void _Draw()
    { if (!Finished) DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + TransitionDrawOffset); }
}

internal sealed record EyesoarSpawnEffectSpawn(EyesoarSpawnEffect Effect) : RoomEntitySpawn;
