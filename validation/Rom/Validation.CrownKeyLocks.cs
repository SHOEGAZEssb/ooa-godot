namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyLocks() => CompareDungeonKeyLocksRom(crown:true);
}
