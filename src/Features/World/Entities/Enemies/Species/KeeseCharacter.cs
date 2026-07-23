using Godot;
using System;

namespace oracleofages;

public partial class KeeseCharacter : EnemyCharacter
{

    private const float SpeedC0 = 0.75f;
    private const float Speed100 = 1.0f;
    private const int InitialRestFrames = 0x20;
    private const int ApproachDistance = 0x31;
    private const int TurningInterval = 12;
    private const int TurningIntervals = 12;

    private static readonly float[] DecelerationSpeeds =
    {
        0.75f, 0.5f, 0.25f, 0.25f, 0.125f, 0.125f, 0.125f, 0.125f
    };
    private static readonly int[] DecelerationAnimationMasks =
    {
        0x00, 0x00, 0x01, 0x01, 0x03, 0x03, 0x07, 0x00
    };
    private static readonly Vector2I[,] SideviewBoundaryOffsets =
    {
        { new(-5, -4), new(9, 0), new(-4, 4), new(0, 0) },
        { new(-5, -4), new(9, 0), new(2, 3), new(0, 6) },
        { new(0, 0), new(0, 0), new(6, -1), new(0, 6) },
        { new(-5, 7), new(9, 0), new(2, -8), new(0, 6) },
        { new(-5, 7), new(9, 0), new(-4, -7), new(0, 0) },
        { new(-5, 7), new(9, 0), new(-11, -8), new(0, 6) },
        { new(0, 0), new(0, 0), new(-7, -1), new(0, 6) },
        { new(-5, -4), new(9, 0), new(-11, 3), new(0, 6) }
    };
    private static readonly int[] BoundaryBounceAngles =
    {
        0x10, 0x0f, 0x0e, 0x0d, 0x0c, 0x0b, 0x0a, 0x09,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        0x00, 0x1f, 0x1e, 0x1d, 0x1c, 0x1b, 0x1a, 0x19,
        0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
        0x10, 0x0f, 0x0e, 0x0d, 0x0c, 0x0b, 0x09, 0x08,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01
    };

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private KeeseState _state = KeeseState.Resting;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _turnAmount;
    private float _speed;
    private bool _flying;

    public EnemyDatabaseEnemyRecord Record { get; private set; }
    internal KeeseState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal bool Flying => _flying;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal int SpriteHeight => Record.SubId == 1 ? -1 : 0;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + SpriteHeight);

    internal void Initialize(
        EnemyDatabaseEnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _counter1 = record.SubId == 0 ? InitialRestFrames : 0;
        _turnAmount = record.SubId == 1 ? 2 : 0;

        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                new[] { record.IdleAnimation, record.FlyAnimation },
                record.TileBase,
                record.Palette));
        RestartAnimation(0);
    }

    internal void UpdateFrame(Vector2 linkPosition, int frameCounter)
    {
        if (IsDead)
            return;

        if (Record.SubId == 1)
            UpdateApproachKeese(linkPosition);
        else
            UpdateNormalKeese(frameCounter);
    }

    public bool TakeSwordHit()
        => TakeSwordHit(1);

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    private void UpdateNormalKeese(int frameCounter)
    {
        switch (_state)
        {
            case KeeseState.Resting:
                if (--_counter1 > 0)
                    return;
                OracleRandomResult startRandom = _random.Next();
                _angle = startRandom.High & 0x1f;
                _counter1 = 0xc0 + (startRandom.Low & 0x3f);
                _speed = SpeedC0;
                _state = KeeseState.Moving;
                SetFlying(true);
                AdvanceAnimation();
                return;

            case KeeseState.Moving:
                ApplySpeed(_speed);
                BounceOffScreenBoundary();
                if ((frameCounter & 1) == 0)
                {
                    _counter1--;
                    if (_counter1 == 0)
                    {
                        _state = KeeseState.Decelerating;
                    }
                    else
                    {
                        OracleRandomResult directionRandom = _random.Next();
                        if ((directionRandom.High & 0x0f) == 0 &&
                            (directionRandom.Low & 0x1f) == 0)
                            _angle = directionRandom.Low & 0x1f;
                    }
                }
                AdvanceAnimation();
                return;

            case KeeseState.Decelerating:
                if (_counter1 < 0x68)
                {
                    ApplySpeed(_speed);
                    BounceOffScreenBoundary();
                }
                if ((_counter1 & 0x0f) == 0)
                    _speed = DecelerationSpeeds[Math.Min(_counter1 >> 4, 7)];
                int mask = DecelerationAnimationMasks[Math.Min(_counter1 >> 4, 7)];
                if ((frameCounter & mask) == 0)
                    AdvanceAnimation();
                _counter1++;
                if (_counter1 != 0x7f)
                    return;

                _state = KeeseState.Resting;
                _counter1 = 0x20 + (_random.Next().Value & 0x7f);
                SetFlying(false);
                return;
        }
    }

    private void UpdateApproachKeese(Vector2 linkPosition)
    {
        if (_state == KeeseState.Resting)
        {
            Vector2 difference = linkPosition - Position;
            if (Mathf.Abs(difference.X) + Mathf.Abs(difference.Y) >= ApproachDistance)
                return;

            _angle = (OracleObjectMath.AngleToward(Position, linkPosition) +
                _turnAmount) & 0x1f;
            _counter1 = TurningInterval;
            _counter2 = TurningIntervals;
            _speed = Speed100;
            _state = KeeseState.Moving;
            SetFlying(true);
            return;
        }

        ApplySpeed(_speed);
        BounceOffScreenBoundary();
        _counter1--;
        if (_counter1 == 0)
        {
            _counter1 = TurningInterval;
            _angle = (_angle + _turnAmount) & 0x1f;
            _counter2--;
            if (_counter2 == 0)
            {
                _state = KeeseState.Resting;
                if ((_random.Next().Value & 0x03) == 0)
                    _turnAmount = -_turnAmount;
                SetFlying(false);
                return;
            }
        }
        AdvanceAnimation();
    }

    private void ApplySpeed(float speed)
    {
        Position += OracleObjectMath.VectorFromAngle32(_angle) * speed;
    }

    private void BounceOffScreenBoundary()
    {
        int doubledAngle = _angle * 2;
        int tableOffset = (doubledAngle & 0x0f) == 0
            ? doubledAngle
            : (doubledAngle & 0xf0) + 8;
        int octant = tableOffset / 8;
        int collisions = 0;
        for (int probe = 0; probe < 4; probe++)
        {
            collisions <<= 1;
            Vector2 point = Position + SideviewBoundaryOffsets[octant, probe];
            if (point.X < 0.0f || point.X >= _room.Width ||
                point.Y < 0.0f || point.Y >= _room.Height)
                collisions |= 1;
        }

        bool hitHorizontal = (collisions & 0x03) != 0;
        bool hitVertical = (collisions & 0x0c) != 0;
        if (hitHorizontal && hitVertical)
            _angle = (_angle + 0x10) & 0x1f;
        else if (hitHorizontal)
            _angle = BoundaryBounceAngles[0x10 + _angle];
        else if (hitVertical)
            _angle = BoundaryBounceAngles[_angle];
    }

    private void SetFlying(bool flying)
    {
        if (_flying == flying)
            return;
        _flying = flying;
        RestartAnimation(flying ? 1 : 0);
    }

}

internal enum KeeseState
{
    Resting,
    Moving,
    Decelerating
}
