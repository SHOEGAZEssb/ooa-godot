namespace oracleofages;

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
    // constants/common/globalFlags.s: GLOBALFLAG_0b
    public const int Flag0b = 0x0b;
    // constants/common/globalFlags.s: GLOBALFLAG_0c
    public const int MakuTreeDisappeared = 0x0c;
    // constants/common/globalFlags.s: GLOBALFLAG_WON_FAIRY_HIDING_GAME
    public const int WonFairyHidingGame = 0x0e;
    // constants/common/globalFlags.s: GLOBALFLAG_D3_CRYSTALS
    public const int D3Crystals = 0x0f;
    // constants/common/globalFlags.s: GLOBALFLAG_10
    public const int Flag10 = 0x10;
    // constants/common/globalFlags.s: GLOBALFLAG_SAVED_NAYRU
    public const int SavedNayru = 0x11;
    // constants/common/globalFlags.s: GLOBALFLAG_MAKU_TREE_SAVED
    public const int MakuTreeSaved = 0x12;
    // constants/common/globalFlags.s: GLOBALFLAG_SAW_TWINROVA_BEFORE_ENDGAME
    public const int SawTwinrovaBeforeEndgame = 0x13;
    // constants/common/globalFlags.s: GLOBALFLAG_FINISHEDGAME
    public const int FinishedGame = 0x14;
    // constants/common/globalFlags.s: GLOBALFLAG_GAVE_ROPE_TO_RAFTON
    public const int GaveRopeToRafton = 0x15;
    // constants/common/globalFlags.s: GLOBALFLAG_16
    public const int SuppressEraInfoOnce = 0x16;
    // constants/common/globalFlags.s: GLOBALFLAG_MOBLINS_KEEP_DESTROYED
    public const int MoblinsKeepDestroyed = 0x1a;
    // constants/common/globalFlags.s: GLOBALFLAG_CAN_BUY_FLUTE
    public const int CanBuyFlute = 0x1d;
    // constants/common/globalFlags.s: GLOBALFLAG_PATCH_REPAIRED_EVERYTHING
    public const int PatchRepairedEverything = 0x1f;
    // constants/common/globalFlags.s: GLOBALFLAG_PREGAME_INTRO_DONE
    public const int PregameIntroDone = 0x21;
    // constants/common/globalFlags.s: GLOBALFLAG_TALKED_TO_HEAD_CARPENTER
    public const int TalkedToHeadCarpenter = 0x22;
    // constants/common/globalFlags.s: GLOBALFLAG_GOT_FLUTE
    public const int GotFlute = 0x23;
    // constants/common/globalFlags.s: GLOBALFLAG_SAVED_COMPANION_FROM_FOREST
    public const int SavedCompanionFromForest = 0x24;
    // constants/common/globalFlags.s: GLOBALFLAG_RAFTON_CHANGED_ROOMS
    public const int RaftonChangedRooms = 0x26;
    // constants/common/globalFlags.s: GLOBALFLAG_TUNI_NUT_PLACED
    public const int TuniNutPlaced = 0x29;
    // constants/common/globalFlags.s: GLOBALFLAG_FOREST_UNSCRAMBLED
    public const int ForestUnscrambled = 0x2b;
    // constants/common/globalFlags.s: GLOBALFLAG_SAVED_GORON_ELDER
    public const int SavedGoronElder = 0x2f;
    // constants/common/globalFlags.s: GLOBALFLAG_PRE_BLACK_TOWER_CUTSCENE_DONE
    public const int PreBlackTowerCutsceneDone = 0x33;
    // constants/common/globalFlags.s: GLOBALFLAG_GOT_RING_FROM_ZELDA
    public const int GotRingFromZelda = 0x38;
    // constants/common/globalFlags.s: GLOBALFLAG_FLAME_OF_DESPAIR_LIT
    public const int FlameOfDespairLit = 0x3a;
    // constants/common/globalFlags.s: GLOBALFLAG_RETURNED_DOG
    public const int ReturnedDog = 0x3b;
    // constants/common/globalFlags.s: GLOBALFLAG_3d
    public const int LinkSummoned = 0x3d;
    // constants/common/globalFlags.s: GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PRESENT_MAP
    public const int MakuGivesAdviceFromPresentMap = 0x3e;
    // constants/common/globalFlags.s: GLOBALFLAG_MAKU_GIVES_ADVICE_FROM_PAST_MAP
    public const int MakuGivesAdviceFromPastMap = 0x3f;
    // constants/common/globalFlags.s: GLOBALFLAG_RALPH_ENTERED_PORTAL
    public const int RalphEnteredPortal = 0x40;
    // constants/common/globalFlags.s: GLOBALFLAG_ENTER_PAST_CUTSCENE_DONE
    public const int EnterPastCutsceneDone = 0x41;
    // constants/common/globalFlags.s: GLOBALFLAG_COMPANION_LOST_IN_FOREST
    public const int CompanionLostInForest = 0x42;
    // constants/common/globalFlags.s: GLOBALFLAG_TALKED_TO_CHEVAL
    public const int TalkedToCheval = 0x43;
    // constants/common/globalFlags.s: GLOBALFLAG_44
    public const int MapleMetInPast = 0x44;
    // constants/common/globalFlags.s: GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER
    public const int RalphEnteredBlackTower = 0x45;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_BIGGORON_SECRET
    public const int BeganBiggoronSecret = 0x58;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_RUUL_SECRET
    public const int BeganRuulSecret = 0x59;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_PLEN_SECRET
    public const int BeganPlenSecret = 0x67;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_ELDER_SECRET
    public const int BeganElderSecret = 0x6c;
    // constants/common/globalFlags.s: GLOBALFLAG_BEGAN_SYMMETRY_SECRET
    public const int BeganSymmetrySecret = 0x6d;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_PLEN_SECRET
    public const int DonePlenSecret = 0x71;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_ELDER_SECRET
    public const int DoneElderSecret = 0x76;
    // constants/common/globalFlags.s: GLOBALFLAG_DONE_SYMMETRY_SECRET
    public const int DoneSymmetrySecret = 0x77;
}
