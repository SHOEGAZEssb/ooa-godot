using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushInitialization()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool fullQueue in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9e);
            _entities.Clear();
            _player.WarpTo(new(120, 98));
            FailIf(_currentRoom.IsSolid(_player.Position), "Push allocation fixture must use floor below $57.");
            _sound.ClearPlayRequestAudit();
            for (int i = 0; i < 20; i++)
                _pushBlocks.UpdatePushAttempt(_player.Position, Vector2I.Up, Vector2.Up);
            FailIf(!_pushBlocks.Active || _pushBlocks.NativeInitialized || _pushBlocks.Visible ||
                _currentRoom.Layout[0x57] != 0x2a || _rooms.PendingTileGraphics != 0 ||
                _rooms.BlockPushAngle != 0 || _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 0,
                "Player input must allocate reserved $14 without performing state0 side effects.");
            if (fullQueue)
                for (int i = 0; i < 31; i++)
                    FailIf(!_rooms.TrySetTile(0x11, 0xa0), "Fixture must fill the queue between player and interaction phases.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(!_pushBlocks.NativeInitialized || !_pushBlocks.Visible || _pushBlocks.ActiveTile != 0x2a ||
                    _pushBlocks.BlockTopLeft != new Vector2(112, 79.5f) || _rooms.BlockPushAngle != 0x80 ||
                    _currentRoom.Layout[0x57] != (fullQueue ? 0x2a : 0xa0) ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1,
                    "Eligible state0 must sample queue capacity, publish direction, sound and move once even during text.");
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                FailIf(_pushBlocks.BlockTopLeft != new Vector2(112, 79.5f),
                    "Initialized reserved push must freeze on later text updates.");
            }
            finally { _entities.TextActiveSource = text; }
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.Active || _currentRoom.Layout[0x47] != 0x2a ||
                _currentRoom.Layout[0x57] != (fullQueue ? 0x2a : 0xa0) ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1,
                "Push completion must occur after32 eligible updates without retrying a rejected source-floor write.");
        }
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9e);
            _entities.Clear();
            _player.WarpTo(new(120, 98));
            for (int i = 0; i < 20; i++)
                _pushBlocks.UpdatePushAttempt(_player.Position, Vector2I.Up, Vector2.Up);
            // Isolate scroll eligibility between allocation and state0.
            _entities.BeginScreenTransition(4, _currentRoom, new(240, 0), _player);
            FailIf(_pushBlocks.NativeInitialized, "Scroll setup must preserve the pending reserved state0.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!_pushBlocks.NativeInitialized || _pushBlocks.BlockTopLeft != new Vector2(112, 79.5f),
                "Pending reserved $14 must initialize and move once during scrolling.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.BlockTopLeft != new Vector2(112, 79.5f),
                "Initialized reserved $14 must stop advancing on later scroll updates.");
            _entities.FinishScreenTransition();
            FailIf(_pushBlocks.Active, "Scroll cleanup must release the initialized pending reservation.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
