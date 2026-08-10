using Godot;

namespace oracleofages;

internal partial class LeeverCharacter : EnemyCharacter
{
    private readonly LeeverBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Leever;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private LeeverState _state;
    private int _counter;
    private int _angle;
    private bool _collisionEnabled;
    private bool _emergenceComplete;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal LeeverState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal override bool CollisionEnabled =>
        _collisionEnabled && base.CollisionEnabled;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _state = LeeverState.Uninitialized;
        _collisionEnabled = false;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        Visible = false;
    }

    internal void UpdateFrame(
        Vector2 linkPosition,
        Vector2I linkFacing,
        int frameCounter)
    {
        if (IsDead || BeginFrame())
            return;
        if (CheckHazards())
            return;

        switch (_state)
        {
            case LeeverState.Uninitialized:
                _state = LeeverState.Underground;
                SetRandomUndergroundCounter();
                return;

            case LeeverState.Underground:
                _counter = (_counter - 1) & 0xff;
                if (_counter != 0)
                    return;

                // ecom_decCounter1 leaves hl on counter1; the source
                // increments it back to one before retrying a rejected cell.
                _counter = 1;
                if (!TryChooseSpawnPosition(
                    linkPosition, linkFacing, frameCounter, out Vector2 spawn))
                {
                    return;
                }

                Position = spawn;
                _state = LeeverState.Emerging;
                _emergenceComplete = false;
                RestartAnimation(0);
                Visible = true;
                QueueRedraw();
                return;

            case LeeverState.Emerging:
                if (_emergenceComplete)
                {
                    _state = LeeverState.Chasing;
                    _collisionEnabled = true;
                    _angle = CardinalAngleToward(linkPosition);
                    _counter =
                        (_random.Next().Value & _behavior.ChaseCounterMask) +
                        _behavior.ChaseCounterBase;
                    AdvanceAnimation();
                    return;
                }

                int previousFrame = AnimationFrame;
                AdvanceAnimation();
                if (previousFrame != 0 && AnimationFrame == 0)
                {
                    // Animation $00 runs into animation $01 in ROM data. The
                    // imported animation is bounded at its label, so select
                    // the same first walking frame explicitly on the wrap.
                    RestartAnimation(1);
                    _emergenceComplete = true;
                }
                return;

            case LeeverState.Chasing:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0 || HasWallOrHoleAhead())
                {
                    BeginSinking();
                    return;
                }

                Position += OracleObjectMovement.Shared.Delta(
                    _behavior.ChaseSpeedRaw, _angle);
                AdvanceAnimation();
                QueueRedraw();
                return;

            case LeeverState.Sinking:
                if (AnimationParameter == 1)
                {
                    _state = LeeverState.Underground;
                    SetRandomUndergroundCounter();
                    Visible = false;
                    QueueRedraw();
                    return;
                }
                AdvanceAnimation();
                return;
        }
    }

    private bool TryChooseSpawnPosition(
        Vector2 linkPosition,
        Vector2I linkFacing,
        int frameCounter,
        out Vector2 spawn)
    {
        int direction = linkFacing == Vector2I.Up ? 0
            : linkFacing == Vector2I.Right ? 1
            : linkFacing == Vector2I.Down ? 2
            : linkFacing == Vector2I.Left ? 3
            : 0;
        int offsetIndex = direction * 4 + (frameCounter & 0x03);
        int packed = unchecked((byte)(
            _room.GetPackedPosition(linkPosition) +
            _behavior.LinkRelativeOffsets[offsetIndex].Value));
        int tileY = packed >> 4;
        int tileX = packed & 0x0f;
        if (tileY >= 8 || tileX >= 10)
        {
            spawn = default;
            return false;
        }

        spawn = new Vector2(tileX * 16 + 8, tileY * 16 + 8);
        return _room.GetTerrainInfo(spawn).Collision == 0;
    }

    private int CardinalAngleToward(Vector2 linkPosition) =>
        (OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition) +
            _behavior.CardinalRounding) & _behavior.CardinalMask;

    private bool HasWallOrHoleAhead() =>
        EnemyAdjacentWallResolver.Shared.ProbeTopDown(
            Position,
            _angle,
            point =>
                point.X < 0 || point.X >= _room.Width ||
                point.Y < 0 || point.Y >= _room.Height ||
                _room.IsSolid(point) ||
                _room.GetTerrainInfo(point).Hazard == HazardType.Hole)
        .Bitset != 0;

    private void BeginSinking()
    {
        _state = LeeverState.Sinking;
        _collisionEnabled = false;
        RestartAnimation(2);
    }

    private void SetRandomUndergroundCounter()
    {
        int index = _random.Next().Value & 0x03;
        _counter = _behavior.UndergroundCounters[index].Value;
    }
}

internal enum LeeverState
{
    Uninitialized,
    Underground = 8,
    Emerging,
    Chasing,
    Sinking
}
