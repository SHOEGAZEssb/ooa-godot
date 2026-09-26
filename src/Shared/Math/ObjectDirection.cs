namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class ObjectDirection
{
    // constants/common/directions.s: DIR_UP
    public const int Up = 0x00;
    // constants/common/directions.s: DIR_RIGHT
    public const int Right = 0x01;
    // constants/common/directions.s: DIR_DOWN
    public const int Down = 0x02;
    // constants/common/directions.s: DIR_LEFT
    public const int Left = 0x03;
}
