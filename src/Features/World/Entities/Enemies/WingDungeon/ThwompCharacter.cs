using Godot;

namespace oracleofages;

internal partial class ThwompCharacter : EnemyCharacter
{
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
        WingDungeonEnemyBehavior behavior =
            WingDungeonEnemyBehavior.Shared;
        switch (_state)
        {
            case ThwompState.Uninitialized:
                _state = ThwompState.Waiting;
                Visible = true;
                return;

            case ThwompState.Waiting:
                if (Mathf.Abs(linkPosition.X - Position.X) <=
                    behavior["thwomp-approach-radius"])
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
                    _counter = behavior["thwomp-rest-frames"];
                    return;
                }
                _yFixed += _speedYFixed;
                Position = OracleObjectMath.ToPixelPosition(new Vector2(
                    Position.X, _yFixed / 256.0f));
                _speedYFixed = System.Math.Min(
                    0x02ff,
                    _speedYFixed + behavior["thwomp-gravity"]);
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
                        _yFixed - behavior["thwomp-rise-speed-fixed"]);
                    Position = OracleObjectMath.ToPixelPosition(new Vector2(
                        Position.X, _yFixed / 256.0f));
                    return;
                }
                _state = ThwompState.Cooldown;
                _counter = behavior["thwomp-cooldown-frames"];
                return;

            case ThwompState.Cooldown:
                if (--_counter == 0)
                    _state = ThwompState.Waiting;
                return;
        }
    }

    internal bool IsLinkRiding(Player player, out float targetY)
    {
        WingDungeonEnemyBehavior behavior =
            WingDungeonEnemyBehavior.Shared;
        float contactPlane = Position.Y - Record.RadiusY - 6;
        targetY =
            contactPlane - behavior["thwomp-riding-slop-y"];
        return Mathf.Abs(player.Position.X - Position.X) <=
                behavior["thwomp-riding-radius-x"] &&
            Mathf.Abs(player.Position.Y - contactPlane) <=
                behavior["thwomp-riding-slop-y"];
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => false;
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
