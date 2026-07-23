using Godot;
using System;

namespace oracleofages;

internal partial class RopeCharacter : SpiritsGraveEnemyCharacter
{

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private RopeState _state;
    private int _counter;
    private int _cooldown;
    private int _angle;
    private float _speed;
    private bool _initialized;

    internal RopeState State => _state;
    internal int Counter => _counter;
    internal int Cooldown => _cooldown;
    internal int Angle => _angle;
    internal float Speed => _speed;

    internal void Initialize(
        EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _speed = 0.5f;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            // State 0 sets direction $ff/SPEED_80 and advances to state 8.
            // State 8 falls through to movement on the following update.
            _initialized = true;
            return;
        }
        if (_state == RopeState.Wandering && _cooldown == 0 &&
            IsCenteredWithLink(linkPosition))
        {
            _angle = (OracleObjectMath.AngleToward(
                OracleObjectMath.ToPixelPosition(Position),
                OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
            _speed = 1.25f;
            _state = RopeState.Charging;
            SetAnimationFromAngle();
            return;
        }

        if (_cooldown > 0)
            _cooldown--;
        if (_state == RopeState.Wandering)
            _counter = (_counter - 1) & 0xff;
        bool moved = _movement.MoveAtAngle(_angle, _speed, allowHoles: false);
        if (!moved || _state == RopeState.Wandering && _counter == 0)
        {
            if (_state == RopeState.Charging)
            {
                _cooldown = 0x40;
                _speed = 0.375f;
            }
            ChangeDirection();
            return;
        }
        AdvanceAnimation(_state == RopeState.Charging ? 3 : 1);
    }

    private void ChangeDirection()
    {
        OracleRandomResult result = _random.Next();
        _angle = result.High & 0x18;
        _counter = 0x70 + (result.Low & 0x70);
        _state = RopeState.Wandering;
        SetAnimationFromAngle();
    }

    private bool IsCenteredWithLink(Vector2 linkPosition)
    {
        Vector2 rope = OracleObjectMath.ToPixelPosition(Position);
        Vector2 link = OracleObjectMath.ToPixelPosition(linkPosition);
        // objectCheckCenteredWithLink uses a 2*b+1 unsigned range, so b=$0a
        // accepts the inclusive high-byte interval [-10, 10] on either axis.
        return Mathf.Abs(link.X - rope.X) <= 0x0a ||
            Mathf.Abs(link.Y - rope.Y) <= 0x0a;
    }

    private void SetAnimationFromAngle() =>
        SetAnimation((_angle & 0x10) != 0 ? 0 : 1);
}
