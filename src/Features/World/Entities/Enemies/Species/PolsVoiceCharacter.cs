using Godot;

namespace oracleofages;

/// <summary>
/// ENEMY_POLS_VOICE $23:$00. The source alternates a random grounded wait
/// with either a cardinal SPEED_80 hop or a rare SPEED_c0 hop aimed toward
/// Link. Any nonzero wLinkPlayingInstrument status kills it immediately.
/// </summary>
internal partial class PolsVoiceCharacter : EnemyCharacter
{
    private readonly PolsVoiceBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.PolsVoice;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private PolsVoiceState _state;
    private int _counter;
    private int _angle;
    private int _speed;
    private int _zFixed;
    private int _speedZ;
    private int _gravity;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal PolsVoiceState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int SpeedRaw => _speed;
    internal int ZFixed => _zFixed;
    internal int ZHigh => _zFixed >> 8;
    internal int SpeedZ => _speedZ;
    internal int Gravity => _gravity;
    internal bool DiedFromInstrument { get; private set; }

    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + ZHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = PolsVoiceState.Uninitialized;
        _counter = 0;
        _angle = 0;
        _speed = 0;
        _zFixed = 0;
        _speedZ = 0;
        _gravity = 0;
        DiedFromInstrument = false;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        ConfigureHazards(
            room,
            animateWhileFallingInHole: false,
            zPosition: () => ZHigh);
    }

    internal void UpdateFrame(Vector2 linkPosition, bool instrumentPlaying)
    {
        if (IsDead)
            return;
        if (CheckHazards())
            return;

        // polsVoice_checkLinkPlayingInstrument replaces the ordinary enemy
        // status with ENEMYSTATUS_NO_HEALTH before knockback/state dispatch.
        if (instrumentPlaying)
        {
            DiedFromInstrument = true;
            Finish();
            return;
        }
        if (BeginFrame())
            return;

        switch (_state)
        {
            case PolsVoiceState.Uninitialized:
                InitializeWaitingState();
                return;

            case PolsVoiceState.Waiting:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                    BeginJump(linkPosition);
                return;

            case PolsVoiceState.Jumping:
                _movement.MoveUsingAdjacentWalls(
                    _angle,
                    _speed,
                    allowHoles: false,
                    topDown: false);
                if (!OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed,
                        ref _speedZ,
                        _gravity))
                {
                    QueueRedraw();
                    return;
                }

                _state = PolsVoiceState.Waiting;
                _counter = _behavior.LandingWaitFrames;
                SetAnimation(1);
                QueueRedraw();
                return;
        }
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (_state == PolsVoiceState.Uninitialized)
            InitializeWaitingState();
        return Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
    }

    internal bool TakeBumpHit(Vector2 sourcePosition, int damage)
    {
        _ = sourcePosition;
        _ = damage;
        return !IsDead && CollisionEnabled && InvincibilityCounter == 0;
    }

    internal bool TakeDamagingHit(Vector2 sourcePosition, int damage) =>
        base.TakeSwordHit(sourcePosition, damage);

    internal void ApplyPolsVoiceBump(
        Vector2 sourcePosition,
        EnemyKnockbackStrength strength) =>
        ApplyCollisionBump(sourcePosition, strength);

    internal override bool TakeBurnHit(int damage) => false;

    private void InitializeWaitingState()
    {
        OracleRandomResult result = _random.Next();
        _counter = (result.Value & _behavior.InitialCounterMask) +
            _behavior.InitialCounterAdd;
        _state = PolsVoiceState.Waiting;
        SetAnimation(1);
        Visible = true;
        QueueRedraw();
    }

    private void BeginJump(Vector2 linkPosition)
    {
        OracleRandomResult result = _random.Next();
        bool normalJump =
            (result.High & _behavior.JumpTypeHighMask) != 0;
        if (normalJump)
        {
            _speedZ = _behavior.NormalInitialSpeedZ;
            _gravity = _behavior.NormalGravity;
            _speed = _behavior.NormalSpeedRaw;
            _angle = result.Low & _behavior.RandomAngleMask;
        }
        else
        {
            _speedZ = _behavior.FastInitialSpeedZ;
            _gravity = _behavior.FastGravity;
            _speed = _behavior.FastSpeedRaw;
            _angle = (OracleObjectMovement.Shared.RelativeAngle(
                OracleObjectMath.ToPixelPosition(Position),
                OracleObjectMath.ToPixelPosition(linkPosition)) +
                _behavior.TargetAngleRounding) &
                _behavior.TargetAngleMask;
        }
        _state = PolsVoiceState.Jumping;
        SetAnimation(0);
        QueueRedraw();
    }
}

internal enum PolsVoiceState
{
    Uninitialized,
    Waiting = 8,
    Jumping = 9
}
