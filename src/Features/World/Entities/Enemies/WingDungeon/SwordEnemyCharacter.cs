using Godot;

namespace oracleofages;

internal partial class SwordEnemyCharacter : EnemyCharacter
{
    private static readonly uint[] BlockingAngleMasks =
    [
        0x0000003f,
        0x00003f00,
        0x003f0000,
        0x01f80000
    ];

    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private SwordEnemyState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _speedRaw;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SwordEnemyState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal Vector2 EnemySwordPosition
    {
        get
        {
            int direction = ((_angle + 4) & 0x18) >> 3;
            int frame = AnimationParameter & 1;
            return Position + SwordOffsets[direction, frame];
        }
    }
    internal Rect2 EnemySwordCollisionBounds
    {
        get
        {
            int direction = ((_angle + 4) & 0x18) >> 3;
            Vector2 radius = direction is 0 or 2
                ? new Vector2(2, 5)
                : new Vector2(5, 2);
            return new Rect2(
                EnemySwordPosition - radius,
                radius * 2.0f);
        }
    }

    private static readonly Vector2[,] SwordOffsets =
    {
        { new(4, -8), new(4, -10) },
        { new(7, 4), new(9, 4) },
        { new(-4, 7), new(-4, 9) },
        { new(-7, 4), new(-9, 4) }
    };

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _state = SwordEnemyState.Uninitialized;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead || BeginFrame())
            return;
        if (CheckHazards())
            return;
        WingDungeonEnemyBehavior behavior =
            WingDungeonEnemyBehavior.Shared;

        switch (_state)
        {
            case SwordEnemyState.Uninitialized:
                _angle = _random.Next().Value & 0x18;
                _speedRaw = behavior["sword-wander-speed-raw"];
                _counter1 = 1;
                _counter2 = behavior[
                    $"sword-cooldown-{System.Math.Min(2, Record.SubId)}"];
                _state = SwordEnemyState.Wandering;
                SetDirectionalAnimation();
                Visible = true;
                return;

            case SwordEnemyState.Wandering:
                if (_counter2 > 0)
                    _counter2--;
                if (_counter2 == 0 &&
                    Mathf.Abs(linkPosition.X - Position.X) <=
                        behavior["sword-chase-radius"] &&
                    Mathf.Abs(linkPosition.Y - Position.Y) <=
                        behavior["sword-chase-radius"])
                {
                    _state = SwordEnemyState.PreparingChase;
                    _counter1 = behavior["sword-chase-prepare-frames"];
                    _angle = OracleObjectMovement.Shared.RelativeAngle(
                        Position, linkPosition);
                    SetDirectionalAnimation();
                    return;
                }

                _counter1 = (_counter1 - 1) & 0xff;
                if (_counter1 == 0)
                    ChooseWanderRoute(linkPosition);
                if (!_movement.MoveAtAngle(
                    _angle, _speedRaw, allowHoles: false))
                {
                    _angle = (_angle + 0x10) & 0x18;
                    SetDirectionalAnimation();
                }
                AdvanceAnimation();
                return;

            case SwordEnemyState.PreparingChase:
                if (--_counter1 != 0)
                    return;
                _state = SwordEnemyState.Chasing;
                _counter1 = behavior["sword-chase-frames"];
                _speedRaw = behavior["sword-chase-speed-raw"];
                return;

            case SwordEnemyState.Chasing:
                if (--_counter1 == 0)
                {
                    _state = SwordEnemyState.Wandering;
                    _speedRaw = behavior["sword-wander-speed-raw"];
                    _angle = (_angle + 4) & 0x18;
                    _counter2 = behavior[
                        $"sword-cooldown-{System.Math.Min(2, Record.SubId)}"];
                    SetDirectionalAnimation();
                    AdvanceAnimation();
                    return;
                }
                if ((_counter1 &
                    behavior["sword-turn-interval-mask"]) == 0)
                {
                    int target =
                        OracleObjectMovement.Shared.RelativeAngle(
                            Position, linkPosition);
                    _angle = NudgeAngle(_angle, target);
                    SetDirectionalAnimation();
                }
                _movement.MoveAtAngle(
                    _angle, _speedRaw, allowHoles: false);
                AdvanceAnimation();
                AdvanceAnimation();
                return;
        }
    }

    internal bool BlocksSwordFrom(Vector2 sourcePosition)
    {
        int sourceAngle = OracleObjectMovement.Shared.RelativeAngle(
            Position, sourcePosition);
        int direction = ((_angle + 4) & 0x18) >> 3;
        return (BlockingAngleMasks[direction] &
            (1u << sourceAngle)) != 0;
    }

    private void ChooseWanderRoute(Vector2 linkPosition)
    {
        WingDungeonEnemyBehavior behavior =
            WingDungeonEnemyBehavior.Shared;
        OracleRandomResult result = _random.Next();
        _counter1 = behavior["sword-route-base"] +
            (result.Low & behavior["sword-route-mask"]);
        _angle = (result.High &
            behavior["sword-toward-mask"]) == 0
            ? OracleObjectMovement.Shared.RelativeAngle(
                Position, linkPosition) & 0x18
            : result.High & 0x18;
        SetDirectionalAnimation();
    }

    private void SetDirectionalAnimation() =>
        SetAnimation(((_angle + 4) & 0x18) >> 3);

    private static int NudgeAngle(int angle, int target)
    {
        int clockwise = (target - angle) & 0x1f;
        if (clockwise == 0)
            return angle;
        return clockwise < 0x10
            ? (angle + 1) & 0x1f
            : (angle - 1) & 0x1f;
    }
}

internal enum SwordEnemyState
{
    Uninitialized,
    Wandering = 8,
    PreparingChase,
    Chasing
}
