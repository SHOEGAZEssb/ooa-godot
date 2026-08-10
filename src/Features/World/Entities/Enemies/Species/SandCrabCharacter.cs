using Godot;

namespace oracleofages;

internal partial class SandCrabCharacter : EnemyCharacter
{
    private readonly SandCrabBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.SandCrab;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private SandCrabState _state;
    private int _counter;
    private int _angle;
    private int _speedRaw;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SandCrabState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int SpeedRaw => _speedRaw;

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

    internal void UpdateFrame()
    {
        if (IsDead || BeginFrame())
            return;

        switch (_state)
        {
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

}

internal enum SandCrabState
{
    Uninitialized,
    ChoosingDirection = 8,
    Moving
}
