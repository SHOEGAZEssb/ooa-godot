using Godot;
using System;

namespace oracleofages;

public partial class ZolCharacter : EnemyCharacter
{

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int WakeDistance = 0x28;

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private EnemyVerticalMotion _verticalMotion = null!;
    private ZolState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private bool _collisionEnabled;

    public ZolRecord Record { get; private set; }
    internal ZolState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _verticalMotion.ZFixed;
    internal override bool CollisionEnabled =>
        _collisionEnabled && base.CollisionEnabled;
    internal int CurrentAnimationFrame => AnimationFrame;
    protected override bool DrawsAnimation => !IsDead && Visible;
    protected override Vector2 AnimationDrawOffset =>
        new(-16, -16 + (_verticalMotion.ZFixed >> 8));

    internal void Initialize(
        ZolRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _verticalMotion = new EnemyVerticalMotion(this, Gravity);

        string[] encodedAnimations =
        {
            record.EmergeAnimation,
            record.WaitAnimation,
            record.HopAnimation,
            record.DisappearAnimation,
            record.RedIdleAnimation,
            record.RedShakeAnimation
        };
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                encodedAnimations,
                record.TileBase,
                record.Palette));
        ConfigureHazards(
            room,
            animateWhileFallingInHole: false,
            zPosition: () => _verticalMotion.ZFixed);

        if (record.SubId == 0)
        {
            _state = ZolState.GreenHidden;
            _collisionEnabled = false;
            Visible = false;
            RestartAnimation(0);
        }
        else
        {
            _state = ZolState.RedWaiting;
            _counter1 = 0x18;
            _collisionEnabled = true;
            Visible = true;
            RestartAnimation(4);
        }
        QueueRedraw();
    }

    internal UpdateEvent UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return UpdateEvent.None;
        if (BeginFrame())
            return UpdateEvent.None;
        if (CheckHazards())
            return UpdateEvent.None;

        switch (_state)
        {
            case ZolState.GreenHidden:
                if (ManhattanDistance(Position, linkPosition) >= WakeDistance)
                    return UpdateEvent.None;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _counter2 = 4;
                _state = ZolState.GreenEmerging;
                Visible = true;
                RestartAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenEmerging:
                if (AnimationParameter == 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.GreenWaiting;
                _counter1 = 0x30;
                _collisionEnabled = true;
                RestartAnimation(1);
                return UpdateEvent.None;

            case ZolState.GreenWaiting:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHopping;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                RestartAnimation(2);
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
                    RestartAnimation(1);
                }
                else
                {
                    _state = ZolState.GreenDisappearing;
                    _collisionEnabled = false;
                    RestartAnimation(3);
                }
                return UpdateEvent.None;

            case ZolState.GreenDisappearing:
                if (AnimationParameter == 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                _state = ZolState.GreenGone;
                _counter1 = 40;
                Visible = false;
                RestartAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenGone:
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.GreenHidden;
                return UpdateEvent.None;

            case ZolState.RedWaiting:
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                if ((_random.Next().Value & 0x07) == 0)
                {
                    _state = ZolState.RedShaking;
                    _counter1 = 0x20;
                    RestartAnimation(5);
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
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = 0x18;
                return UpdateEvent.None;

            case ZolState.RedShaking:
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return UpdateEvent.None;
                _state = ZolState.RedHopping;
                _verticalMotion.SpeedZ = InitialSpeedZ;
                _angle = OracleObjectMath.AngleToward(Position, linkPosition);
                RestartAnimation(2);
                return UpdateEvent.None;

            case ZolState.RedHopping:
                _movement.MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!_verticalMotion.Update())
                    return UpdateEvent.None;
                _state = ZolState.RedWaiting;
                _counter1 = 0x18;
                RestartAnimation(4);
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
                Finish();
                return UpdateEvent.SpawnGels;
        }

        return UpdateEvent.None;
    }

    public bool TakeSwordHit()
        => TakeSwordHit(2);

    internal bool TakeSwordHit(int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0 ||
            _state is
            ZolState.RedSplitting or ZolState.RedSplitDelay)
            return false;

        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Record.SubId == 1)
        {
            _state = ZolState.RedSplitting;
            return true;
        }

        if (Health > 0)
            return true;
        Finish();
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (IsDead || !CollisionEnabled || _state is
            ZolState.RedSplitting or ZolState.RedSplitDelay)
        {
            return false;
        }
        Health = Math.Max(0, Health - Math.Max(1, damage));
        if (Health == 0)
            Finish();
        return true;
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
            RestartAnimation(animation);
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

    private static int ManhattanDistance(Vector2 first, Vector2 second) =>
        Mathf.Abs(Mathf.FloorToInt(first.X) - Mathf.FloorToInt(second.X)) +
        Mathf.Abs(Mathf.FloorToInt(first.Y) - Mathf.FloorToInt(second.Y));
}

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

internal enum UpdateEvent
{
    None,
    BeginSplit,
    SpawnGels
}
