using Godot;

namespace oracleofages;

internal partial class ThwompCharacter : EnemyCharacter
{
    private const int ArmoredInvincibilityFrames = 28;
    private readonly ArmoredSwordAttackerKnockbackProfile
        _attackerKnockback =
            EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
    private OracleRoomData _room = null!;
    private ThwompState _state;
    private int _counter;
    private int _speedYFixed;
    private int _yFixed;
    private int _originalYFixed;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ThwompState State => _state;
    internal int Counter => _counter;
    internal int SpeedYFixed => _speedYFixed;
    internal int ArmoredAttackerKnockbackFrames(
        EnemyKnockbackStrength strength) => strength switch
        {
            EnemyKnockbackStrength.Low => _attackerKnockback.LowFrames,
            EnemyKnockbackStrength.Normal => _attackerKnockback.NormalFrames,
            EnemyKnockbackStrength.High => _attackerKnockback.HighFrames,
            _ => 0
        };

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position)
    {
        Record = record;
        _room = room;
        _yFixed = Mathf.FloorToInt(position.Y * 256.0f);
        _originalYFixed = _yFixed;
        _state = ThwompState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record),
            initialAnimation: 4);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (BeginFrame())
            return;
        ThwompBehaviorProfile behavior =
            EnemyBehaviorTables.Shared.Thwomp;
        switch (_state)
        {
            case ThwompState.Uninitialized:
                PrepareForScreenTransition();
                return;

            case ThwompState.Waiting:
                if (Mathf.Abs(linkPosition.X - Position.X) <=
                    behavior.ApproachRadius)
                {
                    _state = ThwompState.Falling;
                    _speedYFixed = 0;
                    SetAnimation(8);
                    return;
                }
                int lookAngle = (
                    OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition) + 2) & 0x1c;
                SetAnimation(lookAngle >> 2);
                return;

            case ThwompState.Falling:
                if (TouchesGround())
                {
                    _state = ThwompState.Resting;
                    _counter = behavior.RestFrames;
                    return;
                }
                _yFixed += _speedYFixed;
                Position = OracleObjectMath.ToPixelPosition(new Vector2(
                    Position.X, _yFixed / 256.0f));
                _speedYFixed = System.Math.Min(
                    0x02ff,
                    _speedYFixed + behavior.Gravity);
                return;

            case ThwompState.Resting:
                if (--_counter != 0)
                    return;
                _state = ThwompState.Rising;
                return;

            case ThwompState.Rising:
                if (_yFixed != _originalYFixed)
                {
                    _yFixed = System.Math.Max(
                        _originalYFixed,
                        _yFixed - behavior.RiseSpeedFixed);
                    Position = OracleObjectMath.ToPixelPosition(new Vector2(
                        Position.X, _yFixed / 256.0f));
                    return;
                }
                _state = ThwompState.Cooldown;
                _counter = behavior.CooldownFrames;
                return;

            case ThwompState.Cooldown:
                if (--_counter == 0)
                    _state = ThwompState.Waiting;
                return;
        }
    }

    /// <summary>
    /// ENEMY_THWOMP state 0 installs state $08, animation $04, and visibility
    /// while the destination room is parsed. Its ordinary proximity, facing,
    /// movement, animation, and riding updates remain frozen during scrolling.
    /// </summary>
    internal void PrepareForScreenTransition()
    {
        if (_state != ThwompState.Uninitialized)
            return;
        _state = ThwompState.Waiting;
        Visible = true;
        QueueRedraw();
    }

    internal bool IsLinkRiding(Player player, out float targetY)
    {
        ThwompBehaviorProfile behavior =
            EnemyBehaviorTables.Shared.Thwomp;
        float contactPlane = Position.Y - Record.RadiusY - 6;
        targetY =
            contactPlane - behavior.RidingSlopY;
        return Mathf.Abs(player.Position.X - Position.X) <=
                behavior.RidingRadiusX &&
            Mathf.Abs(player.Position.Y - contactPlane) <=
                behavior.RidingSlopY;
    }

    internal override bool TakeSwordHit(Vector2 _, int __) =>
        AcceptArmoredSwordHit(ArmoredInvincibilityFrames);
    internal override bool TakeBurnHit(int _) => false;

    private bool TouchesGround()
    {
        int y = (_yFixed >> 8) + 0x10;
        int x = Mathf.FloorToInt(Position.X);
        return _room.IsSolid(new Vector2(x - 4, y)) ||
            _room.IsSolid(new Vector2(x + 3, y));
    }
}

internal enum ThwompState
{
    Uninitialized,
    Waiting = 8,
    Falling,
    Resting,
    Rising,
    Cooldown
}
