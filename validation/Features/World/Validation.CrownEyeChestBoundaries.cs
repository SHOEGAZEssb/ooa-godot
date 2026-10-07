using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEyeChestBoundaries()
    {
        CompareChestAfterPuffGatesRom();
        GD.Print("Compared native chest-script exact trigger, one-time item gate, text initialization, command yields, puff slot/lifetime, queue rejection, completion and re-entry.");
    }
}
