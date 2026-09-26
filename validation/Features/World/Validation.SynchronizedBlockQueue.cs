using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockQueue()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int queued in new[] { 0, 29, 30, 31 })
        {
            LoadValidationRoom(4, 0x9e);
            _player.WarpTo(new(120, 120));
            for (int i = 0; _pushBlocks.RemainingPushFrames != 1 && i < 80; i++)
            {
                StepGameplayUpdates(1, Vector2.Up);
                FailIf(_currentRoom.IsSolid(_player.Position), "Queue fixture must approach statue $57 through original floor.");
            }
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "Queue fixture must stop one contact update before the push.");
            for (int i = 0; i < queued; i++)
                FailIf(!_rooms.TrySetTile(0x11, 0xa0), "Fixture queue fill must succeed.");
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Up);
            // setTile rejects before changing layout when all31 entries are
            // occupied. $bd therefore sees the rejected primary source too.
            int firstCount = queued == 31 ? 2 : 1;
            var first = _entities.Entities<PushBlockController>().OrderBy(b => _entities.InteractionSlot(b)).ToArray();
            FailIf(!_pushBlocks.Active || first.Length != firstCount ||
                _currentRoom.Layout[0x57] != (queued == 31 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x37] != (queued >= 30 ? 0x2a : 0xa0),
                "Queue capacity must determine source-floor writes before the synchronizer scans.");
            for (int i = 0; i < first.Length; i++)
                FailIf(first[i].BlockTopLeft != new Vector2(112, (queued == 31 && i == 0 ? 80 : 48) - 0.5f),
                    "Rejected source tiles must allocate in descending order and move in the same update.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<PushBlockController>().Count != firstCount,
                "Synchronizer state2 must not scan while graphics queue capacity becomes available.");
            StepGameplayUpdates(1, Vector2.Zero);
            int delayed = queued == 31 ? 2 : queued == 30 ? 1 : 0;
            FailIf(_entities.Entities<PushBlockController>().Count != firstCount + delayed ||
                _currentRoom.Layout[0x57] != 0xa0 || _currentRoom.Layout[0x37] != 0xa0 ||
                _sound.PlayRequestsFor(SoundId.SndMoveBlock) != 1 + firstCount + delayed,
                "The next state1 scan must allocate duplicates for rejected source writes, then clear those sources.");
            StepGameplayUpdates(29, Vector2.Zero, batched: batched);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != delayed ||
                _currentRoom.Layout[0x47] != 0x2a || _currentRoom.Layout[0x27] != 0x2a,
                "Initial blocks must finish on update32 while delayed duplicate blocks retain their counters.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<PushBlockController>().Count != delayed,
                "Delayed duplicate blocks must remain alive on their movement update31.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.Layout[0x47] != 0x2a || _currentRoom.Layout[0x27] != 0x2a,
                "Delayed duplicate blocks must complete on global update34 without changing destination identity.");
        }
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1, 2 })
        {
            LoadValidationRoom(4, 0x9e);
            _player.WarpTo(new(120, 120));
            for (int i = 0; !_pushBlocks.Active && i < 80; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_pushBlocks.Active, "Destination queue fixture must begin an actual statue push.");
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(!_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 1,
                "Both statue objects must remain active through movement update31.");
            for (int i = 0; i < 31 - free; i++)
                FailIf(!_rooms.TrySetTile(0x11, 0xa0), "Completion queue fill must succeed.");
            StepGameplayUpdates(1, Vector2.Zero);
            // Reserved $d1 completes before dynamic $d4. Neither handler
            // checks setTile's failure result before deleting itself.
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.Layout[0x47] != (free >= 1 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x27] != (free == 2 ? 0x2a : 0xa0),
                "Final queue capacity must favor the reserved block; failed destination writes still delete their objects.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(_currentRoom.Layout[0x47] != (free >= 1 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x27] != (free == 2 ? 0x2a : 0xa0),
                "Draining graphics must not retry a rejected push destination write.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
