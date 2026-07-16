using Godot;
using System;

namespace oracleofages;

public partial class ZolCharacter : Node2D
{
    internal enum ZolState
    {
        GreenHidden = 8,
        GreenEmerging = 9,
        GreenWaiting = 10,
        GreenHopping = 11,
        GreenDisappearing = 12,
        GreenGone = 13,
        RedWaiting = 16,
        RedSliding = 17,
        RedShaking = 18,
        RedHopping = 19,
        RedSplitting = 20,
        RedSplitDelay = 21
    }

    internal enum UpdateEvent { None, BeginSplit, SpawnGels }

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int WakeDistance = 0x28;

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyVerticalMotion _verticalMotion = null!;
    private EnemyAnimationPlayer _animation = null!;
    private ZolState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _health;
    private bool _collisionEnabled;
    private Vector2 _transitionDrawOffset;

    public EnemyDatabase.ZolRecord Record { get; private set; }
    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public Rect2 CollisionBounds => new(
        Position - new Vector2(Record.CollisionRadiusX, Record.CollisionRadiusY),
        new Vector2(Record.CollisionRadiusX * 2, Record.CollisionRadiusY * 2));
    internal ZolState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _verticalMotion.ZFixed;
    internal int Health => _health;
    internal bool CollisionEnabled => _collisionEnabled;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int CurrentAnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(
        EnemyDatabase.ZolRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _verticalMotion = new EnemyVerticalMotion(this, Gravity);
        _animation = new EnemyAnimationPlayer(this, animationCount: 6);
        Position = position;
        _health = record.Health;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        string[] encodedAnimations =
        {
            record.EmergeAnimation,
            record.WaitAnimation,
            record.HopAnimation,
            record.DisappearAnimation,
            record.RedIdleAnimation,
            record.RedShakeAnimation
        };
        _animation.Load(source, encodedAnimations, record.TileBase, record.Palette);

        if (record.SubId == 0)
        {
            _state = ZolState.GreenHidden;
            _collisionEnabled = false;
            Visible = false;
            SetAnimation(0);
        }
        else
        {
            _state = ZolState.RedWaiting;
            _counter1 = 0x18;
            _collisionEnabled = true;
            Visible = true;
            SetAnimation(4);
        }
        QueueRedraw();
    }

    internal UpdateEvent UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return UpdateEvent.None;

        if (_verticalMotion.ZFixed == 0 && _collisionEnabled && _movement.IsOnHazard)
        {
            DiedInHazard = true;
            IsDead = true;
            Visible = false;
            return UpdateEvent.None;
        }

        switch (_state)
        {
            case ZolState.GreenHidden:
                if (ManhattanDistance(Position, linkPosition) >= WakeDistance)
                    return UpdateEvent.None;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _counter2 = 4;
                _state = ZolState.GreenEmerging;
                Visible = true;
                SetAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenEmerging:
                if (_animation.CurrentParameter == 0)
                {
                    _animation.Advance();
                    return UpdateEvent.None;
                }
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.GreenWaiting;
                _counter1 = 0x30;
                _collisionEnabled = true;
                SetAnimation(1);
                return UpdateEvent.None;

            case ZolState.GreenWaiting:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHopping;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                SetAnimation(2);
                return UpdateEvent.None;

            case ZolState.GreenHopping:
                _movement.MoveAtAngle(_angle, 0.75f, allowHoles: true);
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _counter1 = 0x30;
                _counter2--;
                if (_counter2 > 0)
                {
                    _state = ZolState.GreenWaiting;
                    SetAnimation(1);
                }
                else
                {
                    _state = ZolState.GreenDisappearing;
                    _collisionEnabled = false;
                    SetAnimation(3);
                }
                return UpdateEvent.None;

            case ZolState.GreenDisappearing:
                if (_animation.CurrentParameter == 0)
                {
                    _animation.Advance();
                    return UpdateEvent.None;
                }
                _state = ZolState.GreenGone;
                _counter1 = 40;
                Visible = false;
                SetAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenGone:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHidden;
                return UpdateEvent.None;

            case ZolState.RedWaiting:
                _animation.Advance();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                if ((_random.Next().Value & 0x07) == 0)
                {
                    _state = ZolState.RedShaking;
                    _counter1 = 0x20;
                    SetAnimation(5);
                }
                else
                {
                    _state = ZolState.RedSliding;
                    _counter1 = 0x10;
                    _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                }
                return UpdateEvent.None;

            case ZolState.RedSliding:
                _movement.MoveAtAngle(_angle, 0.5f, allowHoles: false);
                BounceOffScreenBoundary();
                _animation.Advance();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = 0x18;
                return UpdateEvent.None;

            case ZolState.RedShaking:
                _animation.Advance();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.RedHopping;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                SetAnimation(2);
                return UpdateEvent.None;

            case ZolState.RedHopping:
                _movement.MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = 0x18;
                SetAnimation(4);
                return UpdateEvent.None;

            case ZolState.RedSplitting:
                _state = ZolState.RedSplitDelay;
                _counter2 = 18;
                _collisionEnabled = false;
                Visible = false;
                return UpdateEvent.BeginSplit;

            case ZolState.RedSplitDelay:
                if (--_counter2 > 0)
                    return UpdateEvent.None;
                IsDead = true;
                return UpdateEvent.SpawnGels;
        }

        return UpdateEvent.None;
    }

    public bool OverlapsLink(Vector2 linkPosition)
    {
        return !IsDead && _collisionEnabled &&
            Mathf.Abs(linkPosition.X - Position.X) < Record.CollisionRadiusX + 6 &&
            Mathf.Abs(linkPosition.Y - Position.Y) < Record.CollisionRadiusY + 6;
    }

    public bool TakeSwordHit()
    {
        if (IsDead || !_collisionEnabled || _state is
            ZolState.RedSplitting or ZolState.RedSplitDelay)
            return false;

        _health = Math.Max(0, _health - 2);
        if (Record.SubId == 1)
        {
            _state = ZolState.RedSplitting;
            return true;
        }

        if (_health > 0)
            return true;
        IsDead = true;
        Visible = false;
        return true;
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    internal void SetStateForValidation(
        ZolState state,
        int counter1 = 0,
        int counter2 = 0,
        int animation = -1,
        bool collisionEnabled = true)
    {
        _state = state;
        _counter1 = counter1;
        _counter2 = counter2;
        _collisionEnabled = collisionEnabled;
        Visible = true;
        if (animation >= 0)
            SetAnimation(animation);
    }

    public override void _Draw()
    {
        if (IsDead || !Visible || !_animation.HasFrames)
            return;
        DrawTexture(
            _animation.CurrentTexture,
            new Vector2(-16, -16 + (_verticalMotion.ZFixed >> 8)) +
                _transitionDrawOffset);
    }

    private void BounceOffScreenBoundary()
    {
        bool hitHorizontal = Position.X <= 5 || Position.X >= _room.Width - 6;
        bool hitVertical = Position.Y <= 4 || Position.Y >= _room.Height - 7;
        if (hitHorizontal)
            _angle = (0x20 - _angle) & 0x1f;
        if (hitVertical)
            _angle = (0x10 - _angle) & 0x1f;
    }

    private void SetAnimation(int index)
    {
        _animation.SetAnimation(index);
    }

    private static int ManhattanDistance(Vector2 first, Vector2 second) =>
        Mathf.Abs(Mathf.FloorToInt(first.X) - Mathf.FloorToInt(second.X)) +
        Mathf.Abs(Mathf.FloorToInt(first.Y) - Mathf.FloorToInt(second.Y));
}
