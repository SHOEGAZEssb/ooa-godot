using Godot;
using System;

namespace oracleofages;

internal partial class BoomerangMoblinCharacter : SpiritsGraveEnemyCharacter
{

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private BoomerangMoblinCharacterMoblinState _state;
    private int _counter;
    private int _angle;
    private bool _initialized;
    private bool _boomerangReturned;

    internal BoomerangMoblinCharacterMoblinState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;

    internal void Initialize(
        EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
    }

    internal int UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return -1;
        BeginFrame();
        if (!_initialized)
        {
            // State 0 initializes SPEED_80 and chooses the first route on the
            // enemy's first object update, not while the room is parsed.
            _initialized = true;
            ChooseDirection();
            return -1;
        }
        switch (_state)
        {
            case BoomerangMoblinCharacterMoblinState.Moving:
                _counter--;
                if (_counter == 0 || !_movement.MoveAtAngle(
                    _angle, 0.5f, allowHoles: false))
                {
                    _state = BoomerangMoblinCharacterMoblinState.Deciding;
                }
                break;

            case BoomerangMoblinCharacterMoblinState.Deciding:
                ChooseDirection();
                int target = (OracleObjectMath.AngleToward(Position, linkPosition) + 4) & 0x18;
                if (target == _angle)
                {
                    _state = BoomerangMoblinCharacterMoblinState.WaitingForBoomerang;
                    return _angle;
                }
                break;

            case BoomerangMoblinCharacterMoblinState.WaitingForBoomerang:
                if (_boomerangReturned)
                {
                    _boomerangReturned = false;
                    ChooseDirection();
                }
                break;
        }
        AdvanceAnimation();
        return -1;
    }

    internal void ReturnBoomerang() => _boomerangReturned = true;

    private void ChooseDirection()
    {
        // @gotoState8WithRandomAngleAndCounter calls getRandomNumber for the
        // duration, then ecom_setRandomCardinalAngle consumes a second value.
        _counter = 0x30 + (_random.Next().Value & 0x03) * 0x10;
        _angle = _random.Next().Value & 0x18;
        _state = BoomerangMoblinCharacterMoblinState.Moving;
        SetAnimation(_angle >> 3);
    }
}
