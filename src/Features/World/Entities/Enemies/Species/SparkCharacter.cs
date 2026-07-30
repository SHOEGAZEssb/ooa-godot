using Godot;

namespace oracleofages;

/// <summary>ENEMY_SPARK $13 wall-hugging state.</summary>
internal partial class SparkCharacter : EnemyCharacter
{
    private static readonly Vector2I[,] WallProbeOffsets =
    {
        { new(-4, -9), new(7, 0) },
        { new(8, -4), new(0, 7) },
        { new(-4, 8), new(7, 0) },
        { new(-9, -4), new(0, 7) }
    };

    private OracleRoomData _room = null!;
    private bool _initialized;
    private int _angle;
    private Vector2 _precisePosition;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Angle => _angle;
    internal bool Initialized => _initialized;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position)
    {
        Record = record;
        _room = room;
        _precisePosition = position;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame()
    {
        if (BeginFrame())
            return;
        if (!_initialized)
        {
            PrepareForScreenTransition();
            return;
        }

        int wallAngle = (_angle - 0x08) & 0x18;
        if (!WallInDirection(wallAngle))
        {
            int coordinate = (_angle & 0x08) == 0
                ? Mathf.FloorToInt(Position.Y)
                : Mathf.FloorToInt(Position.X);
            if ((coordinate & 0x07) == 0)
                _angle = wallAngle;
        }
        else if (WallInDirection(_angle))
        {
            _angle = (_angle + 0x08) & 0x18;
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            EnemyBehaviorTables.Shared.Spark.SpeedRaw,
            _angle);
        AdvanceAnimation();
    }

    /// <summary>
    /// ENEMY_SPARK state 0 resolves its initial wall angle and visibility
    /// during destination room parsing, before a scrolling transition exposes
    /// the incoming room. Movement and animation remain frozen until the
    /// transition completes.
    /// </summary>
    internal void PrepareForScreenTransition()
    {
        if (_initialized)
            return;
        _initialized = true;
        _angle = InitialWallAngle();
        Visible = true;
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => false;
    internal override bool TakeBurnHit(int _) => false;

    private int InitialWallAngle()
    {
        for (int direction = 0; direction < 3; direction++)
        {
            if (WallInDirection(direction * 0x08))
                return (direction + 1) * 0x08;
        }
        return 0;
    }

    private bool WallInDirection(int angle)
    {
        int direction = (angle & 0x18) >> 3;
        Vector2I center = new(
            Mathf.FloorToInt(Position.X),
            Mathf.FloorToInt(Position.Y));
        Vector2I first = center + WallProbeOffsets[direction, 0];
        Vector2I second = first + WallProbeOffsets[direction, 1];
        return Collides(first) || Collides(second);
    }

    private bool Collides(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolid(point) ||
        _room.GetTerrainInfo(point).Hazard == HazardType.Hole;
}
