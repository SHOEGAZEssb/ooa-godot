using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSideScrollPitRespawn()
    {
        var checkpoints = new List<(Vector2 Position, int Health, bool Visible, bool Airborne)>();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            LoadValidationRoom(6, 0x97);
            _inventory.RefillHealth();
            // rooms/ages/large/room0497.bin: left ledge at row $06,
            // pit $f4 across columns $04-$0a of row $0a.
            _player.WarpTo(new Vector2(56, 89));
            FailIf(_collision.Collides(_player.Position) ||
                _currentRoom.GetMetatile(new Vector2(56, 96)) != 0x26 ||
                _currentRoom.GetMetatile(new Vector2(80, 168)) != 0xf4,
                "6:97 pit fixture lost its source ledge/pit geometry.");
            // loadRoomCollisions writes $ff beyond the bottom; the Link
            // special-collision mask for low nibble $0f is zero.
            FailIf((_collision.AdjacentWallsBitset(new Vector2(80, 169)) & 0x30) != 0,
                "6:97's bottom border incorrectly acts as solid ground.");
            int checkpoint = 0;
            void Step(int count, Vector2 move = default)
            {
                StepGameplayUpdates(count, move, batched: batched);
                var state = (_player.PrecisePosition, _inventory.HealthQuarters,
                    _player.Visible, _player.SideScrollAirborne);
                if (!batched) checkpoints.Add(state);
                else FailIf(checkpoint >= checkpoints.Count || checkpoints[checkpoint] != state,
                    $"6:97 pit recovery diverged under batched updates at checkpoint {checkpoint}.");
                checkpoint++;
            }
            for (int fall = 0; fall < 2; fall++)
            {
                int health = _inventory.HealthQuarters;
                Step(12, Vector2.Right);
                FailIf(!_player.SideScrollAirborne || _player.Position.X < 64,
                    "6:97 Link did not walk off the actual left ledge.");
                // Brake in the gap before the first moving platform at X=$58.
                Step(8, Vector2.Left);
                Step(16);
                FailIf(!_player.Visible || _inventory.HealthQuarters != health,
                    "6:97 pit applied damage before Link reached the bottom tile.");
                int updates = 0;
                while (_player.Visible && updates++ < 60) Step(1);
                FailIf(_player.Visible || _inventory.HealthQuarters != health ||
                    _player.Position != new Vector2(56, 89) || IsTransitioning,
                    $"6:97 fall {fall} failed to hide/respawn Link at the saved ledge: {_player.Position}.");
                Step(1);
                FailIf(_player.Visible || _inventory.HealthQuarters != health,
                    "6:97 respawn counter $02 revealed/damaged Link one update early.");
                Step(1);
                FailIf(!_player.Visible || _inventory.HealthQuarters != health - 2 ||
                    _player.SideScrollAirborne,
                    "6:97 respawn zero update must reveal Link and remove two health quarters.");
                Vector2 respawn = _player.PrecisePosition;
                Step(16, Vector2.Right);
                FailIf(_player.PrecisePosition != respawn,
                    "6:97 respawn recovery must retain movement ownership for $10 updates.");
            }
        }
    }
}
