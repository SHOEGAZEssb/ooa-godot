namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookDungeonSwitches()
    {
        CompareSwitchTileReconstructionRom();
        CompareDungeonSwitchEdgesRom();
        CompareDungeonSwitchContactsRom();
        CompareSwitchTileTogglerGatesRom();
        CompareNuunSwitchBridgeRom();
        CompareNuunSwitchAllocationRom();
        var collisions = PartSwitchCollisionDatabase.Shared;
        // Independent partActiveCollisions $05 mask/effect goldens remain
        // useful alongside the executed contact and rail handoff checks.
        const string enabled = "00001111111101100000001101111110";
        for (int item = 0; item < 32; item++)
        {
            int expected = enabled[item] == '0' ? -1 : item >= 0x19 ? 0 : 28;
            FailIf(collisions.HitLockout(item) != expected,
                $"PART_SWITCH collision ${item:x2} lost the source mask/effect lockout.");
        }
    }
}
