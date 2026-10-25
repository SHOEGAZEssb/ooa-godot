using Godot;

namespace Oracles;

internal partial class SandCrabCharacter : EnemyCharacter, ISwitchHookEnemy, ITerrainShadowSource
{
    internal override bool InitializationPending => _state == SandCrabState.Uninitialized;

    private readonly SandCrabBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.SandCrab;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly ScentSeedAttraction _scentAttraction = new();
    private SandCrabState _state;
    private int _counter;
    private int _angle;
    private int _speedRaw;
    private int _zFixed;
    private int _speedZ;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SandCrabState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int SpeedRaw => _speedRaw;
    internal int ScentAttractionCounter => _scentAttraction.Counter;
    internal int ZFixed => _zFixed;
    internal int SwitchHookSubstate { get; private set; }
    internal override bool CollisionEnabled => base.CollisionEnabled && _state != SandCrabState.SwitchHook;
    int? ITerrainShadowSource.TerrainShadowZHigh => _zFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0, _zFixed >> 8);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = SandCrabState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain);
    }

    internal void UpdateFrame(Vector2? scentSeedTarget = null)
    {
        if (IsDead || BeginFrame())
            return;

        // commonCode.s:ecom_checkScentSeedActive only redirects states >= $08.
        // State $04 continues its own scent handler; state $03 stays held.
        if ((_state >= SandCrabState.ChoosingDirection || _state == SandCrabState.FollowingScentSeed) &&
            scentSeedTarget is { } scentPosition)
        {
            _state = SandCrabState.FollowingScentSeed;
            _angle = _scentAttraction.UpdateAngle(
                Position, scentPosition, _angle, cardinal: true);
            _speedRaw = (_angle & 0x08) == 0
                ? _behavior.VerticalSpeedRaw
                : _behavior.HorizontalSpeedRaw;
            _movement.MoveAtAngle(_angle, _speedRaw, allowHoles: false);
            AdvanceAnimation();
            return;
        }
        if (_state == SandCrabState.FollowingScentSeed)
        {
            _state = SandCrabState.ChoosingDirection;
            AdvanceAnimation();
            return;
        }

        switch (_state)
        {
            case SandCrabState.SwitchHook:
                // sandCrab.s:@state_switchHook: $00 increments once, $01/$02
                // wait; $03 uses commonCode.s:ecom_fallToGroundAndSetState8.
                if (SwitchHookSubstate == 0) SwitchHookSubstate = 1;
                else if (SwitchHookSubstate == 3 &&
                    OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20))
                    _state = SandCrabState.ChoosingDirection;
                QueueRedraw();
                return;

            case SandCrabState.Uninitialized:
                _state = SandCrabState.ChoosingDirection;
                Visible = true;
                return;

            case SandCrabState.ChoosingDirection:
                OracleRandomResult result = _random.Next();
                _state = SandCrabState.Moving;
                _angle = result.High & _behavior.AngleMask;
                _counter = _behavior.DurationBase +
                    (result.Low & _behavior.DurationMask);
                _speedRaw = (_angle & 0x08) == 0
                    ? _behavior.VerticalSpeedRaw
                    : _behavior.HorizontalSpeedRaw;
                AdvanceAnimation();
                return;

            case SandCrabState.Moving:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                {
                    _state = SandCrabState.ChoosingDirection;
                    AdvanceAnimation();
                    return;
                }

                Vector2 before = Position;
                bool moved = _movement.MoveUsingAdjacentWalls(
                    _angle, _speedRaw, allowHoles: false, topDown: false);
                bool crossedPixel =
                    Mathf.FloorToInt(before.X) != Mathf.FloorToInt(Position.X) ||
                    Mathf.FloorToInt(before.Y) != Mathf.FloorToInt(Position.Y);
                bool wallSlide = (_angle & 0x08) == 0
                    ? !Mathf.IsEqualApprox(before.X, Position.X)
                    : !Mathf.IsEqualApprox(before.Y, Position.Y);
                if (!moved || !crossedPixel && !wallSlide)
                    _state = SandCrabState.ChoosingDirection;
                AdvanceAnimation();
                return;
        }
    }

    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead &&
        _state == SandCrabState.SwitchHook && SwitchHookSubstate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition)
    {
        // collisionEffects.s:collisionEffect2e clears recoil, disables collision,
        // and selects state $03:$00 without changing health, counters or RNG.
        KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), linkPosition.Floor()) ^ ObjectAngle.HalfTurn;
        _state = SandCrabState.SwitchHook;
        SwitchHookSubstate = 0;
    }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    {
        Position = position.Floor() + Position - Position.Floor();
        _zFixed = (zHigh << 8) | (_zFixed & 0xff);
        QueueRedraw();
    }
    public void SwapSwitchHook() => SwitchHookSubstate = 2;
    public void ReleaseSwitchHook()
    {
        if (SwitchHookHeld) SwitchHookSubstate = 3;
    }

}

internal enum SandCrabState
{
    Uninitialized,
    SwitchHook = 3,
    FollowingScentSeed = 4,
    ChoosingDirection = 8,
    Moving
}
