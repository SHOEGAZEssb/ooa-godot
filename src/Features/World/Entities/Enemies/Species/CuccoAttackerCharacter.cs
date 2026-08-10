using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_CUCCO_ATTACKER $22. A revenge Cucco consumes one shared RNG value,
/// enters from one of four source screen edges, waits 24 updates before
/// enabling its out-of-bounds deletion check, and flies toward Link.
/// </summary>
internal partial class CuccoAttackerCharacter : EnemyCharacter
{
    private readonly CuccoAttackerBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.CuccoAttacker;
    private OracleRandom _random = null!;
    private CuccoAttackerState _state;
    private int _hitCount;
    private int _counter;
    private int _angle;
    private int _speed;
    private Vector2 _precisePosition;

    internal CuccoAttackerState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _state != CuccoAttackerState.Uninitialized;
    protected override Vector2 AnimationDrawOffset =>
        Animation.CurrentOffset + Vector2.Down * _behavior.Z;

    internal void Initialize(
        ImportedEnemyDefinition cuccoRecord,
        OracleRandom random,
        int hitCount)
    {
        if (cuccoRecord.Id != 0x36 || cuccoRecord.Animations.Length != 2)
        {
            throw new InvalidOperationException(
                "PART_CUCCO_ATTACKER requires the shared ENEMY_CUCCO visual.");
        }
        if (hitCount < 0x10 || hitCount > 0x20)
            throw new ArgumentOutOfRangeException(nameof(hitCount));

        _random = random;
        _hitCount = hitCount;
        _state = CuccoAttackerState.Uninitialized;
        _counter = 0;
        _angle = 0;
        _speed = 0;
        _precisePosition = Vector2.Zero;
        string sourceDuration = "8@";
        string targetDuration = $"{_behavior.AnimationFrameDuration}@";
        string[] animations =
        [
            cuccoRecord.Animations[0].Replace(
                sourceDuration, targetDuration, StringComparison.Ordinal),
            cuccoRecord.Animations[1].Replace(
                sourceDuration, targetDuration, StringComparison.Ordinal)
        ];
        InitializeEnemy(
            Vector2.Zero,
            new EnemyCharacterConfiguration(
                0x40,
                cuccoRecord.RadiusX,
                cuccoRecord.RadiusY,
                EnemyVisualSource.LoadComposite(cuccoRecord.Sprites),
                animations,
                cuccoRecord.TileBase,
                cuccoRecord.Palette,
                DamagePalette: 5,
                SourceGrayscaleInverted:
                    cuccoRecord.SourceGrayscaleInverted));
        Visible = false;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;

        switch (_state)
        {
            case CuccoAttackerState.Uninitialized:
                InitializeFlight(linkPosition);
                return;

            case CuccoAttackerState.Entering:
                _counter--;
                if (_counter == 0)
                    _state = CuccoAttackerState.Flying;
                ApplySpeedAndAnimate();
                return;

            case CuccoAttackerState.Flying:
                if (!WithinScreenBoundary())
                {
                    Finish();
                    return;
                }
                ApplySpeedAndAnimate();
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Cucco attacker state {_state}.");
        }
    }

    private void InitializeFlight(Vector2 linkPosition)
    {
        _state = CuccoAttackerState.Entering;
        _counter = _behavior.EntryDelay;
        int speedIndex = ((_hitCount - 0x10) & 0x1e) >> 1;
        _speed = _behavior.Speeds[speedIndex].Value;

        int random = _random.Next().Value;
        int edge = (random & _behavior.EdgeMask) >> 4;
        int axisOffset = (random & _behavior.AxisTableMask) == 0 ? 0 : 16;
        int varyingPosition = _behavior.EdgeAxisValues[
            axisOffset + (random & _behavior.AxisIndexMask)].Value;
        int edgePosition = _behavior.ScreenEdgePositions[edge].Value;
        _precisePosition = (edge & 1) == 0
            ? new Vector2(edgePosition, varyingPosition)
            : new Vector2(varyingPosition, edgePosition);
        Position = _precisePosition;
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, linkPosition);
        SetAnimation(
            _angle < _behavior.LeftAnimationAngleThreshold ? 1 : 0);
        Visible = true;
        QueueRedraw();
    }

    private void ApplySpeedAndAnimate()
    {
        OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _speed, _angle);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        AdvanceAnimation();
        QueueRedraw();
    }

    private bool WithinScreenBoundary()
    {
        int x = Mathf.FloorToInt(_precisePosition.X);
        int y = Mathf.FloorToInt(_precisePosition.Y);
        return x >= 0 && x < 0xa0 && y >= 0 && y < 0x80;
    }
}

internal enum CuccoAttackerState
{
    Uninitialized,
    Entering,
    Flying
}
