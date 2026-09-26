using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorScroll()
    {
        foreach (bool batch in new[] { false, true })
        foreach (int priorUpdates in new[] { 0, 1, 2 })
        {
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            _player.WarpTo(new(220, 136));
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            for (int i = 0; i < 10; i++)
                _keyDoors.UpdatePushAttempt(_player.Position, Vector2I.Right, Vector2.Right);
            void Step(int n = 1) => StepGameplayUpdates(n, Vector2.Zero, batched: batch);
            if (priorUpdates != 0) Step(priorUpdates);
            var source = _currentRoom;
            Vector2 door = new(232, 136);
            int counter = _keyDoors.OpeningCounter;
            // Isolate scroll lifetime after the exit collision has cleared.
            // Key-door approach and the collision-clear branch have separate
            // checks; this is not a route through an unopened door.
            source.SetPositionTileAndCollision(door, 0xa0, null, (long)_animationTicks);
            FailIf(source.IsSolid(new(234, 136)) || !_rooms.TryGetNeighbor(Vector2I.Right, out _),
                "Reserved-door scroll requires a passable source edge and imported neighbor.");
            _rooms.TryGetNeighbor(Vector2I.Right, out int target);
            _transitions.BeginScroll(_player, Vector2I.Right, target);
            FailIf(!_transitions.ScrollActive || !_keyDoors.Opening,
                "Room identity handoff must retain the outgoing reserved door until its native deletion phase.");
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(_keyDoors.Opening != (priorUpdates != 0) ||
                priorUpdates != 0 && _keyDoors.OpeningCounter != counter,
                "First scroll update must delete outgoing state0 while retaining initialized reserved doors.");
            Step(8);
            FailIf(!_transitions.ScrollActive || _keyDoors.Opening != (priorUpdates != 0) ||
                priorUpdates != 0 && _keyDoors.OpeningCounter != counter ||
                _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
                "Initialized outgoing reserved doors must retain their timer and produce no scroll-time sound.");
            for (int i = 0; _transitions.ScrollActive && i < 100; i++) Step();
            FailIf(_transitions.ScrollActive || _keyDoors.Opening || _rooms.CurrentRoom.Id != target,
                "Scroll cleanup must release the outgoing reserved door and finish in the imported neighbor.");
            Step();
            FailIf(_keyDoors.Opening || source.GetMetatile(door) != 0xa0 ||
                _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
                "A cleaned-up reserved door must leave no delayed animation or sound after arrival.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
