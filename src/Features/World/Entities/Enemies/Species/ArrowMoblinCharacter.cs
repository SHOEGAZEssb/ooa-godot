using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_ARROW_MOBLIN $0c:$00. The shared Moblin handler alternates cardinal
/// SPEED_80 routes with an eight-update stand and fires PART_ENEMY_ARROW $1a
/// on every other route change when the selected direction faces Link.
/// </summary>
internal partial class ArrowMoblinCharacter : EnemyCharacter
{
    private readonly ArrowMoblinBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.ArrowMoblin;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private ArrowMoblinState _state;
    private int _counter;
    private int _angle;
    private int _moveCycles;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ArrowMoblinState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int MoveCycles => _moveCycles;
    internal int ScentAttractionCounter => _scentAttraction.Counter;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        if (record.SubId != 0 ||
            record.Id is not (0x0c or 0x22))
        {
            throw new ArgumentOutOfRangeException(
                nameof(record),
                $"Only the shared arrow Moblin/Shrouded Stalfos handler is implemented, got " +
                $"${record.Id:x2}:${record.SubId:x2}.");
        }

        Record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = ArrowMoblinState.Uninitialized;
        _counter = 0;
        _angle = 0;
        _moveCycles = 0;

        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
    }

    /// <returns>The cardinal angle of an arrow to create, or -1.</returns>
    internal int UpdateFrame(
        Vector2 linkPosition,
        Vector2? scentSeedTarget = null)
    {
        if (IsDead)
            return -1;
        if (BeginFrame())
            return -1;
        if (CheckHazards())
            return -1;

        if (_state != ArrowMoblinState.Uninitialized &&
            scentSeedTarget is { } scentPosition)
        {
            _state = ArrowMoblinState.FollowingScentSeed;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: true);
            SetAnimation(_angle >> 3);
            _movement.MoveAtAngle(
                _angle, _behavior.SpeedRaw, allowHoles: false);
            AdvanceAnimation();
            return -1;
        }
        if (_state == ArrowMoblinState.FollowingScentSeed)
        {
            // moblin_state_scentSeed jumps to the shared state-$08 counter
            // initializer, preserving its final scent-facing angle.
            BeginMoving();
            return -1;
        }

        switch (_state)
        {
            case ArrowMoblinState.Uninitialized:
                // arrowDarknut_state_uninitialized selects the angle before
                // arrowDarknut_setState8WithRandomAngleAndCounter consumes
                // the second RNG value for the movement duration.
                _angle = _random.NextCardinalAngle();
                BeginMoving();
                Visible = true;
                return -1;

            case ArrowMoblinState.Moving:
                _counter--;
                bool moved = _counter != 0 &&
                    _movement.MoveAtAngle(
                        _angle, _behavior.SpeedRaw, allowHoles: false);
                if (_counter == 0 || !moved)
                {
                    _state = ArrowMoblinState.Turning;
                    _counter = _behavior.TurnWait;
                }
                AdvanceAnimation();
                return -1;

            case ArrowMoblinState.Turning:
                _counter--;
                if (_counter != 0)
                    return -1;

                // moblin_state_9 consumes the direction RNG first, then the
                // movement-duration RNG. var30 starts at zero, so the first
                // completed route is an eligible firing cycle.
                _angle = _random.NextCardinalAngle();
                BeginMoving();
                _moveCycles++;
                int towardLink =
                    (OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition) + 4) &
                    0x18;
                return (_moveCycles & 1) != 0 && _angle == towardLink
                    ? _angle
                    : -1;

            default:
                throw new InvalidOperationException(
                    $"Unknown Arrow Moblin state {_state}.");
        }
    }

    private void BeginMoving()
    {
        _counter = _behavior.MoveCounterBase +
            (_random.Next().Value & _behavior.MoveCounterMask);
        _state = ArrowMoblinState.Moving;
        RestartAnimation((_angle & 0x18) >> 3);
    }
}

internal enum ArrowMoblinState
{
    Uninitialized = 0,
    FollowingScentSeed = 4,
    Moving = 8,
    Turning = 9
}
