using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownTrapFall()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        {
            // Death owns its animation across ordinary room loads. Start a
            // fresh gameplay instance for each terminal-state variant.
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _sound.AttachPlayRequestAudit();
            LoadValidationRoom(4, 0x9b);
            _inventory.RefillHealth();
            if (lethal) _inventory.ApplyDamage(_inventory.HealthQuarters - 2);
            _player.WarpTo(new(136, 136));
            _player.SetLocalRespawnPosition(new(136, 136));
            for (int i = 0; !_player.IsFallingInHole && i < 100; i++)
                StepGameplayUpdates(1, Vector2.Down);
            FailIf(!_player.IsFallingInHole || _currentRoom.GetPackedPosition(_player.Position) != 0x98,
                $"Trap fall fixture must enter original pit$98: batch={batched}, lethal={lethal}, position={_player.Position}, health={_inventory.HealthQuarters}, death={_player.DeathAnimationActive}.");
            // Stage completed blocks above the pit; all other probes are
            // original holes, walls or collision-buffer padding.
            foreach (int p in new[] { 0x88, 0x78 })
                _currentRoom.SetPositionTileAndCollision(new(136, (p >> 4) * 16 + 8), 0x2c, 0x0f, (long)_animationTicks);
            var trap = _entities.Entities<PuzzleTrapResetRoomEntity>().Single();
            FailIf(!trap.IsTrapped(0x98), "The fall fixture must geometrically satisfy the trap detector.");
            typeof(PuzzleTrapResetRoomEntity).GetProperty(nameof(PuzzleTrapResetRoomEntity.Counter),
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(trap, 1);
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(trap.State != 1 || trap.Counter != 30 || _entities.PlayerUpdatesFrozen ||
                _player.NativeInteractionCollisionsEnabled || _player.NativeNormalStateForInteraction ||
                _sound.PlayRequestsFor(SoundId.SndError) != 0,
                "Native state02 hole falling must reject trap resets and normal-state collision handlers.");
            _currentRoom.SetPositionTileAndCollision(new(136, 152), 0x2e, 0x0f, (long)_animationTicks);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!_player.IsFallingInHole || _player.SideScrollSquished,
                "A closing block must not replace state02 with a wall-squish request.");
            // Restore the original respawn floor before Link returns there.
            _currentRoom.SetPositionTileAndCollision(new(136, 136), 0xa0, 0, (long)_animationTicks);
            for (int i = 0; _player.IsFallingInHole && i < 80; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.IsFallingInHole || _player.NativeInteractionCollisionsEnabled ||
                _player.NativeNormalStateForInteraction || _player.IsDying != lethal ||
                _player.DeathAnimationActive,
                "Hole recovery must retain state02 and disabled collisions after Link reappears.");
            StepGameplayUpdates(15, Vector2.Zero, batched: batched);
            FailIf(_player.NativeInteractionCollisionsEnabled || _player.NativeNormalStateForInteraction ||
                _player.DeathAnimationActive,
                "The native16-update recovery must remain locked through update15.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.NativeInteractionCollisionsEnabled == lethal || !_player.NativeNormalStateForInteraction ||
                _player.DeathAnimationActive,
                "Recovery update16 must select normal state; pending death still gates interaction collisions.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.DeathAnimationActive != lethal,
                "Only the next normal-state update may consume lethal pit damage and dispatch death.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
