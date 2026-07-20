using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared ENEMY_MASKED_MOBLIN $20:$00 walk/turn/arrow state machine. Room
/// $1:$38 creates two of these after the scripted interaction actors finish.
/// </summary>
public partial class MaskedMoblinCharacter : Node2D
{
    internal enum MoblinState { Uninitialized, Moving, Turning }

    private EnemyDatabase.MaskedMoblinRecord _record;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyAnimationPlayer _animation = null!;
    private MoblinState _state;
    private int _counter;
    private int _angle;
    private int _moveCycles;
    private int _health;
    private Vector2 _transitionDrawOffset;

    public EnemyDatabase.MaskedMoblinRecord Record => _record;
    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public OracleRoomData.HazardType DeathHazard { get; private set; }
    public Rect2 CollisionBounds => new(
        Position - new Vector2(_record.CollisionRadiusX, _record.CollisionRadiusY),
        new Vector2(_record.CollisionRadiusX * 2, _record.CollisionRadiusY * 2));
    internal MoblinState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int Health => _health;

    internal void Initialize(
        EnemyDatabase.MaskedMoblinRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        _record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _animation = new EnemyAnimationPlayer(this, 4);
        Position = position;
        _health = record.Health;
        _state = MoblinState.Uninitialized;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        _animation.Load(source,
            [record.UpAnimation, record.RightAnimation,
             record.DownAnimation, record.LeftAnimation],
            record.TileBase, record.Palette);
        _animation.SetAnimation(0);
        QueueRedraw();
    }

    /// <returns>The cardinal angle of an arrow to create, or -1.</returns>
    internal int UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return -1;
        if (_movement.IsOnHazard)
        {
            DeathHazard = _movement.Hazard;
            DiedInHazard = true;
            IsDead = true;
            Visible = false;
            return -1;
        }

        switch (_state)
        {
            case MoblinState.Uninitialized:
                _angle = _random.Next().Value & 0x18;
                BeginMoving();
                Visible = true;
                return -1;
            case MoblinState.Moving:
                _counter--;
                bool moved = _movement.MoveAtAngle(
                    _angle, _record.SpeedRaw / 40.0f, allowHoles: false);
                _animation.Advance();
                if (_counter == 0 || !moved)
                {
                    _state = MoblinState.Turning;
                    _counter = _record.TurnWait;
                }
                return -1;
            case MoblinState.Turning:
                if (--_counter != 0)
                    return -1;
                _angle = _random.Next().Value & 0x18;
                BeginMoving();
                _moveCycles++;
                int towardLink = (OracleObjectMath.AngleToward(
                    Position, linkPosition) + 4) & 0x18;
                return (_moveCycles & 1) != 0 && _angle == towardLink
                    ? _angle
                    : -1;
            default:
                throw new InvalidOperationException($"Unknown masked Moblin state {_state}.");
        }
    }

    public bool OverlapsLink(Vector2 linkPosition) =>
        !IsDead &&
        Mathf.Abs(linkPosition.X - Position.X) < _record.CollisionRadiusX + 6 &&
        Mathf.Abs(linkPosition.Y - Position.Y) < _record.CollisionRadiusY + 6;

    public bool TakeSwordHit(Vector2 _)
    {
        if (IsDead)
            return false;
        _health = Math.Max(0, _health - 2);
        if (_health == 0)
        {
            IsDead = true;
            Visible = false;
        }
        return true;
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsDead && _animation.HasFrames)
            DrawTexture(_animation.CurrentTexture,
                new Vector2(-16, -16) + _transitionDrawOffset);
    }

    private void BeginMoving()
    {
        _counter = _record.MoveCounterBase +
            (_random.Next().Value & _record.MoveCounterMask);
        _state = MoblinState.Moving;
        _animation.SetAnimation((_angle & 0x18) / 8);
    }
}
