using Godot;
using System;

namespace oracleofages;

public partial class OctorokCharacter : TransitionOffsetNode2D
{

    private static readonly int[] Counter1Values = { 30, 45, 60, 75, 45, 60, 75, 90 };
    private static readonly int[] WalkCounterValues = { 0x19, 0x21, 0x29, 0x31 };

    private EnemyAnimationPlayer _animation = null!;
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

    public OctorokRecord Record { get; private set; }
    public bool IsDead { get; private set; }
    public bool DiedInHazard { get; private set; }
    public HazardType DeathHazard { get; private set; }
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
    internal int CurrentAnimationFrame => _animation.FrameIndex;

    internal void Initialize(
        OctorokRecord record,
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
        _animation = new EnemyAnimationPlayer(this, encodedAnimations.Length);
        _animation.Load(source, encodedAnimations, record.TileBase, record.Palette);

        OracleRandomResult initial = _random.Next();
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
        => TakeSwordHit(sourcePosition, 2);

    internal bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (IsDead || _invincibilityCounter > 0)
            return false;

        if (!TakeRawDamage(damage))
            return false;
        if (IsDead)
            return true;

        _invincibilityCounter = 0x10;
        _knockbackCounter = 0x08;
        _knockbackAngle = GetCardinalAngleAwayFrom(sourcePosition);
        return true;
    }

    internal bool TakeBurnHit(int damage) => TakeRawDamage(damage);

    private bool TakeRawDamage(int damage)
    {
        if (IsDead)
            return false;
        _health = Math.Max(0, _health - Math.Max(1, damage));
        if (_health == 0)
        {
            IsDead = true;
            Visible = false;
        }
        return true;
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
        if (IsDead || !_animation.HasFrames)
            return;
        DrawTexture(_animation.CurrentTexture,
            new Vector2(-16, -16) + TransitionDrawOffset);
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
        OracleRandomResult result = _random.Next();
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
        if (allowHazards && _room.GetTerrainInfo(Position).Hazard != HazardType.None)
        {
            DeathHazard = _room.GetTerrainInfo(Position).Hazard;
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
                HazardType.Water or HazardType.Hole)
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
        _animation.SetAnimation((_angle & 0x18) >> 3);
    }

    private void AdvanceAnimation() => _animation.Advance();
}

internal enum OctorokState
{
    Deciding = 8,
    Standing = 9,
    Walking = 10,
    Shooting = 11
}
