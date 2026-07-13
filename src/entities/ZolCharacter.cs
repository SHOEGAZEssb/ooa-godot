using Godot;
using System;
using System.Collections.Generic;

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

    private sealed record AnimationFrame(Texture2D Texture, int Duration, int Parameter);

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int WakeDistance = 0x28;

    private readonly List<AnimationFrame>[] _animations =
    {
        new(), new(), new(), new(), new(), new()
    };
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private ZolState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _zFixed;
    private int _speedZ;
    private int _health;
    private int _animationIndex;
    private int _animationFrame;
    private int _animationCounter;
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
    internal int ZFixed => _zFixed;
    internal int Health => _health;
    internal bool CollisionEnabled => _collisionEnabled;
    internal int AnimationIndex => _animationIndex;
    internal int CurrentAnimationFrame => _animationFrame;
    internal int AnimationParameter => CurrentFrame.Parameter;
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    private AnimationFrame CurrentFrame =>
        _animations[_animationIndex][_animationFrame];

    internal void Initialize(
        EnemyDatabase.ZolRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        Position = position;
        _health = record.Health;

        byte[] bytes = FileAccess.GetFileAsBytes(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        Image source = new();
        Error error = source.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load Zol graphics: {error}.");
        string[] encodedAnimations =
        {
            record.EmergeAnimation,
            record.WaitAnimation,
            record.HopAnimation,
            record.DisappearAnimation,
            record.RedIdleAnimation,
            record.RedShakeAnimation
        };
        for (int index = 0; index < encodedAnimations.Length; index++)
        {
            _animations[index].AddRange(BuildAnimation(
                source, encodedAnimations[index], record.TileBase, record.Palette));
        }

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

        if (_zFixed == 0 && _collisionEnabled &&
            _room.GetTerrainInfo(Position).Hazard != OracleRoomData.HazardType.None)
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
                _speedZ = InitialSpeedZ;
                _counter2 = 4;
                _state = ZolState.GreenEmerging;
                Visible = true;
                SetAnimation(0);
                return UpdateEvent.None;

            case ZolState.GreenEmerging:
                if (CurrentFrame.Parameter == 0)
                {
                    AdvanceAnimation();
                    return UpdateEvent.None;
                }
                if (!UpdateZ())
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
                _speedZ = InitialSpeedZ;
                _angle = GetAngleToward(linkPosition);
                SetAnimation(2);
                return UpdateEvent.None;

            case ZolState.GreenHopping:
                MoveAtAngle(_angle, 0.75f, allowHoles: true);
                if (!UpdateZ())
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
                if (CurrentFrame.Parameter == 0)
                {
                    AdvanceAnimation();
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
                AdvanceAnimation();
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
                    _angle = GetAngleToward(linkPosition);
                }
                return UpdateEvent.None;

            case ZolState.RedSliding:
                MoveAtAngle(_angle, 0.5f, allowHoles: false);
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
                _speedZ = InitialSpeedZ;
                _angle = GetAngleToward(linkPosition);
                SetAnimation(2);
                return UpdateEvent.None;

            case ZolState.RedHopping:
                MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!UpdateZ())
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
        if (IsDead || !Visible || _animations[_animationIndex].Count == 0)
            return;
        DrawTexture(
            CurrentFrame.Texture,
            new Vector2(-16, -16 + (_zFixed >> 8)) + _transitionDrawOffset);
    }

    private bool UpdateZ()
    {
        _zFixed += _speedZ;
        if (_zFixed < 0)
        {
            _speedZ += Gravity;
            QueueRedraw();
            return false;
        }
        _zFixed = 0;
        _speedZ = 0;
        QueueRedraw();
        return true;
    }

    private void MoveAtAngle(int angle, float speed, bool allowHoles)
    {
        float radians = angle * Mathf.Tau / 32.0f;
        Vector2 movement = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * speed;
        Vector2 destination = Position + movement;
        if (CanOccupy(destination, allowHoles))
        {
            Position = destination;
        }
        else if (CanOccupy(Position + new Vector2(movement.X, 0), allowHoles))
        {
            Position += new Vector2(movement.X, 0);
        }
        else if (CanOccupy(Position + new Vector2(0, movement.Y), allowHoles))
        {
            Position += new Vector2(0, movement.Y);
        }
        QueueRedraw();
    }

    private bool CanOccupy(Vector2 center, bool allowHoles)
    {
        Vector2[] samples =
        {
            center + new Vector2(-5, -4), center + new Vector2(5, -4),
            center + new Vector2(-5, 6), center + new Vector2(5, 6)
        };
        foreach (Vector2 sample in samples)
        {
            if (sample.X < 0 || sample.X >= _room.Width ||
                sample.Y < 0 || sample.Y >= _room.Height || _room.IsSolid(sample))
                return false;
            if (!allowHoles && _room.GetTerrainInfo(sample).Hazard ==
                OracleRoomData.HazardType.Hole)
                return false;
        }
        return true;
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

    private int GetAngleToward(Vector2 target)
    {
        Vector2 difference = target - Position;
        float radians = Mathf.Atan2(difference.X, -difference.Y);
        return Mathf.PosMod(Mathf.RoundToInt(radians * 32.0f / Mathf.Tau), 32);
    }

    private void SetAnimation(int index)
    {
        _animationIndex = index;
        _animationFrame = 0;
        _animationCounter = _animations[index].Count > 0
            ? _animations[index][0].Duration
            : 1;
        QueueRedraw();
    }

    private void AdvanceAnimation()
    {
        List<AnimationFrame> animation = _animations[_animationIndex];
        if (animation.Count <= 1)
            return;
        _animationCounter--;
        if (_animationCounter > 0)
            return;
        if (_animationFrame < animation.Count - 1)
            _animationFrame++;
        else
            _animationFrame = 0;
        _animationCounter = animation[_animationFrame].Duration;
        QueueRedraw();
    }

    private static int ManhattanDistance(Vector2 first, Vector2 second) =>
        Mathf.Abs(Mathf.FloorToInt(first.X) - Mathf.FloorToInt(second.X)) +
        Mathf.Abs(Mathf.FloorToInt(first.Y) - Mathf.FloorToInt(second.Y));

    private static IEnumerable<AnimationFrame> BuildAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int palette)
    {
        foreach (string encodedFrame in encodedAnimation.Split(
            '|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            int comma = encodedFrame.IndexOf(',');
            if (separator < 0 || comma < 0 || comma > separator ||
                !int.TryParse(encodedFrame[..comma], out int duration) ||
                !int.TryParse(encodedFrame[(comma + 1)..separator], out int parameter))
                continue;
            yield return new AnimationFrame(
                NpcCharacter.BuildOamTexture(
                    source, encodedFrame[(separator + 1)..], tileBase, palette),
                Math.Max(1, duration),
                parameter);
        }
    }
}
