using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSpikeTileLanding()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, 0x65);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(TreasureId.Feather);
            // Floor $52 to spike $54 through the actual passable row $05.
            _player.WarpTo(new(44, 82));
            int health = _player.HealthQuarters;
            StepGameplayUpdates(1, Vector2.Right, ["attack"], ["attack"], batched);
            FailIf(!_player.TopDownAirborne, "Room 5:65 Feather must jump from floor $52.");
            int update = 1;
            void CheckAir()
            {
                update++;
                if (_player.TopDownAirborne)
                    FailIf(_player.HealthQuarters != health || _player.KnockbackFrames != 0,
                        $"Room 5:65 spikes must not damage airborne Link on jump update {update}.");
            }
            StepGameplayUpdates(27, Vector2.Right, batched: batched, afterUpdate: CheckAir);
            FailIf(!_player.TopDownAirborne || _player.HealthQuarters != health,
                "Room 5:65 Link must remain unharmed before the spike landing.");
            for (int i = 0; _player.TopDownAirborne && i < 4; i++)
                StepGameplayUpdates(1, Vector2.Right, batched: batched);
            FailIf(_player.TopDownAirborne || _player.HealthQuarters != health - 2 ||
                _player.InvincibilityFrames != 39 || _player.KnockbackFrames != 10 ||
                _terrain.GetActiveTerrain(_player.Position).Terrain.Tile != 0x60,
                $"Room 5:65 landing on spike $54 must damage before ground motion, retain 10 recoil updates, " +
                $"and start invincibility: XY={_player.Position}, HP={_player.HealthQuarters}, " +
                $"recoil={_player.KnockbackFrames}, inv={_player.InvincibilityFrames}.");
            StepGameplayUpdates(10, Vector2.Zero, batched: batched);
            FailIf(_player.KnockbackFrames != 0 || _player.HealthQuarters != health - 2,
                "Room 5:65 landing recoil must finish without repeated damage during invincibility.");

            foreach (string gate in new[] { "objects", "text", "interaction" })
            {
                ReinitializeGameplayForValidation();
                LoadValidationRoom(5, 0x65);
                _entities.Clear();
                _player.WarpTo(new(72, 26));
                StepGameplayUpdates(1, Vector2.Down, batched: batched);
                health = _player.HealthQuarters;
                if (gate == "objects")
                    _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x81);
                else if (gate == "text")
                    _dialogue.ShowMessage("Spike dispatch gate.", _player.Position.Y);
                else
                    _player.ApplyInteractionInvincibility(4);
                StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                FailIf(_player.HealthQuarters != health || _player.KnockbackFrames != 0,
                    $"Room 5:65 {gate} gate must suppress spike damage through four gameplay updates.");
                _entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                _dialogue.Close();
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(_player.HealthQuarters != health - 2 || _player.InvincibilityFrames != 39,
                    $"Room 5:65 must resume spike damage on the first eligible update after the {gate} gate clears.");
            }
        }
    }
}
