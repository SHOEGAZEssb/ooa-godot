using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleCutscene()
    {
        CompareToggleFloorContactRom();
        GD.Print("Compared native Crown orb Sword approach/repeat, PART-to-cutscene selection, freeze/release, delay, floor/debris capacity, four-tile graphics uploads, cue/RNG order and reverse toggle through split/batched gameplay.");
    }
}
