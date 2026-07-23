using Godot;
using System;

namespace oracleofages;

internal partial class GhiniCharacter : SpiritsGraveEnemyCharacter
{
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private int _counter;
    private int _angle;
    private GhiniState _state;

    internal int Counter => _counter;
    internal int Angle => _angle;
    internal GhiniState State => _state;

    internal void Initialize(
        EnemyRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        InitializeEnemy(record, position);
        _random = random;
        _room = room;
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        BeginFrame();
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
        Position += OracleObjectMath.VectorFromAngle32(_angle) * 0.5f;
        bool horizontal = Position.X < 6 || Position.X >= _room.Width - 6;
        bool vertical = Position.Y < 6 || Position.Y >= _room.Height - 6;
        Position = new Vector2(
            Mathf.Clamp(Position.X, 6, _room.Width - 7),
            Mathf.Clamp(Position.Y, 6, _room.Height - 7));
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
        if (!CollisionEnabled)
            return;
        DrawSetTransform(Vector2.Up * 2.0f);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    private void ChooseDirection()
    {
        OracleRandomResult result = _random.Next();
        _counter = 0x30 + (result.Low & 0x7f);
        _angle = result.High & 0x18;
        SetAnimation(_angle < 0x10 ? 1 : 0);
    }
}
