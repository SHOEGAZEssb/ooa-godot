using Godot;
using System;

namespace oracleofages;

internal partial class RopeCharacter : EnemyCharacter
{
    private readonly RopeBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Rope;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private RopeState _state;
    private int _counter;
    private int _cooldown;
    private int _angle;
    private int _speed;
    private bool _initialized;
    private bool _spawnInitialized;
    private bool _spawnCollision;
    private int _zFixed;
    private int _speedZ;
    private int _direction = 0xff;
    private Action<int> _playSound = static _ => { };

    internal ImportedEnemyDefinition Record { get; private set; }
    internal RopeState State => _state;
    internal int Counter => _counter;
    internal int Cooldown => _cooldown;
    internal int Angle => _angle;
    internal int SpeedRaw => _speed;
    internal int ScentAttractionCounter => _scentAttraction.Counter;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal override bool CollisionEnabled => base.CollisionEnabled &&
        (Record.SubId == 0 || _spawnCollision);
    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + new Vector2(0, _zFixed >> 8);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int>? playSound = null)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _playSound = playSound ?? (static _ => { });
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        _speed = _behavior.WanderSpeedRaw;
        _state = RopeState.Wandering;
        if (record.SubId is 2 or 3)
        {
            Visible = false;
            _state = RopeState.SpawnSetup;
            _speed = _behavior.CooldownSpeedRaw;
            ConfigureHazards(room, zPosition: () => _zFixed);
        }
    }

    internal void UpdateFrame(
        Vector2 linkPosition,
        Vector2? scentSeedTarget = null,
        int linkDirection = 0)
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if ((Record.SubId == 0 || _spawnCollision) && CheckHazards())
            return;
        if (!_initialized)
        {
            // State 0 sets direction $ff/SPEED_60 and advances to state 8.
            // State 8 falls through to movement on the following update.
            _initialized = true;
            Visible = true;
            return;
        }
        if (scentSeedTarget is { } scentPosition)
        {
            _spawnInitialized = true;
            _state = RopeState.FollowingScentSeed;
            _speed = _behavior.ChargeSpeedRaw;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: true);
            SetAnimationFromAngle();
            _movement.MoveAtAngle(_angle, _speed, allowHoles: true);
            AdvanceAnimation(3);
            return;
        }
        if (_state == RopeState.FollowingScentSeed)
        {
            _state = RopeState.Wandering;
            _speed = _behavior.WanderSpeedRaw;
            return;
        }
        if (Record.SubId is 2 or 3 && !_spawnInitialized)
        {
            _spawnInitialized = true;
            if (Record.SubId == 2)
            {
                _counter = 8;
                _speed = _behavior.ChargeSpeedRaw;
                _angle = (OracleObjectMovement.Shared.RelativeAngle(
                    OracleObjectMath.ToPixelPosition(Position),
                    OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
                _state = RopeState.SpawnCharge;
            }
            else
            {
                // rope_subid03 stores $fefe (not -$102).
                _speedZ = -0x102;
                _speed = 0x1e;
                _angle = linkDirection * 8;
                _state = RopeState.SpawnBounce;
            }
            SetAnimationFromAngle();
            return;
        }
        if (_state == RopeState.SpawnCharge)
        {
            if (--_counter == 0)
            {
                _spawnCollision = true;
                _state = RopeState.Charging;
            }
            _movement.MoveAtAngle(_angle, _speed, allowHoles: false);
            AdvanceAnimation();
            return;
        }
        if (_state == RopeState.SpawnBounce)
        {
            if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x0e))
            {
                int bouncedSpeed = -_speedZ >> 1;
                if (bouncedSpeed > -0x80 || bouncedSpeed == 0)
                {
                    _speed = _behavior.CooldownSpeedRaw;
                    ChangeDirection();
                    return;
                }
                _speedZ = bouncedSpeed;
                _playSound(OracleSoundEngine.SndBombLand);
            }
            if ((_speedZ >> 8) == 0)
                _spawnCollision = true;
            _movement.MoveAtAngle(_angle, _speed, allowHoles: false);
            QueueRedraw();
            return;
        }
        if (_state == RopeState.Wandering && _cooldown == 0 &&
            IsCenteredWithLink(linkPosition))
        {
            _angle = (OracleObjectMovement.Shared.RelativeAngle(
                OracleObjectMath.ToPixelPosition(Position),
                OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
            _speed = _behavior.ChargeSpeedRaw;
            _state = RopeState.Charging;
            SetAnimationFromAngle();
            return;
        }

        if (_cooldown > 0)
            _cooldown--;
        if (_state == RopeState.Wandering)
            _counter = (_counter - 1) & 0xff;
        bool moved = !(_state == RopeState.Wandering && _counter == 0) &&
            _movement.MoveAtAngle(_angle, _speed, allowHoles: false);
        if (!moved || _state == RopeState.Wandering && _counter == 0)
        {
            if (_state == RopeState.Charging)
            {
                _cooldown = _behavior.CooldownFrames;
                _speed = _behavior.CooldownSpeedRaw;
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
        _counter = _behavior.WanderCounterBase +
            (result.Low & _behavior.WanderCounterMask);
        _state = RopeState.Wandering;
        SetAnimationFromAngle();
    }

    private bool IsCenteredWithLink(Vector2 linkPosition)
    {
        Vector2 rope = OracleObjectMath.ToPixelPosition(Position);
        Vector2 link = OracleObjectMath.ToPixelPosition(linkPosition);
        // objectCheckCenteredWithLink uses a 2*b+1 unsigned range, so b=$0a
        // accepts the inclusive high-byte interval [-10, 10] on either axis.
        return Mathf.Abs(link.X - rope.X) <= _behavior.ApproachAxisRadius ||
            Mathf.Abs(link.Y - rope.Y) <= _behavior.ApproachAxisRadius;
    }

    private void SetAnimationFromAngle()
    {
        if ((_angle & 0x0f) == 0)
            return;
        int direction = ((_angle >> 4) & 1) ^ 1;
        if (_direction == direction)
            return;
        _direction = direction;
        SetAnimation(direction);
    }
}

internal enum RopeState
{
    FollowingScentSeed = 4,
    SpawnSetup = 8,
    SpawnCharge = 11,
    SpawnBounce = 12,
    Wandering = 9,
    Charging = 10
}
