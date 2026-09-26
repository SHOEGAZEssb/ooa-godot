namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class TreasureSpawnMode
{
    // constants/common/treasureSpawnModes.s: TREASURE_SPAWN_MODE_INSTANT
    public const int Instant = 0x00;
    // constants/common/treasureSpawnModes.s: TREASURE_SPAWN_MODE_FROM_SCREEN_TOP
    public const int FromScreenTop = 0x02;
    // constants/common/treasureSpawnModes.s: TREASURE_SPAWN_MODE_BURIED
    public const int Buried = 0x05;
}
