using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleQueue()
    {
        CompareToggleFloorQueueRom();
        GD.Print("Compared native toggle cutscene selection, delay, floor buffers/collision, graphics queue rejection, displayed mappings, ordered debris slots/animation/lifetimes, cues and RNG in split/batched gameplay.");
    }
}
