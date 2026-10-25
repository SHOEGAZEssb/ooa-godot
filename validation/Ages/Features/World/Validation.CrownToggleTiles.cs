using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleTiles()
    {
        CompareToggleFloorEntryRom();
        GD.Print("Compared native cached toggle-floor entry/reset, whole-byte state, original dungeon/group eligibility, literal replacement pairs and four-tile header pixels.");
    }
}
