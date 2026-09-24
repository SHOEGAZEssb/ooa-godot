using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushDestination()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool clearBeforeDeadline in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9e);
            _player.WarpTo(new(120, 120));
            Vector2 destination = new(120, 72);
            // Isolate a changing destination collision ahead of the original
            // statue $57. Link still approaches through original floor $67/$77.
            _currentRoom.SetPositionTileAndCollision(destination, 0xa0, 0x0f, (long)_animationTicks);
            for (int i = 0; _pushBlocks.RemainingPushFrames != 19 && i < 80; i++)
            {
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(_currentRoom.IsSolid(_player.Position), "Blocked-destination approach must stay on floor.");
            }
            FailIf(_pushBlocks.RemainingPushFrames != 19 || _pushBlocks.Active,
                "nextToPushableBlock must count contact before checking the destination.");
            StepGameplayUpdates(18, Vector2.Up, batched: batched);
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "A blocked destination must retain the push countdown through update19.");
            if (clearBeforeDeadline)
                _currentRoom.SetPositionTileAndCollision(destination, 0xa0, 0, (long)_animationTicks);
            StepGameplayUpdates(1, Vector2.Up);
            if (!clearBeforeDeadline)
            {
                FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 20,
                    "A failed destination check at update20 must reset the countdown.");
                _currentRoom.SetPositionTileAndCollision(destination, 0xa0, 0, (long)_animationTicks);
                StepGameplayUpdates(19, Vector2.Up, batched: batched);
                FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1,
                    "Clearing after the failed check must require a fresh20 contact updates.");
                StepGameplayUpdates(1, Vector2.Up);
            }
            FailIf(!_pushBlocks.Active || !_pushBlocks.NativeInitialized || _currentRoom.Layout[0x57] != 0xa0,
                "A clear destination at the countdown boundary must start the native push that update.");
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.Active || _currentRoom.Layout[0x47] != 0x2a,
                "The accepted push must finish normally after32 movement updates.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
