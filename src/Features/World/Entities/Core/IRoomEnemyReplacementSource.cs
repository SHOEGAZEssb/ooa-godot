namespace oracleofages;

internal interface IRoomEnemyReplacementSource
{
    bool TryTakeReplacement(out RoomEnemyReplacement replacement);
}

internal readonly record struct RoomEnemyReplacement(RoomObjectRecord Source, int KillableEnemyIndex, int ZHigh);
