using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateDeferredEventArrivalVisibility()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 2);
            _saveData.SetRoomFlag(1, 0x79, 0x40);
            _saveData.WriteWramByte(AgesWramAddress.wMooshState, 0);
            LoadValidationRoom(0, 0x7c);
            // Isolate the native north-scroll boundary, then drive the full
            // application loop rather than advancing the event directly.
            _player.WarpTo(new Vector2(0x58, 6));
            _transitions.BeginScroll(_player, Vector2I.Up, 0x6c);
            NpcCharacter[] rescue = _entities.Entities<NpcCharacter>()
                .Where(actor => actor.Active && actor.Record.Id is 0x73 or 0x0d).ToArray();
            FailIf(rescue.Length != 4 || rescue.Any(actor => !actor.Visible),
                "Moosh's room $00:$6c rescue actors are hidden after their source initialization.");
            StepGameplayUpdates(64, Vector2.Zero, batched: batched, afterUpdate: () =>
                FailIf(rescue.Any(actor => actor.Active && !actor.Visible),
                    "A Moosh rescue actor disappeared during the incoming scroll."));
            LoadValidationRoom(0, 0x47);
            _saveData.SetRoomFlag(1, 0x79, 0x40, false);
            LoadValidationRoom(0, 0x6c);
            FailIf(_entities.Entities<NpcCharacter>().Any(actor => actor.Active && actor.Record.Id is 0x73 or 0x0d),
                "Moosh visibility initialization bypassed the rescue prerequisite.");

            ReinitializeGameplayForValidation();
            _inventory.AssignAnimalCompanion(0x0d);
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest);
            _saveData.SetGlobalFlag(AgesGlobalFlag.GotFlute, false);
            LoadValidationRoom(0, 0x62);
            _player.WarpTo(new Vector2(154, 0x48));
            _transitions.BeginScroll(_player, Vector2I.Right, 0x63);
            var forest = _roomEvents.Get<CompanionForestEvent>();
            FailIf(forest.Flights.Count != 3, "Room $00:$63 did not create its three reward fairies.");
            var poses = forest.Flights.Select(flight => (flight.XFixed, flight.YFixed, flight.Counter1, flight.Counter2)).ToArray();
            int visibleUpdates = 0;
            StepGameplayUpdates(32, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                FailIf(forest.Flights.Any(flight => !flight.Actor.Visible),
                    "Companion-forest fairies $49:$04 only became visible after scrolling.");
                FailIf(!poses.SequenceEqual(forest.Flights.Select(flight => (flight.XFixed, flight.YFixed, flight.Counter1, flight.Counter2))),
                    "Companion-forest fairy initialization advanced movement during scrolling.");
                visibleUpdates++;
            });
            FailIf(visibleUpdates != 32 || !_transitions.ScrollActive,
                "The companion-forest fixture did not observe 32 incoming-scroll updates.");
            // Cancel a live scroll and re-enter: no unresolved fairy pose may
            // survive into another room or suppress the next initialization.
            LoadValidationRoom(0, 0x47);
            FailIf(forest.Flights.Count != 0, "Cancelling the forest scroll retained its fairy actors.");
            LoadValidationRoom(0, 0x62);
            _transitions.BeginScroll(_player, Vector2I.Right, 0x63);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(forest.Flights.Count != 3 || forest.Flights.Any(flight => !flight.Actor.Visible),
                "Repeating the companion-forest scroll lost its source visibility initialization.");
            LoadValidationRoom(0, 0x47);
            _saveData.SetGlobalFlag(AgesGlobalFlag.GotFlute);
            LoadValidationRoom(0, 0x63);
            FailIf(forest.Flights.Count != 0, "Completed flute reward recreated its arrival fairies.");

            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(1, 0xa8, 0x40);
            LoadValidationRoom(1, 0xa8);
            // raftwreckCutscene.s's terminal warp, after its room flag write.
            _transitions.ApplyWarpWithFadeOut(_player,
                new Warp(1, 0xa8, -1, 0, 0, 1, 0xaa, 0x42, 0, 3));
            int thiefUpdates = 0;
            void ObserveThieves()
            {
                if (_currentRoom.Id != 0xaa || !_transitions.IsTransitioning) return;
                // The destination load can finish after its object pass. Its
                // cleared slots initialize on the next destination update.
                if (thiefUpdates++ == 0) return;
                var thieves = _entities.Entities<NpcCharacter>().Where(actor => actor.Record is { Id: 0x48, SubId: < 5 }).ToArray();
                FailIf(thieves.Length != 5 || thieves.Any(actor => !actor.Visible),
                    $"Tokay thieves $48:$00-$04 are unresolved on destination update {thiefUpdates}.");
                FailIf(_roomEvents.Get<TokayTheftEvent>().StolenCount != 0,
                    "Tokay visibility initialization advanced the theft script during arrival.");
            }
            for (int limit = 0; _transitions.IsTransitioning && limit < 180; limit += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: ObserveThieves);
            FailIf(thiefUpdates < 20 || _transitions.IsTransitioning || _currentRoom.Id != 0xaa,
                "The raftwreck warp did not retain visible thieves throughout its destination fade.");
            _saveData.SetRoomFlag(1, 0xaa, 0x40);
            LoadValidationRoom(0, 0x47);
            LoadValidationRoom(1, 0xaa);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Any(actor => actor.Record is { Id: 0x48, SubId: < 5 } &&
                (actor.Active || actor.Visible)), "Completed theft recreated visible Tokay thieves on re-entry.");
        }
        GD.Print("Validated Moosh rescue scrolling, companion reward fairy initialization/freeze/cancellation and Tokay arrival visibility through split/batched application updates.");
    }
}
