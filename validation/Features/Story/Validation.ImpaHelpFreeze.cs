using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateImpaHelpFreeze()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            var impa = _roomEvents.Get<ImpaIntroEvent>();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                LoadValidationRoom(0, 0x7a);
                // Approach through this clear north-facing corridor; reach the
                // trigger by movement, preserving Link's ordinary state01.
                _player.WarpTo(new Vector2(0x48, 8));
                _runtimeState.SetWramByte(WramAddress.wActiveTilePos, 0x04);
                _runtimeState.SetWramByte(WramAddress.wActiveTileIndex, _currentRoom.GetMetatile(new(72, 13)));
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(_player.Position != new Vector2(0x48, 8) || _dialogue.IsOpen,
                    "Idle Link advanced into the help trigger.");
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], batched: batched);
                FailIf(_dialogue.IsOpen || impa.DisablesLink,
                    "INTERAC $6b:$00 triggered at Y=$07 instead of below it.");
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], batched: batched);
                FailIf(!_dialogue.IsOpen || !impa.DisablesLink || !impa.MenusDisabled ||
                    _player.CutsceneControlled || !_player.NativeNormalStateForInteraction,
                    $"Impa help: attempt={attempt}, Y={_player.Position.Y}, text={_dialogue.IsOpen}, disabled={impa.DisablesLink}, menu={impa.MenusDisabled}, control={_player.CutsceneControlled}, waiting={impa.HelpWaitingAtEdge}.");
                StepGameplayUpdates(2, Vector2.Right, ["inventory"], ["inventory"], batched: batched);
                FailIf(_player.Position != new Vector2(0x48, 6) || _player.FacingVector != Vector2I.Up ||
                    _inventoryMenu.IsActive || impa.Counter != 30,
                    "Help text allowed Link/menu input or advanced its post-text counter.");
                _dialogue.Close();
                if (attempt == 0)
                {
                    LoadValidationRoom(0, 0x47);
                    FailIf(impa.DisablesLink || impa.MenusDisabled || _player.CutsceneControlled,
                        "Cancelling help retained the Link/menu freeze.");
                    continue;
                }
                StepGameplayUpdates(29, Vector2.Right, batched: batched);
                FailIf(impa.Counter != 1 || !impa.DisablesLink || _player.Position != new Vector2(0x48, 6),
                    "Help freeze did not survive the 29th post-text update.");
                StepGameplayUpdates(1, Vector2.Right, batched: batched);
                FailIf(impa.DisablesLink || _player.NativeCutsceneControlled || impa.Counter != 8 ||
                    !_saveData.HasRoomFlag(0, 0x7a, 0x40),
                    "Help completion lost its flag or confused simulated input with native cutscene Link.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x6a,
                    "Help's simulated Up did not hand off through the actual gameplay loop.");
                FailIf(_player.NativeCutsceneControlled || impa.FakeOctoroks.Count != 0,
                    "initializeRoom ran Impa's state 0 before the next object pass.");
            }
        }
        GD.Print("Validated help trigger, Link-only freeze, menu gate, cancellation, repeat and simulated-input handoff.");
    }
}
