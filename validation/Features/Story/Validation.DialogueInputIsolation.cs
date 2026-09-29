using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDialogueClockInputIsolation()
    {
        // Reproduce serial CI's prior-scenario edge in the same host frame.
        // code/textbox.s checks wKeysJustPressed A/B before decrementing
        // the character timer, so leaking this edge skips an entire line.
        foreach (string button in new[] { "attack", "item" })
        {
            Input.ActionPress(button);
            Input.ActionRelease(button);
            FailIf(!Godot.Input.IsActionJustPressed(button),
                $"Dialogue input isolation fixture lost its retained {button} edge.");
            _dialogue.MessageSpeed = 0;
            _dialogue.ShowMessage("AB", _player.Position.Y);
            _dialogue.AdvanceNeutralCharacterClockForValidation(6.0 / 60.0);
            FailIf(_dialogue.VisibleGlyphCount != 0 || Input.OriginalUpdateActive,
                $"Retained {button} edge leaked into neutral dialogue timing or its sample escaped.");
            _dialogue.AdvanceNeutralCharacterClockForValidation(1.0 / 60.0);
            FailIf(_dialogue.VisibleGlyphCount != 1,
                $"Neutral dialogue with a retained {button} edge missed character update 7.");

            // Explicit original-update input must still skip the line. The
            // fixture isolates ambient input without suppressing gameplay A/B.
            _dialogue.ShowMessage("AB", _player.Position.Y);
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
                pressed: [button], justPressed: [button], movement: Vector2.Zero));
            try
            {
                _dialogue.AdvanceCharacterClockForValidation(1.0 / 60.0);
                FailIf(_dialogue.VisibleGlyphCount != 2,
                    $"Explicit {button} edge failed to skip the dialogue line.");
            }
            finally
            {
                Input.EndOriginalUpdate();
                _dialogue.Close();
            }
        }
        GD.Print("Validated neutral dialogue timing with retained host A/B edges and explicit original-update line skipping.");
    }
}
