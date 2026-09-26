namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class WarpDestinationTransition
{
    // constants/common/transitions.s: TRANSITION_DEST_BASIC
    public const int Basic = 0x00;
    // constants/common/transitions.s: TRANSITION_DEST_SET_RESPAWN
    public const int SetRespawn = 0x01;
    // constants/common/transitions.s: TRANSITION_DEST_ENTERSCREEN
    public const int EnterScreen = 0x03;
    // constants/common/transitions.s: TRANSITION_DEST_DONT_SET_RESPAWN
    public const int DontSetRespawn = 0x04;
    // constants/common/transitions.s: TRANSITION_DEST_FALL
    public const int Fall = 0x05;
    // constants/common/transitions.s: TRANSITION_DEST_TIMEWARP
    public const int TimeWarp = 0x06;
    // constants/common/transitions.s: TRANSITION_DEST_SLOWFALL
    public const int SlowFall = 0x0b;
    // constants/common/transitions.s: TRANSITION_DEST_UNKNOWN_C
    public const int UnknownC = 0x0c;
    // constants/common/transitions.s: TRANSITION_DEST_X_SHIFTED
    public const int XShifted = 0x0e;
}
