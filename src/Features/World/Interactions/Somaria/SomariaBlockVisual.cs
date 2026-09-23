using Godot;
using System;

namespace oracleofages;

internal sealed class SomariaBlockVisual
{
    private readonly EnemyAnimationPlayer[] _animations = new EnemyAnimationPlayer[3];
    internal int Pose { get; private set; }
    private EnemyAnimationPlayer Current => _animations[Pose];
    internal int Parameter => Current.CurrentParameter;
    internal int Frame => Current.FrameIndex;
    internal Texture2D Texture => Current.CurrentTexture;
    internal Vector2 Offset => Current.CurrentOffset;

    internal SomariaBlockVisual(Node2D owner, SomariaGraphicsDatabase data)
    {
        for (int pose = 0; pose < _animations.Length; pose++)
        {
            SomariaGraphic graphic = data.Block(pose);
            var animation = new EnemyAnimationPlayer(owner, 1);
            animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{graphic.Sprite}.png"),
                [graphic.Animation], graphic.TileBase, graphic.OamFlags & 7, positionedOam: true);
            animation.SetAnimation(0);
            _animations[pose] = animation;
        }
    }

    internal void SetPose(int pose)
    {
        if (pose is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(pose));
        Pose = pose;
        Current.SetAnimation(0);
    }

    // itemCode18 advances only state1's animation. Parameter1 ends that
    // state on the same update, so the following stream is never executed.
    internal void AdvancePhase()
    {
        if (Pose != 0) throw new InvalidOperationException("Only Somaria phase-in calls itemAnimate.");
        if (Parameter == 0) Current.Advance();
    }
}
