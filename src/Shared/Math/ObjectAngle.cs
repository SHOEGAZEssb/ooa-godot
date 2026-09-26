namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class ObjectAngle
{
    // Object.angle is a clockwise $00-$1f index (constants/common/directions.s).
    public const int FullTurn = 0x20;
    public const int Mask = FullTurn - 1;
    public const int CardinalMask = 0x18;
    public const int HalfTurn = FullTurn / 2;
    // constants/common/directions.s: ANGLE_UP
    public const int Up = 0x00;
    // constants/common/directions.s: ANGLE_RIGHT
    public const int Right = 0x08;
    // constants/common/directions.s: ANGLE_DOWN
    public const int Down = 0x10;
    // constants/common/directions.s: ANGLE_LEFT
    public const int Left = 0x18;
}
