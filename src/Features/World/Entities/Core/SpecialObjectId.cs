namespace Oracles;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class SpecialObjectId
{
    // constants/common/specialObjects.s: SPECIALOBJECT_LINK
    public const int Link = 0x00;
    // constants/common/specialObjects.s: SPECIALOBJECT_MINECART
    public const int Minecart = 0x0a;
    // constants/common/specialObjects.s: SPECIALOBJECT_RICKY
    public const int Ricky = 0x0b;
    // constants/common/specialObjects.s: SPECIALOBJECT_DIMITRI
    public const int Dimitri = 0x0c;
    // constants/common/specialObjects.s: SPECIALOBJECT_MOOSH
    public const int Moosh = 0x0d;
    // constants/common/specialObjects.s: SPECIALOBJECT_MAPLE
    public const int Maple = 0x0e;
    // constants/common/specialObjects.s: SPECIALOBJECT_MAPLE_CUTSCENE
    public const int MapleCutscene = 0x12;
    // constants/common/specialObjects.s: SPECIALOBJECT_RAFT
    public const int Raft = 0x13;
}
