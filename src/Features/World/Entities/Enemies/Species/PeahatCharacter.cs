using Godot;

namespace oracleofages;

internal partial class PeahatCharacter : EnemyCharacter
{
    private readonly PeahatBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Peahat;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private PeahatState _state;
    private int _counter;
    private int _angle;
    private int _zHigh;
    private int _speedRaw;
    private bool _hookHitPending;

    // peahat_updateEnemyCollisionMode runs before this update's Z movement.
    internal int CollisionMode { get; private set; } = 0x58;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal PeahatState State => _state;
    internal int Counter => _counter;
    internal int ZHigh => _zHigh;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + _zHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _state = PeahatState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
    }

    internal void UpdateFrame(int frameCounter = 0)
    {
        if (_hookHitPending)
        {
            _hookHitPending = false;
            if (CollisionMode != 0x58)
            {
                AdvanceInvincibilityCounter();
                return;
            }
        }
        if (IsDead || BeginFrame())
            return;
        if (_state == PeahatState.Uninitialized)
        {
            _random.Next(); // enemyStandardUpdate var3d.
            RestartAnimation(0); // enemyLoadGraphicsAndProperties.
        }
        CollisionMode = _zHigh == 0 ? 0x2e : 0x58;

        switch (_state)
        {
            case PeahatState.Uninitialized:
                _state = PeahatState.Stationary;
                _counter = 1;
                Visible = true;
                return;

            case PeahatState.Stationary:
                if (--_counter != 0)
                    return;
                _state = PeahatState.Accelerating;
                _counter = _behavior.AccelerationFrames;
                _speedRaw = _behavior.InitialSpeedRaw;
                AdvanceAnimation();
                return;

            case PeahatState.Accelerating:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                {
                    _state = PeahatState.Flying;
                    int index = _random.Next().Value & 7;
                    _counter = _behavior.FlightCounters[index];
                    _angle = _random.Next().Value & 0x1f;
                    AdvanceAnimation();
                    return;
                }
                UpdateAccelerationPosition(frameCounter);
                return;

            case PeahatState.Flying:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                {
                    _state = PeahatState.Slowing;
                    _counter = 0;
                    AdvanceAnimation();
                    return;
                }
                if ((_counter & 0x1f) == 0)
                    _angle = _random.Next().Value & 0x1f;
                MoveBouncing();
                AdvanceAnimation();
                return;

            case PeahatState.Slowing:
                _counter++;
                if (_counter == _behavior.SlowdownFrames)
                {
                    _state = PeahatState.Stationary;
                    _counter = _room.IsSolid(Position) ? 1 : 0x80;
                    _zHigh = 0;
                    AdvanceAnimation();
                    return;
                }
                UpdateAccelerationPosition(frameCounter);
                return;
        }
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (CollisionMode != 0x2e)
            return false;
        return TakeSwitchHookHit(sourcePosition, damage);
    }

    internal override bool TakeBurnHit(int damage) =>
        _zHigh == 0 && base.TakeBurnHit(damage);

    internal bool TakeSwitchHookHit(Vector2 linkPosition, int damage)
    {
        if (CollisionMode == 0x2e && !TakeDeferredNoKnockbackHit(linkPosition, damage)) return false;
        _hookHitPending = true;
        return true;
    }

    private void UpdateAccelerationPosition(int frameCounter)
    {
        int value = (_counter - 1) & 0xff;
        if (value < 0x41)
        {
            int index = (value & 0x78) >> 3;
            // peahat_updatePosition subtracts six from the speed-table index.
            // Indices 6-8 clamp to ground height; 5-0 become Z -1 through -6.
            _zHigh = index < 6 ? index - 6 : 0;
            _speedRaw = _behavior.Speeds[index].Value;
            MoveBouncing();
        }
        int mask = _behavior.AnimationFrequencies[_counter >> 4].Value;
        if (mask == 0xff)
        {
            AdvanceAnimation();
            mask = 0;
        }
        if ((frameCounter & mask) == 0) AdvanceAnimation();
    }

    private void MoveBouncing()
    {
        // The source uses objectApplySpeed followed by
        // ecom_bounceOffScreenBoundary. Peahats ignore metatile collision even
        // while their takeoff/landing sprite is at ground height.
        OracleObjectVelocity velocity = OracleObjectMovement.Shared.Velocity(_speedRaw, _angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position,
            _angle,
            point =>
                point.X < 0 || point.X >= _room.Width ||
                point.Y < 0 || point.Y >= _room.Height);
        QueueRedraw();
    }
}

internal enum PeahatState
{
    Uninitialized,
    Stationary = 8,
    Accelerating,
    Flying,
    Slowing
}
