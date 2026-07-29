using Godot;
using System;

namespace oracleofages;

internal partial class GhiniCharacter : EnemyCharacter
{
    private readonly GhiniBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Ghini;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private int _counter;
    private int _angle;
    private GhiniState _state;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal GhiniState State => _state;

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
        _room = room;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.ScreenBoundary);
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if (_state == GhiniState.Uninitialized)
        {
            _state = GhiniState.Choosing;
            return;
        }
        if (_state == GhiniState.Choosing)
        {
            ChooseDirection();
            _state = GhiniState.Moving;
            return;
        }
        Position += OracleObjectMovement.Shared.Delta(
            _behavior.SpeedRaw, _angle);
        bool horizontal = Position.X < Record.RadiusX ||
            Position.X >= _room.Width - Record.RadiusX;
        bool vertical = Position.Y < Record.RadiusY ||
            Position.Y >= _room.Height - Record.RadiusY;
        Position = new Vector2(
            Mathf.Clamp(
                Position.X, Record.RadiusX, _room.Width - Record.RadiusX - 1),
            Mathf.Clamp(
                Position.Y, Record.RadiusY, _room.Height - Record.RadiusY - 1));
        if (horizontal)
            _angle = (0x20 - _angle) & 0x1f;
        if (vertical)
            _angle = (0x10 - _angle) & 0x1f;
        if (horizontal || vertical)
            SetAnimation(_angle < 0x10 ? 1 : 0);
        _counter--;
        if (_counter == 0)
            _state = GhiniState.Choosing;
        AdvanceAnimation();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsDead || !Visible)
            return;
        DrawSetTransform(Vector2.Up * 2.0f);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void ChooseDirection()
    {
        OracleRandomResult result = _random.Next();
        _counter = _behavior.MoveCounterBase +
            (result.Low & _behavior.MoveCounterMask);
        _angle = result.High & 0x18;
        SetAnimation(_angle < 0x10 ? 1 : 0);
    }
}

internal enum GhiniState
{
    Uninitialized,
    Choosing,
    Moving
}
