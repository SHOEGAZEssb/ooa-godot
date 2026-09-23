namespace oracleofages;

/// <summary>Source counter1 reads/writes that survive enemyDelete's cleared slot.</summary>
internal interface INativeEnemyCounter1RoomEntity
{
    int Counter1 { get; set; }
    bool RetainsCounter1AfterDeletion { get; }
}
