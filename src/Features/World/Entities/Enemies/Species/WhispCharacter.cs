using Godot;

namespace oracleofages;

/// <summary>ENEMY_WHISP $19 diagonal bouncing state.</summary>
internal partial class WhispCharacter : EnemyCharacter
{
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private bool _initialized;
    private int _angle;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Angle => _angle;
    internal bool Initialized => _initialized;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        Record = record;
        _room = room;
        _random = random;
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

        EnemyAdjacentWallProbe walls =
            EnemyAdjacentWallResolver.Shared.Probe(
                Position, _angle, Collides);
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            _angle, walls);
        // whisp_state8 calls ecom_bounceOffWalls, then objectApplySpeed; it
        // does not run the adjacent-wall movement helper a second time.
        Position += OracleObjectMovement.Shared.Delta(
            EnemyBehaviorTables.Shared.Whisp.SpeedRaw,
            _angle);
        QueueRedraw();
        AdvanceAnimation();
    }

    /// <summary>
    /// ENEMY_WHISP state 0 consumes one global RNG value for its angle,
    /// installs state $08/SPEED_c0, and becomes visible even while the enemy
    /// dispatcher is restricted by wScrollMode.
    /// </summary>
    internal void PrepareForScreenTransition()
    {
        if (_initialized)
            return;
        _initialized = true;
        _angle = (_random.Next().Value & 0x18) + 0x04;
        Visible = true;
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => false;
    internal override bool TakeBurnHit(int _) => false;

    private bool Collides(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolid(point) ||
        _room.GetTerrainInfo(point).Hazard == HazardType.Hole;
}
