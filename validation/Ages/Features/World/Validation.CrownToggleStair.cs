namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleStair()
    {
        CompareToggleFloorStairRom();
        ReinitializeGameplayForValidation();
    }
}
