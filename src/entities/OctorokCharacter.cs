using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class OctorokCharacter : Node2D
{
    internal enum OctorokState { Deciding = 8, Standing = 9, Walking = 10, Shooting = 11 }

    private sealed record AnimationFrame(Texture2D Texture, int Duration);

    private static readonly int[] Counter1Values = { 30, 45, 60, 75, 45, 60, 75, 90 };
    private static readonly int[] WalkCounterValues = { 0x19, 0x21, 0x29, 0x31 };

    private readonly List<AnimationFrame>[] _animations =
    {
        new(), new(), new(), new()
    };
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private OctorokState _state;
    private int _counter1;
    private int _walkCounter;
    private int _angle;
    private int _health;
    private int _invincibilityCounter;
    private int _knockbackCounter;
    private int _knockbackAngle;
    private int _animationDirection;
    private int _animationFrame;
    private int _animationCounter;
    private Vector2 _transitionDrawOffset;

    public EnemyDatabase.OctorokRecord Record { get; private set; }
    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public Rect2 CollisionBounds => new(
        Position - new Vector2(Record.CollisionRadiusX, Record.CollisionRadiusY),
        new Vector2(Record.CollisionRadiusX * 2, Record.CollisionRadiusY * 2));
    internal OctorokState State => _state;
    internal int Counter1 => _counter1;
    internal int WalkCounter => _walkCounter;
    internal int Angle => _angle;
    internal int Health => _health;
    internal int InvincibilityCounter => _invincibilityCounter;
    internal int KnockbackCounter => _knockbackCounter;
    internal int CurrentAnimationFrame => _animationFrame;
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(
        EnemyDatabase.OctorokRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
        Position = position;
        _health = record.Health;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        string[] encodedAnimations =
        {
            record.UpAnimation,
            record.RightAnimation,
            record.DownAnimation,
            record.LeftAnimation
        };
        for (int direction = 0; direction < encodedAnimations.Length; direction++)
        {
            _animations[direction].AddRange(BuildAnimation(
                source, encodedAnimations[direction], record.TileBase, record.Palette));
        }

        OracleRandom.Result initial = _random.Next();
        _counter1 = Counter1Values[initial.Value & record.CounterMask];
        _angle = initial.High & 0x18;
        _walkCounter = WalkCounterValues[initial.Low & 0x03];
        _state = OctorokState.Walking;
        SetAnimationFromAngle();
        QueueRedraw();
    }

    internal bool UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return false;

        UpdateInvincibility();
        if (_knockbackCounter > 0)
        {
            _knockbackCounter--;
            MoveAtAngle(_knockbackAngle, 2.0f, allowHazards: true);
            return false;
        }

        switch (_state)
        {
            case OctorokState.Deciding:
                DecideNextAction(linkPosition);
                break;
            case OctorokState.Standing:
                UpdateStanding(linkPosition);
                break;
            case OctorokState.Walking:
                UpdateWalking();
                break;
            case OctorokState.Shooting:
                return UpdateShooting();
        }
        return false;
    }

    public bool OverlapsLink(Vector2 linkPosition)
    {
        return !IsDead &&
            Mathf.Abs(linkPosition.X - Position.X) < Record.CollisionRadiusX + 6 &&
            Mathf.Abs(linkPosition.Y - Position.Y) < Record.CollisionRadiusY + 6;
    }

    public bool TakeSwordHit(Vector2 sourcePosition)
    {
        if (IsDead || _invincibilityCounter > 0)
            return false;

        _health = Math.Max(0, _health - 2);
        if (_health == 0)
        {
            IsDead = true;
            Visible = false;
            return true;
        }

        _invincibilityCounter = 0x10;
        _knockbackCounter = 0x08;
        _knockbackAngle = GetCardinalAngleAwayFrom(sourcePosition);
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
        OctorokState state,
        int counter1 = 0,
        int walkCounter = 0,
        int angle = 0)
    {
        _state = state;
        _counter1 = counter1;
        _walkCounter = walkCounter;
        _angle = angle & 0x18;
        SetAnimationFromAngle();
    }

    public override void _Draw()
    {
        List<AnimationFrame> animation = _animations[_animationDirection];
        if (IsDead || animation.Count == 0)
            return;
        DrawTexture(
            animation[_animationFrame % animation.Count].Texture,
            new Vector2(-16, -16) + _transitionDrawOffset);
    }

    private void DecideNextAction(Vector2 linkPosition)
    {
        int decision = _random.Next().Value & Record.CounterMask;
        if (decision == 0)
        {
            _state = OctorokState.Shooting;
            _counter1 = 0x10;
            if (Record.SubId >= 2)
            {
                _angle = GetCardinalAngleToward(linkPosition);
                SetAnimationFromAngle();
            }
            return;
        }

        _state = OctorokState.Standing;
        _counter1 = Counter1Values[decision];
    }

    private void UpdateStanding(Vector2 linkPosition)
    {
        _counter1--;
        if (_counter1 > 0)
            return;

        _state = OctorokState.Walking;
        OracleRandom.Result result = _random.Next();
        _walkCounter = WalkCounterValues[result.Value & 0x03];
        _angle = result.Low & 0x18;
        if ((result.High & 0x03) == 0)
            _angle = GetCardinalAngleToward(linkPosition);
        SetAnimationFromAngle();
    }

    private void UpdateWalking()
    {
        _walkCounter--;
        if (_walkCounter == 0)
        {
            _state = OctorokState.Deciding;
            return;
        }

        if (!MoveAtAngle(_angle, Record.SpeedRaw / 40.0f, allowHazards: false))
        {
            _angle = _random.Next().Value & 0x18;
            SetAnimationFromAngle();
        }
        AdvanceAnimation();
    }

    private bool UpdateShooting()
    {
        _counter1--;
        if (_counter1 > 0)
            return false;

        _counter1 = 0x20;
        _state = OctorokState.Standing;
        return true;
    }

    private bool MoveAtAngle(int angle, float speed, bool allowHazards)
    {
        Vector2 direction = OracleObjectMath.CardinalVector(angle);
        Vector2 destination = Position + direction * speed;
        if (!CanOccupy(destination, allowHazards))
            return false;
        Position = destination;
        if (allowHazards && _room.GetTerrainInfo(Position).Hazard != OracleRoomData.HazardType.None)
        {
            DiedInHazard = true;
            IsDead = true;
            Visible = false;
        }
        QueueRedraw();
        return true;
    }

    private bool CanOccupy(Vector2 center, bool allowHazards)
    {
        float radiusX = Math.Max(1, Record.CollisionRadiusX - 1);
        float radiusY = Math.Max(1, Record.CollisionRadiusY - 1);
        Vector2[] samples =
        {
            center + new Vector2(-radiusX, -radiusY),
            center + new Vector2(radiusX, -radiusY),
            center + new Vector2(-radiusX, radiusY),
            center + new Vector2(radiusX, radiusY)
        };
        foreach (Vector2 sample in samples)
        {
            if (sample.X < 0 || sample.X >= _room.Width ||
                sample.Y < 0 || sample.Y >= _room.Height || _room.IsSolid(sample))
                return false;
            if (!allowHazards && _room.GetTerrainInfo(sample).Hazard is
                OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Hole)
                return false;
        }
        return true;
    }

    private void UpdateInvincibility()
    {
        if (_invincibilityCounter <= 0)
            return;
        _invincibilityCounter--;
        Visible = _invincibilityCounter == 0 || (_invincibilityCounter & 1) == 0;
        QueueRedraw();
    }

    private int GetCardinalAngleAwayFrom(Vector2 source) =>
        (GetCardinalAngleToward(source) + 0x10) & 0x1f;

    private int GetCardinalAngleToward(Vector2 target)
    {
        Vector2 difference = target - Position;
        if (Mathf.Abs(difference.X) >= Mathf.Abs(difference.Y))
            return difference.X >= 0 ? 0x08 : 0x18;
        return difference.Y >= 0 ? 0x10 : 0x00;
    }

    private void SetAnimationFromAngle()
    {
        _animationDirection = (_angle & 0x18) >> 3;
        _animationFrame = 0;
        List<AnimationFrame> animation = _animations[_animationDirection];
        _animationCounter = animation.Count > 0 ? animation[0].Duration : 1;
        QueueRedraw();
    }

    private void AdvanceAnimation()
    {
        List<AnimationFrame> animation = _animations[_animationDirection];
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
        foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation).Frames)
        {
            yield return new AnimationFrame(
                NpcCharacter.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palette),
                frame.Duration);
        }
    }
}
