using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Common ENEMY_STALFOS $31 state machine for ordinary subid $00. The other
/// source subids extend these states with jumps and bone/stomp attacks.
/// </summary>
public partial class StalfosCharacter : EnemyCharacter
{

    private static readonly Vector2I[,] SideviewCollisionOffsets =
    {
        { new(-4, -5), new(0, 9), new(4, -4), new(0, 0) },
        { new(-4, -5), new(0, 9), new(3, 2), new(6, 0) },
        { new(0, 0), new(0, 0), new(-1, 6), new(6, 0) },
        { new(7, -5), new(0, 9), new(-8, 2), new(6, 0) },
        { new(7, -5), new(0, 9), new(-7, -4), new(0, 0) },
        { new(7, -5), new(0, 9), new(-8, -11), new(6, 0) },
        { new(0, 0), new(0, 0), new(-1, -7), new(6, 0) },
        { new(-4, -5), new(0, 9), new(3, -11), new(6, 0) }
    };

    private static readonly int[] BounceAngles =
    {
        0x10, 0x0f, 0x0e, 0x0d, 0x0c, 0x0b, 0x0a, 0x09,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
        0x00, 0x1f, 0x1e, 0x1d, 0x1c, 0x1b, 0x1a, 0x19,
        0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
        0x10, 0x0f, 0x0e, 0x0d, 0x0c, 0x0b, 0x09, 0x08,
        0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01
    };

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private StalfosState _state;
    private int _counter1;
    private int _angle;

    public StalfosRecord Record { get; private set; }
    internal StalfosState State => _state;
    internal int Counter1 => _counter1;
    internal int Angle => _angle;
    internal int CurrentAnimationFrame => AnimationFrame;

    internal void Initialize(
        StalfosRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        if (record.SubId != 0)
            throw new ArgumentOutOfRangeException(
                nameof(record), record.SubId,
                "Only ordinary ENEMY_STALFOS subid $00 is implemented.");

        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = StalfosState.Uninitialized;

        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                new[] { record.WalkAnimation, record.JumpAnimation },
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        RestartAnimation(0);
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if (CheckHazards())
            return;

        switch (_state)
        {
            case StalfosState.Uninitialized:
                _state = StalfosState.Deciding;
                Visible = true;
                return;

            case StalfosState.Deciding:
                // State $08 always consumes this first 1-in-8 attack roll.
                // Subid $00 cannot shoot, so every result falls through to
                // the shared random-walk selection and its second RNG call.
                _random.Next();
                BeginRandomWalk(linkPosition);
                return;

            case StalfosState.Walking:
                _counter1--;
                if (_counter1 == 0)
                    _state = StalfosState.Deciding;
                BounceOffWallsAndHoles();
                Position += OracleObjectMath.VectorFromAngle32(_angle) *
                    (Record.SpeedRaw / 40.0f);
                QueueRedraw();
                AdvanceAnimation();
                return;
        }
    }

    public bool TakeSwordHit(Vector2 sourcePosition)
        => TakeSwordHit(sourcePosition, 2);

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    private void BeginRandomWalk(Vector2 linkPosition)
    {
        OracleRandomResult result = _random.Next();
        _counter1 = 0x20 + (result.Value & 0x30);
        _angle = (result.Low & 0x0f) == 1
            ? OracleObjectMath.AngleToward(Position, linkPosition)
            : result.High & 0x1f;
        _state = StalfosState.Walking;
        RestartAnimation(0);
    }

    private void BounceOffWallsAndHoles()
    {
        int doubledAngle = _angle * 2;
        int tableOffset = (doubledAngle & 0x0f) == 0
            ? doubledAngle
            : (doubledAngle & 0xf0) + 8;
        int octant = tableOffset / 8;
        Vector2I point = new(
            Mathf.FloorToInt(Position.X), Mathf.FloorToInt(Position.Y));
        bool hitVertical = false;
        bool hitHorizontal = false;
        for (int probe = 0; probe < 4; probe++)
        {
            point += SideviewCollisionOffsets[octant, probe];
            bool collision = IsWallOrHole(point);
            if (probe < 2)
                hitVertical |= collision;
            else
                hitHorizontal |= collision;
        }

        if (hitHorizontal && hitVertical)
            _angle = (_angle + 0x10) & 0x1f;
        else if (hitHorizontal)
            _angle = BounceAngles[0x10 + _angle];
        else if (hitVertical)
            _angle = BounceAngles[_angle];
    }

    private bool IsWallOrHole(Vector2I point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return true;
        }

        Vector2 sample = point;
        return _room.IsSolid(sample) ||
            _room.GetTerrainInfo(sample).Hazard == HazardType.Hole;
    }
}

internal enum StalfosState
{
    Uninitialized = 0,
    Deciding = 8,
    Walking = 9
}
