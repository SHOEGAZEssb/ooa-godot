using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePoeScrollPresentation()
    {
        byte overworldFlags = _saveData.GetRoomFlags(0, 0x7c);
        byte tombFlags = _saveData.GetRoomFlags(2, 0x2e);
        foreach (bool batched in new[] { false, true })
        for (int bits = 0; bits < 8; bits++)
        {
            bool progress = (bits & 1) != 0, tomb = (bits & 2) != 0, item = (bits & 4) != 0;
            _saveData.SetRoomFlag(0, 0x7c, 0x40, progress);
            _saveData.SetRoomFlag(0, 0x7c, 0x20, item);
            _saveData.SetRoomFlag(2, 0x2e, 0x40, tomb);
            // poe.s deletes variant $00 after either $40 flag, and $02 unless
            // both are set and the overworld item bit $20 remains clear.
            int expected = !progress && !tomb ? 0 : progress && tomb && !item ? 2 : -1;
            LoadValidationRoom(0, 0x7b);
            _player.WarpTo(new(156, 64));
            _transitions.BeginScroll(_player, Vector2I.Right, 0x7c);
            var actors = _entities.Entities<PoeCharacter>().ToArray();
            void Check()
            {
                FailIf(actors.Length != 2 || actors.Any(actor =>
                    actor.Visible != (actor.Record.Var03 == expected) ||
                    actor.Active != (actor.Record.Var03 == expected)),
                    $"Poe $59 room 0:7c scroll visibility diverged for flags ${bits:x2}.");
                FailIf(_saveData.HasRoomFlag(0, 0x7c, 0x40) != progress ||
                    _saveData.HasRoomFlag(0, 0x7c, 0x20) != item ||
                    _saveData.HasRoomFlag(2, 0x2e, 0x40) != tomb || _dialogue.IsOpen,
                    "Poe preload advanced past checkabutton or changed persistent flags.");
            }
            Check();
            var frames = actors.Select(actor => actor.CurrentAnimationFrame).ToArray();
            for (int update = 0; _transitions.ScrollActive && update < 120; update += 2)
            {
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                Check();
                FailIf(!actors.Select(actor => actor.CurrentAnimationFrame).SequenceEqual(frames),
                    "Poe animation advanced during scrolling or repeated state 0 at commit.");
            }
            FailIf(_transitions.ScrollActive, "Poe room 0:7c scroll did not finish.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            Check();
            FailIf(_roomEvents.Get<PoeEvent>().HasState != (expected >= 0),
                "Poe room 0:7c script did not hand off after scrolling.");

            // Departure must preserve the selected actor, including on a
            // repeated visit. Source state 1 freezes; room-event cancellation
            // must not act like interactionDelete at the start of the scroll.
            for (int visit = 0; visit < 2; visit++)
            {
                var outgoing = _entities.Entities<PoeCharacter>().ToArray();
                var outgoingFrames = outgoing.Select(actor => actor.CurrentAnimationFrame).ToArray();
                var outgoingPositions = outgoing.Select(actor => actor.Position).ToArray();
                _player.WarpTo(new(6, 64));
                _transitions.BeginScroll(_player, Vector2I.Left, 0x7b);
                void CheckOutgoing()
                {
                    FailIf(_entities.OutgoingEntities<PoeCharacter>().Count != 2 ||
                        outgoing.Any(actor => actor.Visible != (actor.Record.Var03 == expected) ||
                            actor.Active != (actor.Record.Var03 == expected)) ||
                        !outgoing.Select(actor => actor.CurrentAnimationFrame).SequenceEqual(outgoingFrames) ||
                        !outgoing.Select(actor => actor.Position).SequenceEqual(outgoingPositions),
                        $"Outgoing Poe $59 disappeared or advanced before scroll completion, flags ${bits:x2}, visit {visit}.");
                    FailIf(_roomEvents.Get<PoeEvent>().HasState,
                        "Outgoing Poe retained its room script after destination handoff.");
                }
                CheckOutgoing();
                for (int update = 0; _transitions.ScrollActive && update < 120; update += 2)
                {
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    if (_transitions.ScrollActive)
                        CheckOutgoing();
                }
                FailIf(_transitions.ScrollActive || _entities.OutgoingEntities<PoeCharacter>().Count != 0,
                    "Outgoing Poe was not retired at scroll completion.");
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                _player.WarpTo(new(156, 64));
                _transitions.BeginScroll(_player, Vector2I.Right, 0x7c);
                for (int update = 0; _transitions.ScrollActive && update < 120; update += 2)
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                actors = _entities.Entities<PoeCharacter>().ToArray();
                Check();
                FailIf(_transitions.ScrollActive || _roomEvents.Get<PoeEvent>().HasState != (expected >= 0),
                    "Poe $59 did not restart correctly on scroll re-entry.");
            }

            // Tomb's independent state-0 branch also uses the preload contract.
            LoadValidationRoom(0, 0x60);
            _entities.BeginScreenTransition(2, _world.LoadRoom(2, 0x2e), new(160, 0), _player);
            var tombActor = _entities.Entities<PoeCharacter>().Single();
            FailIf(tombActor.Visible != (progress && !tomb) || tombActor.Active != (progress && !tomb),
                $"Poe $59:$00:$01 room 2:2e preload diverged for flags ${bits:x2}.");
            // A direct room load cancels the pending preload; the next iteration
            // must select fresh actors using the new flags.
            LoadValidationRoom(0, 0x60);
        }
        _saveData.SetRoomFlag(0, 0x7c, 0x40, (overworldFlags & 0x40) != 0);
        _saveData.SetRoomFlag(0, 0x7c, 0x20, (overworldFlags & 0x20) != 0);
        _saveData.SetRoomFlag(2, 0x2e, 0x40, (tombFlags & 0x40) != 0);
    }
}
