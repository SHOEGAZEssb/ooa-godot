using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLedgeInteractionState()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9a);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            Vector2? start = null;
            for (int y = 8; y < _currentRoom.Height - 32 && start is null; y++)
            for (int x = 8; x < _currentRoom.Width - 8 && start is null; x++)
            {
                Vector2 point = new(x, y);
                if (!_collision.Collides(point) &&
                    _currentRoom.GetMetatile(point + new Vector2(-3, 8)) is 0xb0 or 0xc1 &&
                    _currentRoom.GetMetatile(point + new Vector2(2, 8)) is 0xb0 or 0xc1)
                    start = point;
            }
            FailIf(start is null, "Crown room4:9a must provide an original downward ledge approach.");
            void Step(int count = 1, Vector2 move = default) =>
                StepGameplayUpdates(count, move, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(start!.Value);
                FailIf(_collision.Collides(_player.Position) ||
                    !_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction,
                    "Ledge state fixture must approach the original cliff from normal floor.");
                int approach = 0;
                while (_player.LedgeJumpPhase == LedgeJumpState.None && approach++ < 20)
                    Step(move: Vector2.Down);
                FailIf(_player.LedgeJumpPhase != LedgeJumpState.Airborne ||
                    _player.NativeNormalStateForInteraction || !_player.NativeInAirForInteraction,
                    "The real cliff movement dispatch must immediately enter state12 with wLinkInAir=$81.");
                Step(28);
                FailIf(_player.LedgeJumpPhase != LedgeJumpState.Airborne ||
                    _player.NativeNormalStateForInteraction || !_player.NativeInAirForInteraction,
                    "State12 and its in-air byte must persist through airborne update28.");
                Step();
                FailIf(_player.LedgeJumpPhase != LedgeJumpState.None ||
                    !_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction,
                    "Landing update29 must clear wLinkInAir and restore state01 together.");
                Step();
                FailIf(!_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction,
                    "Ledge state must remain normal after landing.");
            }
            _player.WarpTo(start!.Value);
            for (int i = 0; i < 20 && _player.LedgeJumpPhase == LedgeJumpState.None; i++)
                Step(move: Vector2.Down);
            FailIf(_player.LedgeJumpPhase == LedgeJumpState.None, "Cancellation fixture must start a real ledge jump.");
            _player.WarpTo(start!.Value);
            Step();
            FailIf(!_player.NativeNormalStateForInteraction || _player.NativeInAirForInteraction,
                "Warp cancellation must release the ledge state and air gate.");
        }
    }
}
