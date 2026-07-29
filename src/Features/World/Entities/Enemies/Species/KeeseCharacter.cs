using Godot;
using System;

namespace oracleofages;

public partial class KeeseCharacter : EnemyCharacter
{

    private const int SpeedC0 = 0x1e;
    private const int Speed100 = 0x28;
    private const int InitialRestFrames = 0x20;
    private const int ApproachDistance = 0x31;
    private const int TurningInterval = 12;
    private const int TurningIntervals = 12;

    private static readonly int[] DecelerationSpeeds =
    {
        0x1e, 0x14, 0x0a, 0x0a, 0x05, 0x05, 0x05, 0x05
    };
    private static readonly int[] DecelerationAnimationMasks =
    {
        0x00, 0x00, 0x01, 0x01, 0x03, 0x03, 0x07, 0x00
    };

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private KeeseState _state = KeeseState.Resting;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _turnAmount;
    private int _speed;
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
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary);
        RestartAnimation(0);
    }

    internal void UpdateFrame(Vector2 linkPosition, int frameCounter)
    {
        if (IsDead)
            return;
        if (BeginFrame())
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
        if (IsDead || InvincibilityCounter > 0)
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

    private void ApplySpeed(int speed)
    {
        Position += OracleObjectSpeedTable.Shared.Delta(speed, _angle);
    }

    private void BounceOffScreenBoundary()
    {
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position,
            _angle,
            point =>
                point.X < 0 || point.X >= _room.Width ||
                point.Y < 0 || point.Y >= _room.Height);
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
