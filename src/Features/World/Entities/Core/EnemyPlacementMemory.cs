namespace oracleofages;

// The placement view of wTmpcec0. Movement helpers reuse the first four
// bytes as velocity scratch, so these names apply only during placement.
internal static class EnemyPlacementMemory
{
    // include/structs.s: EnemyPlacementStruct.randomBufferIndex
    public const int RandomBufferIndex = 0xcec0;
    // include/structs.s: EnemyPlacementStruct.numEnemies
    public const int Count = 0xcec1;
    // include/structs.s: EnemyPlacementStruct.enemyPos
    public const int Position = 0xcec2;
    // include/structs.s: EnemyPlacementStruct.killedEnemiesBitset
    public const int KilledEnemies = 0xcec9;
    // include/structs.s: EnemyPlacementStruct.numKillableEnemies
    public const int KillableCount = 0xceca;
    // include/structs.s: EnemyPlacementStruct.randomPlacementAttemptCounter
    public const int AttemptsRemaining = 0xcecf;
    // include/structs.s: EnemyPlacementStruct.placedEnemyPositions
    public const int Positions = 0xced0;

    // code/objectLoading.s:parseObjectData clears $20 bytes, including struct padding.
    internal const int ResetByteCount = 0x20;
}
