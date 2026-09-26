namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class WarpSourceTransition
{
    // constants/common/transitions.s: TRANSITION_SRC_FADEOUT
    public const int FadeOut = 0x02;
    // constants/common/transitions.s: TRANSITION_SRC_LEAVESCREEN
    public const int LeaveScreen = 0x03;
    // constants/common/transitions.s: TRANSITION_SRC_INSTANT
    public const int Instant = 0x04;
}
