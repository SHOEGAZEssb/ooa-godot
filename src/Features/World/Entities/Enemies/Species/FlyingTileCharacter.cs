using Godot;
using System;

namespace oracleofages;

/// <summary>Active subid-$80 form of ENEMY_FLYING_TILE $52.</summary>
internal partial class FlyingTileCharacter : EnemyCharacter
{
    private readonly FlyingTileBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.FlyingTile;
    private OracleRoomData _room = null!;
    private Action _roomTileChanged = null!;
    private Func<long> _animationTick = null!;
    private Vector2 _precisePosition;
    private FlyingTileState _state;
    private int _counter;
    private int _zFixed;
    private int _angle;
    private bool _debrisPending;
    private bool _collisionBreakPending;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal FlyingTileState State => _state;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal int ZHigh => _zFixed >> 8;
    internal int Angle => _angle;
    internal int SpeedRaw => _behavior.SpeedRaw;
    internal override bool CollisionEnabled =>
        !_collisionBreakPending &&
        _state is not FlyingTileState.Initializing && base.CollisionEnabled;

    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + new Vector2(0, ZHigh);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        Record = record;
        _room = room;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _precisePosition = position;
        _state = FlyingTileState.Initializing;
        _counter = 0;
        _zFixed = 0;
        _angle = 0;
        _debrisPending = false;
        _collisionBreakPending = false;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        // collisionEffect1c and collisionEffect20 both write Object.var2a.
        // enemyCode52 dispatches that status before its ordinary state on the
        // next object update and jumps directly to flyingTile_dead.
        if (_collisionBreakPending)
        {
            _collisionBreakPending = false;
            BreakImmediately();
            return;
        }
        if (BeginFrame())
            return;

        switch (_state)
        {
            case FlyingTileState.Initializing:
                _room.SetPositionTileAndCollision(
                    Position,
                    (byte)_behavior.ReplacementTile,
                    collision: null,
                    _animationTick());
                _roomTileChanged();
                _state = FlyingTileState.Rising;
                Visible = true;
                QueueRedraw();
                return;

            case FlyingTileState.Rising:
                _zFixed += _behavior.RiseDeltaZFixed;
                if (ZHigh < _behavior.RiseHighByteThreshold)
                {
                    _state = FlyingTileState.Waiting;
                    _counter = _behavior.ChargeWaitFrames;
                }
                AdvanceAnimation();
                QueueRedraw();
                return;

            case FlyingTileState.Waiting:
                _counter--;
                if (_counter == 0)
                {
                    _state = FlyingTileState.Charging;
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition);
                }
                AdvanceAnimation();
                return;

            case FlyingTileState.Charging:
                Position = OracleObjectMovement.Shared.ApplySpeed(
                    ref _precisePosition,
                    _behavior.SpeedRaw,
                    _angle);
                if (_room.IsSolid(Position))
                {
                    BreakImmediately();
                    return;
                }
                AdvanceAnimation();
                return;
        }
    }

    internal bool QueueCollisionBreak()
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter != 0)
            return false;
        _collisionBreakPending = true;
        return true;
    }

    private void BreakImmediately()
    {
        _debrisPending = true;
        Finish();
    }

    internal bool TakeDebrisRequest()
    {
        bool pending = _debrisPending;
        _debrisPending = false;
        return pending;
    }
}

internal enum FlyingTileState
{
    Initializing,
    Rising,
    Waiting,
    Charging
}
