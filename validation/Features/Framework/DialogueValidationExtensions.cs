using Godot;

namespace oracleofages;

internal static class DialogueValidationExtensions
{
    // ActionRelease does not clear Godot's just-pressed edge until the next
    // host frame. Synchronous scenarios must supply their own neutral sample.
    internal static bool AdvanceNeutralCharacterClockForValidation(this DialogueBox dialogue, double delta)
    {
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
            pressed: [], justPressed: [], movement: Vector2.Zero));
        try
        {
            return dialogue.AdvanceCharacterClockForValidation(delta);
        }
        finally
        {
            Input.EndOriginalUpdate();
        }
    }
}
