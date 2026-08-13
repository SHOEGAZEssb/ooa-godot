using Godot;

namespace oracleofages;

/// <summary>
/// ENEMY_ARM_MIMIC $4e:$00. Its movement angle and animation direction are
/// the exact opposites of Link's live wLinkAngle and w1Link.direction.
/// </summary>
internal partial class ArmMimicCharacter : EnemyCharacter
{
    private readonly ArmMimicBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.ArmMimic;
    private EnemyTerrainMovement _movement = null!;
    private bool _initialized;
    private Vector2I _preloadLinkFacing;
    private int _angle;
    private int _direction;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool Initialized => _initialized;
    internal int Angle => _angle;
    internal int Direction => _direction;
    internal int SpeedRaw => _behavior.SpeedRaw;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        Vector2I preloadLinkFacing = default)
    {
        if (record.Animations.Length != 4)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(record), record.Animations.Length,
                "ENEMY_ARM_MIMIC requires four directional animations.");
        }

        Record = record;
        _initialized = false;
        _preloadLinkFacing = preloadLinkFacing;
        _angle = 0xff;
        _direction = 0;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        Visible = false;
    }

    internal void UpdateFrame(int linkAngle, Vector2I linkFacing)
    {
        if (IsDead)
            return;
        if (CheckHazards())
            return;
        if (BeginFrame())
            return;

        int reverseDirection =
            (DirectionIndex(linkFacing) + _behavior.ReverseDirectionOffset) &
            _behavior.DirectionMask;
        if (!_initialized)
        {
            InitializePresentation(linkFacing);
            return;
        }

        // wLinkAngle=$ff means Link is not supplying movement. The source
        // returns before both movement and enemyAnimate on that update.
        if (linkAngle == 0xff)
            return;
        if (linkAngle is < 0 or > 0x1f)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(linkAngle), linkAngle,
                "wLinkAngle must be $00-$1f or stopped value $ff.");
        }

        _angle = (linkAngle + _behavior.ReverseAngleOffset) &
            _behavior.AngleMask;
        _movement.MoveUsingAdjacentWalls(
            _angle,
            _behavior.SpeedRaw,
            allowHoles: false,
            topDown: false);

        if (_direction != reverseDirection)
        {
            _direction = reverseDirection;
            SetAnimation(_direction);
        }
        AdvanceAnimation();
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (!_initialized)
        {
            if (!IsCardinal(_preloadLinkFacing))
            {
                throw new System.InvalidOperationException(
                    "ENEMY_ARM_MIMIC source state 0 requires Link's " +
                    "cardinal scrolling direction during destination preload.");
            }
            InitializePresentation(_preloadLinkFacing);
        }
        return ScreenTransitionPresentation.Visible;
    }

    private void InitializePresentation(Vector2I linkFacing)
    {
        _initialized = true;
        _direction =
            (DirectionIndex(linkFacing) + _behavior.ReverseDirectionOffset) &
            _behavior.DirectionMask;
        SetAnimation(_direction);
        Visible = true;
        QueueRedraw();
    }

    private static bool IsCardinal(Vector2I direction) =>
        direction == Vector2I.Up || direction == Vector2I.Right ||
        direction == Vector2I.Down || direction == Vector2I.Left;

    private static int DirectionIndex(Vector2I direction) => direction switch
    {
        { X: 0, Y: -1 } => 0,
        { X: 1, Y: 0 } => 1,
        { X: 0, Y: 1 } => 2,
        { X: -1, Y: 0 } => 3,
        _ => throw new System.ArgumentOutOfRangeException(
            nameof(direction), direction,
            "Link direction must be cardinal for Arm Mimic animation.")
    };
}
