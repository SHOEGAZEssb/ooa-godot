using Godot;
using System;

namespace oracleofages;

internal partial class PeahatCharacter : EnemyCharacter
{
    private static readonly int[] SpeedValues =
    [
        0x1e, 0x1e, 0x1e, 0x14, 0x14, 0x0a, 0x0a, 0x05, 0x05
    ];

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private PeahatState _state;
    private int _counter;
    private int _angle;
    private int _zHigh;
    private int _speedRaw;

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
        _movement = new EnemyTerrainMovement(this, room);
        _state = PeahatState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureHazards(room, zPosition: () => _zHigh);
    }

    internal void UpdateFrame()
    {
        if (IsDead || BeginFrame())
            return;
        if (CheckHazards())
            return;

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
                _counter = WingDungeonEnemyBehavior.Shared[
                    "peahat-acceleration-frames"];
                _speedRaw = WingDungeonEnemyBehavior.Shared[
                    "peahat-initial-speed-raw"];
                AdvanceAnimation();
                return;

            case PeahatState.Accelerating:
                _counter = (_counter - 1) & 0xff;
                if (_counter == 0)
                {
                    _state = PeahatState.Flying;
                    int index = _random.Next().Value & 7;
                    _counter = WingDungeonEnemyBehavior.Shared[
                        $"peahat-flight-counter-{index}"];
                    _angle = _random.Next().Value & 0x1f;
                    AdvanceAnimation();
                    return;
                }
                UpdateAccelerationPosition();
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
                if (_counter == WingDungeonEnemyBehavior.Shared[
                    "peahat-slowdown-frames"])
                {
                    _state = PeahatState.Stationary;
                    _counter = _room.IsSolid(Position) ? 1 : 0x80;
                    _zHigh = 0;
                    AdvanceAnimation();
                    return;
                }
                UpdateAccelerationPosition();
                return;
        }
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (_zHigh != 0)
            return false;
        return base.TakeSwordHit(sourcePosition, damage);
    }

    internal override bool TakeBurnHit(int damage) =>
        _zHigh == 0 && base.TakeBurnHit(damage);

    private void UpdateAccelerationPosition()
    {
        int value = (_counter - 1) & 0xff;
        if (value < 0x41)
        {
            int index = (value & 0x78) >> 3;
            _zHigh = -Math.Max(0, index - 6);
            _speedRaw = SpeedValues[index];
            MoveBouncing();
        }
        AdvanceAnimation();
    }

    private void MoveBouncing()
    {
        if (!_movement.MoveAtAngle(_angle, _speedRaw, allowHoles: true))
            _angle = (_angle + 0x10) & 0x1f;
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
