using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDrowningStateBoundary()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x00);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.RefillHealth();
            if (lethal) _inventory.ApplyDamage(_inventory.HealthQuarters - 2);
            Vector2 start = default, direction = default;
            for (int y = 24; y < _currentRoom.Height - 16 && direction == Vector2.Zero; y += 16)
            for (int x = 24; x < _currentRoom.Width - 16 && direction == Vector2.Zero; x += 16)
            foreach (Vector2 candidate in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
            {
                Vector2 point = new(x, y);
                if (_currentRoom.IsSolid(point) || _currentRoom.GetTerrainInfo(point).Hazard != HazardType.None ||
                    _currentRoom.GetTerrainInfo(point + candidate * 16).Hazard != HazardType.Water) continue;
                start = point; direction = candidate; break;
            }
            FailIf(direction == Vector2.Zero, "Water fixture needs an original floor-to-water boundary.");
            _player.WarpTo(start);
            _player.SetLocalRespawnPosition(start);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int i = 0; i < 40 && !_player.IsDrowning; i++) StepGameplayUpdates(1, direction);
            FailIf(!_player.IsDrowning || !_player.NativeNormalStateForInteraction ||
                _player.NativeInteractionCollisionsEnabled || _player.NativePuzzleResetVulnerable ||
                _player.AcceptsRoomEntityContact || _player.AcceptsGroundInteractionContact,
                $"Walking into original water must request state02: position={_player.Position}, drowning={_player.IsDrowning}, normal={_player.NativeNormalStateForInteraction}, collisions={_player.NativeInteractionCollisionsEnabled}, flippers={_inventory.HasTreasure(TreasureId.Flippers)}.");
            _player.RequestWallSquish(0, "Drowning pending-state fixture");
            Step();
            FailIf(_player.NativeNormalStateForInteraction || _player.SideScrollSquished || !_player.Visible,
                "Pending drowning owns wLinkForceState; wall crush cannot overwrite it, and selection does not hide Link.");
            Step(); // state02 parameter04 initializes its six-update first frame.
            Step(5);
            FailIf(_player.DrownAnimationFrame != 0 || !_player.Visible, "Drown frame$d4 lasts six animation updates.");
            Step();
            FailIf(_player.DrownAnimationFrame != 1, "Drown frame$0b begins on animation update6.");
            Step(16);
            FailIf(!_player.Visible || !_player.IsDrowning || _player.NativeInteractionCollisionsEnabled,
                "The terminal animation update remains visible with disabled collisions.");
            Step();
            FailIf(_player.Visible, "The next substate5 check hides Link at the local respawn point.");
            Step();
            FailIf(_player.Visible, "Respawn counter2 retains the first invisible update.");
            Step();
            FailIf(!_player.Visible || _player.IsDrowning || _player.NativeNormalStateForInteraction ||
                _player.NativeInteractionCollisionsEnabled || _player.DeathAnimationActive || _player.IsDying != lethal,
                "Reveal applies damage but retains state02 and disabled collisions through recovery, including lethal damage.");
            Step(15);
            FailIf(_player.NativeNormalStateForInteraction || _player.NativeInteractionCollisionsEnabled || _player.DeathAnimationActive,
                "Recovery must not finish before counter16 reaches zero.");
            Step();
            FailIf(!_player.NativeNormalStateForInteraction || _player.NativeInteractionCollisionsEnabled == lethal,
                "Recovery update16 restores normal state and collisions unless death is pending.");
            Step();
            FailIf(_player.DeathAnimationActive != lethal, "Lethal drowning dispatches death only on the next normal update.");
            if (!lethal)
            {
                _player.WarpTo(start);
                for (int i = 0; i < 40 && !_player.IsDrowning; i++) StepGameplayUpdates(1, direction);
                FailIf(!_player.IsDrowning || !_player.NativeNormalStateForInteraction,
                    "After recovery, another shore approach must begin a fresh pending drowning request.");
                _player.WarpTo(start);
                Step(3);
                FailIf(_player.IsDrowning || !_player.NativeNormalStateForInteraction || !_player.NativeInteractionCollisionsEnabled,
                    "Warp cancellation must clear pending drowning and restore ordinary collision ownership.");
            }
        }
    }
}
