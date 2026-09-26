using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownTrapJump()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte scratch in new byte[] { 0, 0x10 })
        {
            LoadValidationRoom(4, 0x9b);
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(TreasureId.Feather);
            _player.WarpTo(new(136, 40));
            FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.Layout[0x18] != 0xf7,
                "Trap boundary fixture must start on original floor $28 below pit tile $18.");
            StepGameplayUpdates(1, Vector2.Up, ["attack"], ["attack"]);
            for (int i = 0; _currentRoom.GetPackedPosition(_player.Position) != 0x18 && i < 20; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_player.TopDownAirborne || _currentRoom.GetPackedPosition(_player.Position) != 0x18,
                "An actual feather jump must reach the original top pit border before scanning.");
            // Complete two block destinations behind Link after takeoff.
            // All other room probes are original walls or the pit border.
            foreach (int p in new[] { 0x28, 0x38 })
                _currentRoom.SetPositionTileAndCollision(new(136, (p >> 4) * 16 + 8), 0x2c, 0x0f, (long)_animationTicks);
            var trap = _entities.Entities<PuzzleTrapResetRoomEntity>().Single();
            typeof(PuzzleTrapResetRoomEntity).GetProperty(nameof(PuzzleTrapResetRoomEntity.Counter),
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(trap, 3);
            _entities.RuntimeState.SetWramByte(0xcef8, scratch);
            _sound.ClearPlayRequestAudit();
            try
            {
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(trap.State != 1 || trap.Counter != 2 || _entities.PlayerUpdatesFrozen,
                    "Airborne trap detection must wait for its scan deadline.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                bool trapped = scratch != 0;
                FailIf(!_player.TopDownAirborne || trap.State != (trapped ? 2 : 1) ||
                    trap.Counter != (trapped ? 60 : 30) || _entities.PlayerUpdatesFrozen != trapped ||
                    _entities.PlayerMenusDisabled != trapped ||
                    _entities.WarpTilesDisabled != trapped ||
                    _sound.PlayRequestsFor(SoundId.SndError) != (trapped ? 1 : 0),
                    "The live airborne scan must use raw $cef8: zero rejects; nonzero starts the60-update reset lock.");
                Vector2 point = _player.PrecisePosition;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(trap.Counter != (trapped ? 58 : 28) ||
                    trapped && _player.PrecisePosition != point ||
                    _sound.PlayRequestsFor(SoundId.SndError) != (trapped ? 1 : 0),
                    "Trap state must retain its timer and request the error cue only once.");
            }
            finally { _entities.RuntimeState.SetWramByte(0xcef8, 0); }
        }
        LoadValidationRoom(0, 0x60);
    }
}
