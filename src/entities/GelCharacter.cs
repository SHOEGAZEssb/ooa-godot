using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class GelCharacter : Node2D
{
    internal enum GelState
    {
        Waiting = 8,
        Inching = 9,
        Shaking = 10,
        Hopping = 11,
        Attached = 13
    }

    private sealed record AnimationFrame(Texture2D Texture, int Duration);

    private const int InitialSpeedZ = -0x200;
    private const int Gravity = 0x28;
    private const int AttachedFrames = 120;

    private readonly List<AnimationFrame>[] _animations = { new(), new(), new() };
    private EnemyDatabase.GelDefinition _definition;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private GelState _state;
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

    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public bool IsAttached => !IsDead && _state == GelState.Attached;
    public Rect2 CollisionBounds => new(
        Position - new Vector2(_definition.CollisionRadiusX, _definition.CollisionRadiusY),
        new Vector2(_definition.CollisionRadiusX * 2, _definition.CollisionRadiusY * 2));
    internal EnemyDatabase.GelDefinition Definition => _definition;
    internal GelState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _zFixed;
    internal int AnimationIndex => _animationIndex;
    internal int CurrentAnimationFrame => _animationFrame;
    internal bool CollisionEnabled => _collisionEnabled;
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(
        EnemyDatabase.GelDefinition definition,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        _definition = definition;
        _room = room;
        _random = random;
        Position = position;
        _health = definition.Health;
        _state = GelState.Waiting;
        _counter1 = 0x10;
        _collisionEnabled = true;

        byte[] bytes = FileAccess.GetFileAsBytes(
            $"res://assets/oracle/gfx/{definition.SpriteName}.png");
        Image source = new();
        Error error = source.LoadPngFromBuffer(bytes);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load Gel graphics: {error}.");
        string[] encodedAnimations =
        {
            definition.NormalAnimation,
            definition.AttachedAnimation,
            definition.ShakeAnimation
        };
        for (int index = 0; index < encodedAnimations.Length; index++)
        {
            _animations[index].AddRange(BuildAnimation(
                source, encodedAnimations[index], definition.TileBase, definition.Palette));
        }
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
            AdvanceAnimation();
            QueueRedraw();
            return;
        }

        if (_zFixed == 0 && _collisionEnabled &&
            _room.GetTerrainInfo(Position).Hazard != OracleRoomData.HazardType.None)
        {
            DiedInHazard = true;
            IsDead = true;
            Visible = false;
            return;
        }

        switch (_state)
        {
            case GelState.Waiting:
                AdvanceAnimation();
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
                MoveAtAngle(_angle, 0.25f, allowHoles: false);
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return;
                _state = GelState.Waiting;
                _counter1 = 0x10;
                return;

            case GelState.Shaking:
                AdvanceAnimation();
                if (--_counter1 > 0)
                    return;
                BeginHop(OracleObjectMath.AngleToward(Position, linkPosition));
                return;

            case GelState.Hopping:
                MoveAtAngle(_angle, 1.0f, allowHoles: true);
                if (!UpdateZ())
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
        _zFixed = 0;
        _speedZ = 0;
        _collisionEnabled = false;
        SetAnimation(1);
        ZIndex = 11;
    }

    public bool TakeSwordHit()
    {
        if (IsDead || IsAttached)
            return false;
        _health = Math.Max(0, _health - 2);
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
        List<AnimationFrame> animation = _animations[_animationIndex];
        if (IsDead || animation.Count == 0)
            return;
        DrawTexture(
            animation[_animationFrame].Texture,
            new Vector2(-16, -16 + (_zFixed >> 8)) + _transitionDrawOffset);
    }

    private void BeginHop(int angle)
    {
        _state = GelState.Hopping;
        _speedZ = InitialSpeedZ;
        _angle = angle & 0x1f;
        // gel_beginHop does not alter collisionType. A Gel hopping normally
        // therefore stays enabled, while a Gel whose Link collision disabled
        // it stays disabled until state $0b restores bit 7 on landing.
        ZIndex = 10;
        SetAnimation(0);
    }

    private bool UpdateZ()
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, Gravity);
        if (landed)
            _speedZ = 0;
        QueueRedraw();
        return landed;
    }

    private void MoveAtAngle(int angle, float speed, bool allowHoles)
    {
        Vector2 movement = OracleObjectMath.VectorFromAngle32(angle) * speed;
        Vector2 destination = Position + movement;
        if (CanOccupy(destination, allowHoles))
            Position = destination;
        else if (CanOccupy(Position + new Vector2(movement.X, 0), allowHoles))
            Position += new Vector2(movement.X, 0);
        else if (CanOccupy(Position + new Vector2(0, movement.Y), allowHoles))
            Position += new Vector2(0, movement.Y);
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
        _animationFrame = (_animationFrame + 1) % animation.Count;
        _animationCounter = animation[_animationFrame].Duration;
        QueueRedraw();
    }

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
                !int.TryParse(encodedFrame[..comma], out int duration))
                continue;
            yield return new AnimationFrame(
                NpcCharacter.BuildOamTexture(
                    source, encodedFrame[(separator + 1)..], tileBase, palette),
                Math.Max(1, duration));
        }
    }
}
