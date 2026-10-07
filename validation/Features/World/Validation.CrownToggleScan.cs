using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleScan()
    {
        CompareToggleFloorScanRom();
        GD.Print("Compared native toggle storage scan boundaries, excluded$00, included padding/$af, literal layout/underlying/collision pairs and no-debris completion through split/batched gameplay.");
    }
}
