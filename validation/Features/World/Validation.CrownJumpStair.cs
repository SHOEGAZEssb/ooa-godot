using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownJumpStair()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            _entities.Clear();
            _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
            _inventory.EquipA(InventoryState.ItemFeather);
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Jump/stair fixture must use Crown4:a1's unchanged stair and approach.");
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Up, ["attack"], ["attack"]);
            StepGameplayUpdates(14, Vector2.Up, batched: batched);
            FailIf(!_player.TopDownAirborne || IsTransitioning || _player.Position.Y > 25,
                "A real Feather jump must reach the stair while its air-state gate rejects activation.");
            int updates = 0;
            while (_player.TopDownAirborne && updates++ < 80)
            {
                FailIf(IsTransitioning || _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                    "An airborne update must not select the stair or emit an entrance sound.");
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(_player.TopDownAirborne || !IsTransitioning ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                "linkUpdateInAir clears the air state before func_60e9: the stair must activate on the landing update.");
        }
        ReinitializeGameplayForValidation();
    }
}
