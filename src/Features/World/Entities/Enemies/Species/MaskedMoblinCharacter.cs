using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared ENEMY_MASKED_MOBLIN $20:$00 walk/turn/arrow state machine. Room
/// $1:$38 creates two of these after the scripted interaction actors finish.
/// </summary>
public partial class MaskedMoblinCharacter : EnemyCharacter
{

    private MaskedMoblinRecord _record;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private MoblinState _state;
    private int _counter;
    private int _angle;
    private int _moveCycles;

    public MaskedMoblinRecord Record => _record;
    internal MoblinState State => _state;
    internal int Counter => _counter;
    internal int Angle => _angle;

    internal void Initialize(
        MaskedMoblinRecord record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        _record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = MoblinState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromSprite(
                record.Health,
                record.CollisionRadiusX,
                record.CollisionRadiusY,
                record.SpriteName,
                [record.UpAnimation, record.RightAnimation,
                 record.DownAnimation, record.LeftAnimation],
                record.TileBase,
                record.Palette));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        RestartAnimation(0);
    }

    /// <returns>The cardinal angle of an arrow to create, or -1.</returns>
    internal int UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return -1;
        if (BeginFrame())
            return -1;
        if (CheckHazards())
            return -1;

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
                AdvanceAnimation();
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

    public bool TakeSwordHit(Vector2 _)
        => TakeSwordHit(Vector2.Zero, 2);

    internal override bool TakeSwordHit(Vector2 _, int damage)
    {
        if (IsDead || !CollisionEnabled || InvincibilityCounter > 0)
            return false;
        return ApplyDamage(damage, invincibilityFrames: 0);
    }

    private void BeginMoving()
    {
        _counter = _record.MoveCounterBase +
            (_random.Next().Value & _record.MoveCounterMask);
        _state = MoblinState.Moving;
        RestartAnimation((_angle & 0x18) / 8);
    }
}

internal enum MoblinState
{
    Uninitialized,
    Moving,
    Turning
}
