namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class TreasureGrabMode
{
    // constants/common/treasureSpawnModes.s: TREASURE_GRAB_MODE_1_HAND
    public const int OneHand = 0x01;
    // constants/common/treasureSpawnModes.s: TREASURE_GRAB_MODE_2_HAND
    public const int TwoHands = 0x02;
    // constants/common/treasureSpawnModes.s: TREASURE_GRAB_MODE_SPIN_SLASH
    public const int SpinSlash = 0x03;
}
