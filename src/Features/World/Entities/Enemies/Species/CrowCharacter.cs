using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_CROW ($41), subid $00. The perched Crow turns toward Link, rises for
/// 25 updates when approached, then charges with the original 32-step angle,
/// SPEED_140 movement, random four-step aim offset, and eight-update steering.
/// </summary>
public partial class CrowCharacter : TransitionOffsetNode2D
{

    private const int ApproachRadiusY = 0x30;
    private const int ApproachRadiusX = 0x18;
    private const int RisingFrames = 25;
    private const int ChargeCounter = 90;
    private const int AirborneZ = -6;
    private const int ScreenBottom = 0x88;
    private const int ScreenRight = 0xa8;

    private EnemyAnimationPlayer _animation = null!;
    private OracleRandom _random = null!;
    private Vector2 _precisePosition;
    private CrowState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _z;
    private int _health;
    private bool _collisionEnabled;

    public CrowRecord Record { get; private set; }
    public bool IsDead { get; private set; }
    internal bool DeletedOutOfBounds { get; private set; }
    public Rect2 CollisionBounds => _collisionEnabled && !IsDead
        ? new Rect2(
            Position - new Vector2(Record.CollisionRadiusX, Record.CollisionRadiusY),
            new Vector2(Record.CollisionRadiusX * 2, Record.CollisionRadiusY * 2))
        : new Rect2(Position, Vector2.Zero);
    internal CrowState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int Z => _z;
    internal bool CollisionEnabled => _collisionEnabled;
    internal int CurrentAnimation => _animation.AnimationIndex;
    internal int CurrentAnimationFrame => _animation.FrameIndex;
    internal Vector2 PrecisePosition => _precisePosition;

    internal void Initialize(
        CrowRecord record,
        Vector2 position,
        OracleRandom random)
    {
        if (record.SubId != 0)
            throw new InvalidOperationException(
                $"CrowCharacter only supports ENEMY_CROW subid $00, got ${record.SubId:x2}.");

        Record = record;
        _random = random;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        _health = record.Health;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        _animation = new EnemyAnimationPlayer(this, 4);
        _animation.Load(
            source,
            [
                record.PerchedRightAnimation,
                record.PerchedLeftAnimation,
                record.FlightRightAnimation,
                record.FlightLeftAnimation
            ],
            record.TileBase,
            record.Palette);
        _animation.SetAnimation(0);
        QueueRedraw();
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;

        switch (_state)
        {
            case CrowState.Perched:
                FaceTarget(linkPosition, flight: false);
                if (!WithinUnsignedApproachRange(linkPosition))
                    return;
                _state = CrowState.Rising;
                _counter1 = RisingFrames;
                return;

            case CrowState.Rising:
                FaceTarget(linkPosition, flight: true);
                _counter1--;
                if (_counter1 == 0)
                {
                    BeginCharge(linkPosition);
                }
                else if ((_counter1 & 0x03) == 0)
                {
                    _z--;
                }
                _animation.Advance();
                return;

            case CrowState.Charging:
                if (!WithinChargeScreenBounds())
                {
                    DeletedOutOfBounds = true;
                    IsDead = true;
                    Visible = false;
                    return;
                }

                if (_counter2 > 0)
                {
                    _counter2--;
                    if (_counter2 != 0 && (_counter2 & 0x07) == 0)
                    {
                        int target = OracleObjectMath.AngleToward(Position, linkPosition);
                        NudgeAngleToward(target);
                        SetDirectionalAnimation(flight: true);
                    }
                }
                _precisePosition += OracleObjectMath.VectorFromAngle32(_angle) *
                    (Record.SpeedRaw / 40.0f);
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                _animation.Advance();
                return;
        }
    }

    public bool OverlapsLink(Vector2 linkPosition) =>
        _collisionEnabled && !IsDead &&
        Mathf.Abs(linkPosition.X - Position.X) < Record.CollisionRadiusX + 6 &&
        Mathf.Abs(linkPosition.Y - Position.Y) < Record.CollisionRadiusY + 6;

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || !_collisionEnabled)
            return false;
        _health = Math.Max(0, _health - Math.Max(1, damage));
        if (_health > 0)
            return true;
        IsDead = true;
        Visible = false;
        return true;
    }

    public override void _Draw()
    {
        if (!IsDead && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16 + _z) + TransitionDrawOffset);
        }
    }

    private bool WithinUnsignedApproachRange(Vector2 target)
    {
        int crowY = (int)Position.Y & 0xff;
        int crowX = (int)Position.X & 0xff;
        int targetY = (int)target.Y & 0xff;
        int targetX = (int)target.X & 0xff;
        return ((targetY - crowY + ApproachRadiusY) & 0xff) < 0x61 &&
            ((targetX - crowX + ApproachRadiusX) & 0xff) < 0x31;
    }

    private void BeginCharge(Vector2 linkPosition)
    {
        _state = CrowState.Charging;
        _counter2 = ChargeCounter;
        _collisionEnabled = true;
        _angle = OracleObjectMath.AngleToward(Position, linkPosition);
        int offset = (_random.Next().Value & 0x04) == 0 ? -4 : 4;
        _angle = (_angle + offset) & 0x1f;
        SetDirectionalAnimation(flight: true);
    }

    private void FaceTarget(Vector2 target, bool flight)
    {
        _angle = OracleObjectMath.AngleToward(Position, target);
        SetDirectionalAnimation(flight);
    }

    private void SetDirectionalAnimation(bool flight)
    {
        // crow_setAnimationFromAngle deliberately preserves the current
        // animation when the target is exactly above or below the Crow.
        if ((_angle & 0x0f) == 0)
            return;
        int animation = (flight ? 2 : 0) + ((_angle & 0x10) == 0 ? 1 : 0);
        if (_animation.AnimationIndex != animation)
            _animation.SetAnimation(animation);
    }

    private void NudgeAngleToward(int target)
    {
        int difference = (_angle - target) & 0x1f;
        if (difference == 0)
            return;
        _angle = difference < 0x10
            ? (_angle - 1) & 0x1f
            : (_angle + 1) & 0x1f;
    }

    private bool WithinChargeScreenBounds()
    {
        int y = (int)Mathf.Floor(_precisePosition.Y);
        int x = (int)Mathf.Floor(_precisePosition.X);
        return y is >= 0 and < ScreenBottom && x is >= 0 and < ScreenRight;
    }
}

internal enum CrowState
{
    Perched,
    Rising,
    Charging
}
