namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class SpecialObjectId
{
    // constants/common/specialObjects.s: SPECIALOBJECT_LINK
    public const int Link = 0x00;
    // constants/common/specialObjects.s: SPECIALOBJECT_DIMITRI
    public const int Dimitri = 0x0c;
    // constants/common/specialObjects.s: SPECIALOBJECT_MOOSH
    public const int Moosh = 0x0d;
    // constants/common/specialObjects.s: SPECIALOBJECT_RAFT
    public const int Raft = 0x13;
}
