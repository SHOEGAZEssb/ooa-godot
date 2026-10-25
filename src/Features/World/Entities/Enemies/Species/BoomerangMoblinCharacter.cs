using Godot;
using System;

namespace Oracles;

internal partial class BoomerangMoblinCharacter : EnemyCharacter
{
    internal override bool InitializationPending => !_initialized;


    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private BoomerangMoblinCharacterMoblinState _state;
    private int _counter;
    private int _angle;
    private bool _initialized;
    private bool _boomerangReturned;
    private readonly EnemyBehaviorTables _behavior = EnemyBehaviorTables.Shared;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal BoomerangMoblinCharacterMoblinState State => _state;
    internal int ScentAttractionCounter => _scentAttraction.Counter;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
    }

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
        if (!_initialized)
        {
            // State 0 initializes SPEED_80 and chooses the first route on the
            // enemy's first object update, not while the room is parsed.
            _initialized = true;
            _random.Next(); // enemyStandardUpdate's state-zero var3d draw precedes route selection.
            ChooseDirection();
            return -1;
        }
        if (scentSeedTarget is { } scentPosition)
        {
            _state = BoomerangMoblinCharacterMoblinState.FollowingScentSeed;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: true);
            SetAnimation(_angle >> 3);
            _movement.MoveAtAngle(
                _angle,
                _behavior.BoomerangMoblin.SpeedRaw,
                allowHoles: false);
            AdvanceAnimation();
            return -1;
        }
        if (_state == BoomerangMoblinCharacterMoblinState.FollowingScentSeed)
        {
            ChooseDirection();
            return -1;
        }
        switch (_state)
        {
            case BoomerangMoblinCharacterMoblinState.Moving:
                _counter--;
                if (_counter == 0 || !_movement.MoveAtAngle(
                    _angle,
                    _behavior.BoomerangMoblin.SpeedRaw,
                    allowHoles: false))
                {
                    _state = BoomerangMoblinCharacterMoblinState.Deciding;
                }
                break;

            case BoomerangMoblinCharacterMoblinState.Deciding:
                ChooseDirection();
                int target = (OracleObjectMovement.Shared.RelativeAngle(
                    Position, linkPosition) + 4) & ObjectAngle.CardinalMask;
                if (target == _angle)
                {
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

    // boomerangMoblin.s @state_9 changes state only after
    // ecom_spawnProjectile succeeds. Failure retains the newly chosen route.
    internal void BeginWaitingForBoomerang() =>
        _state = BoomerangMoblinCharacterMoblinState.WaitingForBoomerang;

    private void ChooseDirection()
    {
        // @gotoState8WithRandomAngleAndCounter calls getRandomNumber for the
        // duration, then ecom_setRandomCardinalAngle consumes a second value.
        _counter = _behavior.BoomerangMoblinRouteCounters[
            _random.Next().Value & 0x03].Value;
        _angle = _random.NextCardinalAngle();
        _state = BoomerangMoblinCharacterMoblinState.Moving;
        SetAnimation(_angle >> 3);
    }
}

internal enum BoomerangMoblinCharacterMoblinState
{
    FollowingScentSeed = 4,
    Moving = 8,
    Deciding = 9,
    WaitingForBoomerang = 10
}
