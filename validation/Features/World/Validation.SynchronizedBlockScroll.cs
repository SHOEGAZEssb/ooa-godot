using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockScroll()
    {
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(4, 0x9e);
            _player.WarpTo(new(120, 120));
            for (int i = 0; !_pushBlocks.Active && i < 80; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_pushBlocks.Active || !_pushBlocks.NativeInitialized,
                "A real statue push must initialize reserved $14 before the scroll fixture.");
            var partner = _entities.Entities<PushBlockController>().Single();
            Vector2 primaryPoint = _pushBlocks.BlockTopLeft, partnerPoint = partner.BlockTopLeft;
            var source = _currentRoom;
            byte primaryTarget = source.Layout[0x47], partnerTarget = source.Layout[0x27];
            // Isolate departure during movement; ordinary doorway approaches
            // and statue routes have separate checks. Use the real south exit.
            _player.WarpTo(new(120, 156));
            FailIf(source.IsSolid(_player.Position) || !_rooms.TryGetNeighbor(Vector2I.Down, out _),
                "Synchronized-block scroll requires floor and the imported south neighbor.");
            _rooms.TryGetNeighbor(Vector2I.Down, out int target);
            _transitions.BeginScroll(_player, Vector2I.Down, target);
            FailIf(!_pushBlocks.Active || !_pushBlocks.Visible ||
                _entities.OutgoingEntities<PushBlockController>().Count != 1,
                "Room identity handoff must retain both initialized outgoing push sprites.");
            _sound.ClearPlayRequestAudit();
            for (int i = 0; i < 10; i++)
            {
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(!_transitions.ScrollActive || !_pushBlocks.Active ||
                    _pushBlocks.BlockTopLeft != primaryPoint || partner.BlockTopLeft != partnerPoint ||
                    _pushBlocks.Position != partner.Position ||
                    _sound.PlayRequestsFor(SoundId.SndMoveBlock) != 0,
                    "Reserved and dynamic blocks must freeze equally and share the outgoing scroll offset.");
            }
            for (int i = 0; _transitions.ScrollActive && i < 100; i++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_transitions.ScrollActive || _pushBlocks.Active || _pushBlocks.Visible ||
                _pushBlocks.Position != Vector2.Zero || _entities.OutgoingEntities<PushBlockController>().Count != 0,
                "Scroll completion must clear reserved and dynamic push objects and their drawing offsets.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(source.Layout[0x47] != primaryTarget || source.Layout[0x27] != partnerTarget,
                "Cleared outgoing blocks must not complete delayed destination writes after arrival.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
