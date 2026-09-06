using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDialogueScreenContext()
    {
        // Expected display rows follow initTextbox's byte subtraction and
        // initTextboxStuff's rounded tilemap address minus hardware SCY.
        (DialogueScreenContext Screen, int Y)[] cases =
        [
            (new(0x47, 0, 0, 0xf0), 96),
            (new(0x48, 0, 0, 0xf0), 24),
            (new(0xff, 0, 0, 0xf0), 24),
            (new(0x10, 0x20, 0, 0x10), 24), // byte subtraction wraps to $f0
            (new(0x10, 0xf0, 0, 0xe0), 96), // wraps to $20
            (new(0x67, 0x20, 0, 0x10), 96),
            (new(0x68, 0x20, 0, 0x10), 24),
            (new(0, 0, 0, 0), 80),
            (new(0x70, 0x23, 0, 0x13), 21), // camera rounding down
            (new(0x70, 0x24, 0, 0x14), 28), // camera rounding up
        ];
        foreach (var test in cases)
        {
            _dialogue.ShowMessage("A", test.Screen);
            FailIf(_dialogue.Position.Y != test.Y,
                $"initTextbox screen {test.Screen} expected Y={test.Y}, " +
                $"got {_dialogue.Position.Y}.");
            _dialogue.Close();
        }
        var screens = new DialogueScreenDatabase();
        _dialogue.ShowMessage("A", screens.ClearedLink(0x02));
        FailIf(_dialogue.Position.Y != 96,
            "Cleared w1Link / palace gfx state $02 lost its HUD offset.");
        _dialogue.Close();
        _dialogue.ShowMessage("A", screens.ClearedLink(0x09, 0x70), textboxFlags: 1);
        FailIf(_dialogue.Position.Y != 80 || _dialogue.TextboxFlagsForValidation != 1,
            "Cleared w1Link / tower gfx state $09 did not cancel offset $70 with SCY=$70.");
        _dialogue.Close();
        _dialogue.ShowMessage("\\pos(0)A", DialogueScreenContext.Gameplay(0));
        FailIf(_dialogue.Position.Y != 24,
            "Initial text command $0c:$20 did not override automatic lower placement.");
        _dialogue.Close();
        _dialogue.ShowMessage("\\pos(2)A", DialogueScreenContext.Gameplay(0),
            textPosition: 0, textboxFlags: 8);
        FailIf(_dialogue.Position.Y != 24,
            "TEXTBOXFLAG_DONTCHECKPOSITION did not preserve explicit top $00.");
        _dialogue.Close();
        GD.Print("Validated textbox byte boundaries/wrap, camera rounding, " +
            "imported palace/tower SCY, initial position command, and forced-position flag.");
    }
}
