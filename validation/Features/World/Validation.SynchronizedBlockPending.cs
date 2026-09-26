using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockPending()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var allocate = typeof(RoomEntityManager).GetMethod("TryCreateSynchronizedBlock", flags)!;
        foreach (bool batched in new[] { false, true })
        foreach (byte tile in new byte[] { 0x2a, 0xa0, 0xda })
        {
            LoadValidationRoom(4, 0x9e);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            _player.WarpTo(new(120, 136));
            // Isolate a lower-slot child awaiting its next interaction pass.
            FailIf(!(bool)allocate.Invoke(_entities, [(byte)0x37, 0])!,
                "Pending synchronized block must allocate its native interaction slot.");
            var block = _entities.Entities<PushBlockController>().Single();
            int slot = _entities.InteractionSlot(block);
            FailIf(block.Active, "Allocated synchronized block must remain state0 until dispatch.");
            var target = _rooms.GetRoom(4, 0xbc);
            target.SetPositionTileAndCollision(new(120, 56), tile, null, (long)_animationTicks);
            target.SetUnderlyingStorageMetatile(0x37, 0xa0);
            byte destination = target.Layout[0x27];
            _rooms.SetLoadedRoom(4, target);
            _entities.BeginScreenTransition(4, target, new(240, 0), _player);
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!block.Active || !block.NativeInitialized || block.ActiveTile != tile ||
                block.BlockTopLeft != new Vector2(112, 47.5f) || target.Layout[0x37] != 0xa0 ||
                _rooms.BlockPushAngle != 0x80 || _entities.InteractionSlot(block) != slot ||
                _sound.PlayRequestsFor(SoundId.SndMoveBlock) != 1,
                "Scroll state0 must read the current tile, initialize native $14, replace its floor and move once.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(block.BlockTopLeft != new Vector2(112, 47.5f) ||
                _sound.PlayRequestsFor(SoundId.SndMoveBlock) != 1 || target.Layout[0x27] != destination,
                "After state0 falls through, the initialized outgoing block must freeze without destination writes.");
            _entities.FinishScreenTransition();
            FailIf(_entities.OutgoingEntities<PushBlockController>().Count != 0,
                "Scroll cleanup must release the child that initialized during scrolling.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
