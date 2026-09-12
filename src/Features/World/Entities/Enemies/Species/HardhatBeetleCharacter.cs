using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_HARDHAT_BEETLE $4d:$00. It continuously tracks Link at SPEED_60,
/// cannot enter holes, and converts sword collisions into recoil without
/// losing health.
/// </summary>
internal partial class HardhatBeetleCharacter : EnemyCharacter
{
    private readonly HardhatBeetleBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.HardhatBeetle;
    private EnemyTerrainMovement _movement = null!;
    private bool _initialized;
    private int _angle;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool Initialized => _initialized;
    internal int Angle => _angle;
    internal void DeleteForRoomEvent() => Finish();

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position)
    {
        Record = record;
        Dictionary<int, Color[]>? palettes = null;
        if (record.Id == 0x5f)
        {
            var source = OracleGraphicsData.LoadPalette("res://assets/oracle/objects/harmless_beetle_palette.bin", 1, 6);
            var colors = new Color[4];
            for (int shade = 0; shade < 4; shade++) colors[shade] = source[6, shade];
            palettes = new() { [6] = colors };
        }
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record), paletteOverrides: palettes);
        _movement = new EnemyTerrainMovement(this, room);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
    }

    internal void UpdateFrame(Vector2 linkPosition)
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;
        if (CheckHazards())
            return;
        if (!_initialized)
        {
            _initialized = true;
            return;
        }

        _angle = OracleObjectMovement.Shared.RelativeAngle(
            OracleObjectMath.ToPixelPosition(Position),
            OracleObjectMath.ToPixelPosition(linkPosition));
        _movement.MoveUsingAdjacentWalls(
            _angle,
            _behavior.SpeedRaw,
            allowHoles: false,
            topDown: false);
        AdvanceAnimation();
    }

    internal bool TakeBumpHit(Vector2 sourcePosition, int damage)
    {
        _ = sourcePosition;
        _ = damage;
        return !IsDead && CollisionEnabled && InvincibilityCounter == 0;
    }

    internal override bool TakeBurnHit(int damage)
    {
        _ = damage;
        return false;
    }
}
