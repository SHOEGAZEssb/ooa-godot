using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFloorDoorRespawnState()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9b);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(new(136, 136));
                _player.SetLocalRespawnPosition(new(136, 136));
                Step(16, Vector2.Up);
                FailIf(_collision.Collides(_player.Position) || _player.Position == _player.LocalRespawnPosition,
                    "Respawn fixture must leave the Crown4:9b anchor through clear floor.");
                Vector2 before = _player.Position;
                int health = _player.HealthQuarters;
                // State01 consumes force-state before $81, and state02 has
                // no $81 gate. Repeat while the real controller owns that mask.
                if (repeat == 1) _entities.LockSmogLinkAndMenu();
                _player.BeginFloorDoorRespawn();
                FailIf(_player.Position != before || !_player.Visible || !_player.NativeNormalStateForInteraction,
                    "respawnLink only publishes a force-state request; Link must remain normal on the request update.");
                Step();
                FailIf(_player.Position != before || !_player.Visible || _player.NativeNormalStateForInteraction,
                    "The first Link update must consume state02 without moving or hiding Link.");
                Step();
                FailIf(_player.Position != new Vector2(136, 136) || _player.Visible ||
                    _player.FloorDoorRespawnCounter != 2 || _player.NativeNormalStateForInteraction,
                    "The following state02 dispatch must initialize the local respawn and counter02.");
                Step();
                FailIf(_player.Visible || _player.HealthQuarters != health,
                    "The first invisible wait decrement must not reveal or damage Link.");
                Step();
                FailIf(!_player.Visible || _player.HealthQuarters != health - 2 ||
                    _player.NativeNormalStateForInteraction,
                    "The second wait decrement must reveal Link and apply raw $fc damage, retaining state02.");
                Step(15, Vector2.Left);
                FailIf(_player.Position != new Vector2(136, 136) || _player.NativeNormalStateForInteraction,
                    "State02 must suppress movement through recovery update15.");
                Step(movement: Vector2.Left);
                FailIf(_player.Position != new Vector2(136, 136) || !_player.NativeNormalStateForInteraction ||
                    _player.IsFloorDoorRespawning,
                    "Recovery update16 must restore normal state without executing movement.");
                Step();
                FailIf(!_player.NativeNormalStateForInteraction, "Completed floor-door recovery must remain normal.");
                _player.Heal(2);
            }
            _entities.Clear();
            _player.BeginFloorDoorRespawn();
            _player.WarpTo(new(136, 136));
            Step();
            FailIf(_player.IsFloorDoorRespawning || !_player.Visible || !_player.NativeNormalStateForInteraction,
                "An explicit warp must cancel the pending floor-door respawn.");
        }
        foreach (bool batched in new[] { false, true })
        foreach (bool shield in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9b);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(shield ? TreasureId.Shield : TreasureId.Sword, 1);
            _inventory.EquipA(shield ? TreasureId.Shield : TreasureId.Sword);
            _inventory.EquipB(TreasureId.None);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(new(136, 136));
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                FailIf(shield ? !_player.IsUsingShield : !_player.IsAttacking,
                    "The respawn item fixture must create its parent through normal button input.");
                int swordFrame = _player.SwordStateFrame;
                _player.BeginFloorDoorRespawn();
                FailIf(shield ? !_player.IsUsingShield : !_player.IsAttacking,
                    "A respawn request must retain existing item parents.");
                // Release the button: checkUseItems is skipped on consumption,
                // so even the held shield must retain its preceding value.
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(shield ? !_player.IsUsingShield :
                    !_player.IsAttacking || _player.SwordStateFrame != swordFrame,
                    "Consuming state02 must preserve shield state and freeze the sword parent until initialization.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_player.IsUsingShield || _player.IsAttacking,
                    "State02 initialization must cancel shield and sword parents together.");
                StepGameplayUpdates(18, Vector2.Zero, batched: batched);
                FailIf(!_player.NativeNormalStateForInteraction || _player.IsUsingShield || _player.IsAttacking,
                    "Recovery must finish without recreating a released item parent.");
                _player.Heal(2);
            }
        }
    }
}
