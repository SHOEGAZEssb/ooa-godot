using Godot;
using System;

namespace oracleofages;

public partial class GelCharacter : TransitionOffsetNode2D
{
    internal enum GelState
    {
        Waiting = 8,
        Inching = 9,
        Shaking = 10,
        Hopping = 11,
        Attached = 13
    }

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int AttachedFrames = 120;

    private EnemyDatabase.GelDefinition _definition;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyVerticalMotion _verticalMotion = null!;
    private EnemyAnimationPlayer _animation = null!;
    private GelState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _health;
    private bool _collisionEnabled;

    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public OracleRoomData.HazardType DeathHazard { get; private set; }
    public bool IsAttached => !IsDead && _state == GelState.Attached;
    public Rect2 CollisionBounds => new(
        Position - new Vector2(_definition.CollisionRadiusX, _definition.CollisionRadiusY),
        new Vector2(_definition.CollisionRadiusX * 2, _definition.CollisionRadiusY * 2));
    internal EnemyDatabase.GelDefinition Definition => _definition;
    internal GelState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _verticalMotion.ZFixed;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int CurrentAnimationFrame => _animation.FrameIndex;
    internal bool CollisionEnabled => _collisionEnabled;

    internal void Initialize(
        EnemyDatabase.GelDefinition definition,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        _definition = definition;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _verticalMotion = new EnemyVerticalMotion(this, Gravity);
        _animation = new EnemyAnimationPlayer(this, animationCount: 3);
        Position = position;
        _health = definition.Health;
        _state = GelState.Waiting;
        _counter1 = 0x10;
        _collisionEnabled = true;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{definition.SpriteName}.png");
        string[] encodedAnimations =
        {
            definition.NormalAnimation,
            definition.AttachedAnimation,
            definition.ShakeAnimation
        };
        _animation.Load(source, encodedAnimations, definition.TileBase, definition.Palette);
        SetAnimation(0);
        QueueRedraw();
    }

    internal void UpdateFrame(
        Vector2 linkPosition,
        Vector2I linkFacing,
        bool anyButtonJustPressed)
    {
        if (IsDead)
            return;

        if (_state == GelState.Attached)
        {
            Position = linkPosition;
            _counter2--;
            if (_counter2 <= 0)
            {
                BeginHop(AngleAwayFromFacing(linkFacing));
                return;
            }
            if (anyButtonJustPressed)
                _counter2 = Math.Max(1, _counter2 - 3);
            if ((_counter2 & 0x03) == 0)
                ZIndex = ZIndex <= 10 ? 11 : 9;
            _animation.Advance();
            QueueRedraw();
            return;
        }

        if (_verticalMotion.ZFixed == 0 && _collisionEnabled && _movement.IsOnHazard)
        {
            DeathHazard = _movement.Hazard;
            DiedInHazard = true;
            IsDead = true;
            Visible = false;
            return;
        }

        switch (_state)
        {
            case GelState.Waiting:
                _animation.Advance();
                if (--_counter1 > 0)
                    return;
                if ((_random.Next().Value & 0x07) == 0)
                {
                    _state = GelState.Shaking;
                    _counter1 = 0x30;
                    SetAnimation(2);
                }
                else
                {
                    _state = GelState.Inching;
                    _counter1 = 0x08;
                    _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                }
                return;

            case GelState.Inching:
                _movement.MoveAtAngle(_angle, 0.25f, allowHoles: false);
                _animation.Advance();
                if (--_counter1 > 0)
                    return;
                _state = GelState.Waiting;
                _counter1 = 0x10;
                return;

            case GelState.Shaking:
                _animation.Advance();
                if (--_counter1 > 0)
                    return;
                BeginHop(OracleObjectMath.AngleToward(Position, linkPosition));
                return;

            case GelState.Hopping:
                _movement.MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!_verticalMotion.Update())
                    return;
                _state = GelState.Waiting;
                _counter1 = 0x10;
                _collisionEnabled = true;
                SetAnimation(0);
                return;
        }
    }

    public bool OverlapsLink(Vector2 linkPosition)
    {
        return !IsDead && _collisionEnabled && !IsAttached &&
            Mathf.Abs(linkPosition.X - Position.X) < _definition.CollisionRadiusX + 6 &&
            Mathf.Abs(linkPosition.Y - Position.Y) < _definition.CollisionRadiusY + 6;
    }

    internal void AttachToLink(Vector2 linkPosition)
    {
        if (IsDead || IsAttached)
            return;
        Position = linkPosition;
        _state = GelState.Attached;
        _counter2 = AttachedFrames;
        _verticalMotion.Reset();
        _collisionEnabled = false;
        SetAnimation(1);
        ZIndex = 11;
    }

    public bool TakeSwordHit()
        => TakeSwordHit(2);

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || IsAttached)
            return false;
        _health = Math.Max(0, _health - Math.Max(1, damage));
        if (_health > 0)
            return true;
        IsDead = true;
        Visible = false;
        return true;
    }

    internal void SetStateForValidation(
        GelState state,
        int counter1 = 0,
        int counter2 = 0,
        int animation = -1)
    {
        _state = state;
        _counter1 = counter1;
        _counter2 = counter2;
        if (animation >= 0)
            SetAnimation(animation);
    }

    public override void _Draw()
    {
        if (IsDead || !_animation.HasFrames)
            return;
        DrawTexture(
            _animation.CurrentTexture,
            new Vector2(-16, -16 + (_verticalMotion.ZFixed >> 8)) +
                TransitionDrawOffset);
    }

    private void BeginHop(int angle)
    {
        _state = GelState.Hopping;
        _verticalMotion.SpeedZ = InitialSpeedZ;
        _angle = angle & 0x1f;
        // gel_beginHop does not alter collisionType. A Gel hopping normally
        // therefore stays enabled, while a Gel whose Link collision disabled
        // it stays disabled until state $0b restores bit 7 on landing.
        ZIndex = 10;
        SetAnimation(0);
    }

    private int AngleAwayFromFacing(Vector2I facing)
    {
        if (facing == Vector2I.Zero)
            return _random.Next().Value & 0x1f;
        int linkAngle = facing == Vector2I.Up ? 0x00
            : facing == Vector2I.Right ? 0x08
            : facing == Vector2I.Down ? 0x10
            : 0x18;
        return (linkAngle + 0x10) & 0x1f;
    }

    private void SetAnimation(int index)
    {
        _animation.SetAnimation(index);
    }
}
