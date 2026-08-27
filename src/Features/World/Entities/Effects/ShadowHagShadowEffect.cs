using Godot;
using System;

namespace oracleofages;

/// <summary>PART_SHADOW_HAG_SHADOW $41.</summary>
internal sealed partial class ShadowHagShadowEffect : FixedEffectNode2D
{
    private static readonly int[] InitialAngles = [0x04, 0x0c, 0x14, 0x1c];
    private ShadowHagBoss _owner = null!;
    private EnemyAnimationPlayer _animation = null!;
    private ShadowHagShadowState _state;
    private int _counter1;
    private int _angleIndex;
    private int _angle;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal ShadowHagShadowState State => _state;
    internal int Counter1 => _counter1;
    internal int Angle => _angle;

    internal void Initialize(
        ShadowHagBoss owner,
        int angleIndex,
        DungeonInteractionVisual visual)
    {
        if (angleIndex is < 0 or > 3 ||
            visual.Key != "shadow-hag-shadow" ||
            visual.Animations.Length != 5)
        {
            throw new InvalidOperationException(
                "PART_SHADOW_HAG_SHADOW requires angle $00-$03 and its " +
                "five imported animations.");
        }
        _owner = owner;
        _angleIndex = angleIndex;
        Position = owner.Position;
        ZIndex = 10;
        Visible = false;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        _animation.SetAnimation(1);
        QueueRedraw();
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;
        if (_owner.Defeated)
        {
            Finish();
            return;
        }
        if (!_initialized)
        {
            _initialized = true;
            _state = ShadowHagShadowState.Chasing;
            _counter1 = 8;
            _angle = InitialAngles[_angleIndex];
            Visible = true;
            QueueRedraw();
            return;
        }

        switch (_state)
        {
            case ShadowHagShadowState.Chasing:
                if (_owner.ShadowsConverging)
                    _state = ShadowHagShadowState.Converging;
                if (_counter1 != 0)
                    _counter1--;
                if (_counter1 == 0)
                {
                    _counter1 = 8;
                    int target = OracleObjectMovement.Shared.RelativeAngle(
                        Position, _owner.LinkPosition);
                    _angle = NudgeAngle(_angle, target);
                }
                Move();
                break;

            case ShadowHagShadowState.Converging:
                Vector2 targetPosition = _owner.Position;
                Vector2 pixels = OracleObjectMath.ToPixelPosition(Position);
                Vector2 targetPixels =
                    OracleObjectMath.ToPixelPosition(targetPosition);
                if (Mathf.Abs(pixels.X - targetPixels.X) <= 4 &&
                    Mathf.Abs(pixels.Y - targetPixels.Y) <= 4)
                {
                    _owner.ShadowReturned();
                    _state = ShadowHagShadowState.DeletePending;
                }
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, targetPosition);
                Move();
                break;

            case ShadowHagShadowState.DeletePending:
                Finish();
                break;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished && Visible)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + TransitionDrawOffset);
        }
    }

    private void Move()
    {
        // objectApplySpeed retains an unsigned wrapping 8.8 position. A
        // shadow can circle far enough during a later cycle to cross $00;
        // keeping the host-space negative coordinate makes its high byte
        // equivalent to the boss while the convergence distance stays 256
        // pixels away forever.
        Vector2 precisePosition = Position;
        OracleObjectMovement.Shared.ApplySpeed(
            ref precisePosition, 0x28, _angle);
        Position = precisePosition;
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
    }

    private static int NudgeAngle(int current, int target)
    {
        int clockwise = (target - current) & 0x1f;
        return clockwise == 0 ? current
            : (current + (clockwise < 0x10 ? 1 : -1)) & 0x1f;
    }
}

internal enum ShadowHagShadowState
{
    Chasing,
    Converging,
    DeletePending
}
