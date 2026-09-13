using Godot;

namespace oracleofages;

internal interface IBurningEnemyTarget
{
    bool BurnTargetAlive { get; }
    int BurnTargetId { get; }
    Vector2 BurnPosition { get; }
    int BurnHealth { get; set; }
    int BurnZFixed { get; set; }
    void ReleaseBurn();
}
