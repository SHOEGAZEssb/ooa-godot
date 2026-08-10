using Godot;

namespace oracleofages;

/// <summary>
/// Shared ecom_updateAngleToScentSeed counter. Enemy.var3d is a wrapping byte:
/// it starts at zero and refreshes the target angle every 16 attraction
/// updates, while each species retains ownership of movement and exit state.
/// </summary>
internal sealed class ScentSeedAttraction
{
    private readonly ScentSeedAttractionBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.ScentSeedAttraction;
    private int _counter;

    internal int Counter => _counter;

    internal int UpdateAngle(
        Vector2 enemyPosition,
        Vector2 scentPosition,
        int currentAngle,
        bool cardinal)
    {
        _counter = (_counter - 1) & 0xff;
        int angle = currentAngle;
        if ((_counter & _behavior.AngleRefreshMask) == 0)
        {
            angle = OracleObjectMovement.Shared.RelativeAngle(
                enemyPosition, scentPosition);
        }
        return cardinal
            ? (angle + _behavior.CardinalRounding) &
                _behavior.CardinalMask
            : angle;
    }
}
