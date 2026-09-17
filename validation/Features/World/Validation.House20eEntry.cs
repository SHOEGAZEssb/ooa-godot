using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHouse20eEntry()
    {
        foreach (bool savedNayru in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
            _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagSavedNayru, savedNayru);
            LoadValidationRoom(0, 0x56);
            _player.WarpTo(new Vector2(0x88, 0x38));
            for (int repeat = 0; repeat < 3; repeat++)
            {
                FailIf(_collision.Collides(_player.Position),
                    "House 2:0e entry fixture must approach through clear ground in 0:56.");
                for (int update = 0; update < 40 && !IsTransitioning; update++)
                    StepGameplayUpdates(1, Vector2.Up);
                FailIf(!IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0x0e,
                    $"House 0:56 doorway did not begin entry to 2:0e: {_activeGroup}:{_currentRoom.Id:x2}, {_player.Position}.");
                // $ff / transition $03 / parameter $09 enters at x=$50,
                // y=$80, then walks up $1c pixels before releasing Link.
                // The triggering gameplay update also advances the warp once.
                FailIf(_player.Position != new Vector2(0x50, 0x7f),
                    $"House 2:0e load retained the source position: {_player.Position}.");
                StepGameplayUpdates(120, Vector2.Zero, batched: batched);
                FailIf(IsTransitioning || _player.Position != new Vector2(0x50, 0x64) ||
                    !_inventoryMenu.CanOpenForValidation || _gameplayPause.IsLeased,
                    $"House 2:0e entry did not release Link/menu at $50,$64: {_player.Position}, transition={IsTransitioning}.");
                Vector2 arrival = _player.Position;
                StepGameplayUpdates(4, Vector2.Right, batched: batched);
                FailIf(_player.Position.X <= arrival.X || _player.Position.Y != arrival.Y,
                    "House 2:0e ignored movement after entry.");
                StepGameplayUpdates(4, Vector2.Left, batched: batched);
                StepGameplayUpdates(1, Vector2.Zero, held: ["inventory"], pressed: ["inventory"]);
                StepGameplayUpdates(60, Vector2.Zero, batched: batched);
                FailIf(!_inventoryMenu.IsOpen, "House 2:0e ignored the inventory button after entry.");
                StepGameplayUpdates(1, Vector2.Zero, held: ["inventory"], pressed: ["inventory"]);
                StepGameplayUpdates(60, Vector2.Zero, batched: batched);
                FailIf(_inventoryMenu.IsActive || _gameplayPause.IsLeased,
                    "House 2:0e retained the menu pause after closing inventory.");
                for (int update = 0; update < 50 && !IsTransitioning; update++)
                    StepGameplayUpdates(1, Vector2.Down);
                FailIf(!IsTransitioning, "House 2:0e exit did not begin through its south doorway.");
                StepGameplayUpdates(120, Vector2.Zero, batched: batched);
                FailIf(IsTransitioning || _activeGroup != 0 || _currentRoom.Id != 0x56,
                    "House 2:0e exit did not return control in 0:56.");
                StepGameplayUpdates(12, Vector2.Down);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        GD.Print("Validated 0:56 -> 2:0e doorway entry/exit/re-entry, both NPC story states, batched host updates, collection, arrival and input release.");
    }
}
