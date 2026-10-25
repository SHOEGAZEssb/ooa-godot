namespace Oracles;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class GlobalFlag
{
    // constants/common/globalFlags.s: GLOBALFLAG_1000_ENEMIES_KILLED
    public const int Flag1000EnemiesKilled = 0x00;
    // constants/common/globalFlags.s: GLOBALFLAG_10000_RUPEES_COLLECTED
    public const int Flag10000RupeesCollected = 0x01;
    // constants/common/globalFlags.s: GLOBALFLAG_INTRO_DONE
    public const int IntroDone = 0x0a;
    // constants/common/globalFlags.s (Ages): GLOBALFLAG_RING_SECRET_GENERATED
    public const int RingSecretGenerated = 0x28;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_BIGGORON_SECRET
    public const int BeganBiggoronSecret = 0x58;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_RUUL_SECRET
    public const int BeganRuulSecret = 0x59;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_FAIRY_SECRET
    public const int BeganFairySecret = 0x65;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_TROY_SECRET
    public const int BeganTroySecret = 0x66;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_PLEN_SECRET
    public const int BeganPlenSecret = 0x67;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_ELDER_SECRET
    public const int BeganElderSecret = 0x6c;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_SYMMETRY_SECRET
    public const int BeganSymmetrySecret = 0x6d;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_FAIRY_SECRET
    public const int DoneFairySecret = 0x6f;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_TROY_SECRET
    public const int DoneTroySecret = 0x70;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_PLEN_SECRET
    public const int DonePlenSecret = 0x71;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_ELDER_SECRET
    public const int DoneElderSecret = 0x76;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_SYMMETRY_SECRET
    public const int DoneSymmetrySecret = 0x77;
}
