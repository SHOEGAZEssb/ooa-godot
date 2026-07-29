using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Common ENEMY_STALFOS $31 state machine for ordinary subid $00. The other
/// source subids extend these states with jumps and bone/stomp attacks.
/// </summary>
public partial class StalfosCharacter : EnemyCharacter
{
    private readonly StalfosBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Stalfos;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
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
                Position +=
                    OracleObjectMovement.Shared.Delta(Record.SpeedRaw, _angle);
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
        _counter1 = _behavior.MoveCounterBase +
            (result.Value & _behavior.MoveCounterMask);
        _angle = (result.Low & 0x0f) == 1
            ? OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition)
            : result.High & 0x1f;
        _state = StalfosState.Walking;
        RestartAnimation(0);
    }

    private void BounceOffWallsAndHoles()
    {
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position,
            _angle,
            IsWallOrHole);
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
