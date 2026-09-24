using Godot;
using System;

namespace oracleofages;

/// <summary>ENEMY_WHISP $19 diagonal bouncing state.</summary>
internal partial class WhispCharacter : EnemyCharacter
{
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private bool _initialized;
    private int _angle;
    private bool _collisionEnabled = true;
    private SparkTransformation? _transformation;
    internal override bool CollisionEnabled => _collisionEnabled && base.CollisionEnabled;
    internal int TransformationState => _transformation?.State ?? 8;
    internal bool TransformationCompleted => _transformation?.Completed == true;
    internal void ApplyBoomerangHit()
    {
        ApplyUnrandomizedMysteryHit();
        (_transformation ?? throw new InvalidOperationException(
            "ENEMY_WHISP $19 requires its native transformation owners.")).Hit();
    }
    internal void ConfigureTransformation(Func<Vector2, int> createPuff, Func<int, int> parameter,
        Action<Vector2, int> createFairy) =>
        _transformation = new(this, createPuff, parameter, createFairy, Finish);

    internal void ApplyUnrandomizedMysteryHit()
    {
        // collisionEffect35 sets health=0 and clears collisionType bit7.
        // Whisp's NO_HEALTH handler only transforms for boomerang IDs, so
        // this otherwise unused raw Mystery collision keeps moving visibly.
        Health = 0;
        _collisionEnabled = false;
    }

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
        if (_transformation?.Update(_angle, createsFairy: false) == true)
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
        Position += MovementDelta(
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
        _angle = _random.NextCardinalAngle() + 0x04;
        Visible = true;
        QueueRedraw();
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => false;
    internal override bool TakeBurnHit(int _) => false;

    private bool Collides(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        // whisp_state8 selects ecom_bounceOffWalls, with A=0.
        _room.IsSolidForEnemyMovement(point, holesAreWalls: false);
}
