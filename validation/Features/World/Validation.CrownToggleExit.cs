namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleExit()
    {
        CompareToggleFloorExitRom();
        ReinitializeGameplayForValidation();
    }
}
