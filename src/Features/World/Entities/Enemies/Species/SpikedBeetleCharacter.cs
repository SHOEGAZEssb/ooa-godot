using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_SPIKED_BEETLE $14:$00. Its normal shell rejects sword damage; a
/// shovel or raised shield enters the source-defined airborne flipped mode,
/// where ordinary sword and Ember Seed damage become effective.
/// </summary>
internal partial class SpikedBeetleCharacter : EnemyCharacter
{
    private readonly SpikedBeetleBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.SpikedBeetle;
    private readonly ArmoredSwordAttackerKnockbackProfile
        _attackerKnockback =
            EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
    private readonly System.Collections.Generic.IReadOnlyList<EnemyBehaviorValue>
        _shakeXOffsets = EnemyBehaviorTables.Shared.SpikedBeetleShakeXOffsets;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _soundRequested = null!;
    private SpikedBeetleState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _speed;
    private int _zFixed;
    private int _speedZ;
    private bool _flippedCollision;
    private bool _flippedRebound;
    private bool _pendingFlipHit;
    private bool _lastSwordHitWasFlipped;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SpikedBeetleState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int ZFixed => _zFixed;
    internal int ZHigh => _zFixed >> 8;
    internal int SpeedZ => _speedZ;
    internal bool FlippedCollision => _flippedCollision;
    internal bool FlippedRebound => _flippedRebound;
    internal bool FlipHitPending => _pendingFlipHit;

    internal int ArmoredAttackerKnockbackFrames(
        EnemyKnockbackStrength strength) => strength switch
        {
            EnemyKnockbackStrength.Low => _attackerKnockback.LowFrames,
            EnemyKnockbackStrength.Normal => _attackerKnockback.NormalFrames,
            EnemyKnockbackStrength.High => _attackerKnockback.HighFrames,
            _ => 0
        };

    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + ZHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> soundRequested)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _soundRequested = soundRequested;
        _state = SpikedBeetleState.Uninitialized;
        _counter1 = 0;
        _counter2 = 0;
        _angle = 0;
        _speed = 0;
        _zFixed = 0;
        _speedZ = 0;
        _flippedCollision = false;
        _flippedRebound = false;
        _pendingFlipHit = false;
        _lastSwordHitWasFlipped = false;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        ConfigureHazards(room, zPosition: () => ZHigh);
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;

        // A source collision writes "just hit"; the next enemy update
        // dispatches that status before the ordinary state.
        if (_pendingFlipHit)
        {
            AdvanceInvincibilityCounter();
            if (CheckHazards())
                return;
            _pendingFlipHit = false;
            if (_state != SpikedBeetleState.Flipped)
                BeginFlip();
            else
                _flippedRebound = _zFixed < 0;
            return;
        }

        if (_flippedRebound && KnockbackCounter != 0)
        {
            AdvanceInvincibilityCounter();
            if (CheckHazards())
                return;
            UpdateFlippedRebound();
            return;
        }

        if (BeginFrame())
            return;
        if (CheckHazards())
            return;

        switch (_state)
        {
            case SpikedBeetleState.Uninitialized:
                SetRandomAngleAndCounter();
                _speed = _behavior.WanderSpeedRaw;
                _state = SpikedBeetleState.Wandering;
                return;

            case SpikedBeetleState.Wandering:
                if (IsCenteredWithLink(
                    linkPosition, _behavior.ApproachAxisRadius))
                {
                    BeginCharge(linkPosition);
                    return;
                }

                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0 ||
                    !_movement.MoveUsingAdjacentWalls(
                        _angle,
                        _speed,
                        allowHoles: false,
                        topDown: false))
                {
                    SetRandomAngleAndCounter();
                    return;
                }
                AdvanceAnimation();
                return;

            case SpikedBeetleState.Charging:
                _counter2 = DecrementByte(_counter2);
                if ((_counter2 & _behavior.ChargeAccelerationMask) == 0 &&
                    _speed < _behavior.ChargeMaximumSpeedRaw)
                {
                    _speed += _behavior.ChargeSpeedStepRaw;
                }
                if (_movement.MoveUsingAdjacentWalls(
                    _angle,
                    _speed,
                    allowHoles: false,
                    topDown: false))
                {
                    AdvanceAnimation();
                    return;
                }

                _state = SpikedBeetleState.Resting;
                _counter1 = _behavior.WallRestFrames;
                return;

            case SpikedBeetleState.Resting:
                if (IsCenteredWithLink(
                    linkPosition, _behavior.ApproachAxisRadius))
                {
                    BeginCharge(linkPosition);
                    return;
                }
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                {
                    AdvanceAnimation();
                    return;
                }

                _state = SpikedBeetleState.Wandering;
                _speed = _behavior.WanderSpeedRaw;
                SetRandomAngleAndCounter();
                return;

            case SpikedBeetleState.Flipped:
                _counter1 = DecrementByte(_counter1);
                if (_counter1 == 0)
                {
                    BeginFlipBack();
                    return;
                }
                if (_counter1 < _behavior.ShakeThreshold)
                {
                    int offsetIndex = (_counter1 & 0x06) >> 1;
                    Position += Vector2.Right *
                        _shakeXOffsets[offsetIndex].Value;
                }
                AdvanceAnimation();
                return;

            case SpikedBeetleState.FlippingBack:
                _movement.MoveUsingAdjacentWalls(
                    _angle,
                    _speed,
                    allowHoles: false,
                    topDown: false);
                AdvanceAnimation();
                if (!OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed,
                    ref _speedZ,
                    _behavior.Gravity))
                {
                    QueueRedraw();
                    return;
                }

                _state = SpikedBeetleState.Wandering;
                if (IsCenteredWithLink(
                    linkPosition, _behavior.LandingApproachAxisRadius))
                {
                    BeginCharge(linkPosition);
                }
                else
                {
                    _speed = _behavior.WanderSpeedRaw;
                }
                QueueRedraw();
                return;
        }
    }

    internal bool TryApplyFlipHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int shieldLevel)
    {
        if (!CollisionEnabled || InvincibilityCounter != 0 ||
            _pendingFlipHit || !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }

        bool highKnockback = shieldLevel == 1;
        ApplyCollisionBump(
            sourcePosition,
            highKnockback ? 21 : 16,
            highKnockback ? 11 : 8);
        _pendingFlipHit = true;
        return true;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        _lastSwordHitWasFlipped = _flippedCollision;
        if (_flippedCollision)
            return base.TakeSwordHit(sourcePosition, damage);
        return AcceptArmoredSwordHit(
            _behavior.ArmoredInvincibilityFrames);
    }

    internal override bool TakeBurnHit(int damage) =>
        _flippedCollision && base.TakeBurnHit(damage);

    internal void ApplyAcceptedSwordResponse(
        Vector2 sourcePosition,
        EnemyKnockbackStrength strength)
    {
        if (!_lastSwordHitWasFlipped)
            return;

        ApplySwordKnockback(sourcePosition, strength);
        if (_zFixed >= 0)
            _flippedRebound = false;
    }

    private void BeginFlip()
    {
        _flippedRebound = true;
        _flippedCollision = true;
        _state = SpikedBeetleState.Flipped;
        _counter1 = _behavior.FlippedWaitFrames;
        _zFixed = 0;
        _speedZ = _behavior.InitialSpeedZ;
        _angle = KnockbackAngle ^ 0x10;
        SetAnimation(1);
        _soundRequested(OracleSoundEngine.SndBombLand);
        QueueRedraw();
    }

    private void UpdateFlippedRebound()
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed,
            ref _speedZ,
            _behavior.Gravity);
        bool stopped = false;
        if (landed)
        {
            int reboundSpeed = (-_speedZ) >> 1;
            if (reboundSpeed > -0x80)
            {
                stopped = true;
            }
            else
            {
                _speedZ = reboundSpeed;
            }
        }

        KnockbackCounter = stopped ? 0 : 1;
        _movement.MoveUsingAdjacentWalls(
            KnockbackAngle,
            _behavior.FlippedRecoilSpeedRaw,
            allowHoles: true,
            topDown: false);
        QueueRedraw();
    }

    private void BeginFlipBack()
    {
        _state = SpikedBeetleState.FlippingBack;
        _speed = _behavior.FlipBackSpeedRaw;
        _flippedCollision = false;
        _flippedRebound = false;
        Position += Vector2.Right;
        _zFixed = 0;
        _speedZ = _behavior.InitialSpeedZ;
        SetAnimation(0);
        QueueRedraw();
    }

    private void SetRandomAngleAndCounter()
    {
        OracleRandomResult result = _random.Next();
        _angle = result.High & 0x18;
        _counter1 = 0x30 + (result.Low & 0x30);
    }

    private void BeginCharge(Vector2 linkPosition)
    {
        _angle = (OracleObjectMovement.Shared.RelativeAngle(
            OracleObjectMath.ToPixelPosition(Position),
            OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
        _state = SpikedBeetleState.Charging;
        _counter2 = _behavior.ChargeCounter;
        _speed = _behavior.ChargeInitialSpeedRaw;
    }

    private bool IsCenteredWithLink(Vector2 linkPosition, int radius)
    {
        Vector2 beetle = OracleObjectMath.ToPixelPosition(Position);
        Vector2 link = OracleObjectMath.ToPixelPosition(linkPosition);
        return Mathf.Abs(link.X - beetle.X) <= radius ||
            Mathf.Abs(link.Y - beetle.Y) <= radius;
    }

    private static int DecrementByte(int value) => (value - 1) & 0xff;
}

internal enum SpikedBeetleState
{
    Uninitialized = 0,
    Wandering = 8,
    Charging = 9,
    Resting = 10,
    Flipped = 11,
    FlippingBack = 12
}
