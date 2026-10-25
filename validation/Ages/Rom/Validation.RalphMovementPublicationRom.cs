using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRalphMovementPublicationRom()
    {
        var allVisited = new HashSet<CutsceneCommandSource>();
        foreach (bool batched in new[] { false, true })
        foreach (bool currents in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            int destination = currents ? 0x20 : 0x79;
            _saveData.SetRoomFlag(1, destination, 0x40, false);
            _saveData.SetGlobalFlag(AgesGlobalFlag.TalkedToCheval, true);
            _inventory.LoseTreasure(TreasureId.TuneOfCurrents);
            RalphAfterChevalEvent owner = currents ? _roomEvents.Get<RalphBeforeCurrentsEvent>() : _roomEvents.Get<RalphAfterChevalEvent>();
            var commands = currents ? CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + "cutscenes/ralph_before_currents_commands.tsv") : owner.Database.Commands;
            var runner = (CutsceneCommandRunner)typeof(InteractiveInfiniteScriptHost<RalphAfterChevalCharacter>)
                .GetField("_runner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            var precise = typeof(RalphAfterChevalEvent).GetField("_precisePosition", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source =>
            {
                var command = commands.SingleOrDefault(command => command.Source == source);
                if (command is not CutsceneMoveCommand move) return null;
                return (runner.ActorSpeed("Ralph"), move.Angle, (Vector2)precise.GetValue(owner)!);
            });
            _roomEvents.CommandTraceSink = trace;
            if (currents)
            {
                LoadValidationRoom(0, 0x20); var origin = new Vector2(0x48, 0x28);
                FailIf(_collision.Collides(origin), "Ralph $37:$0b time-warp origin must be open floor.");
                _player.WarpTo(origin); _transitions.ApplyHarpTimeWarp(_player, origin);
            }
            else
            {
                LoadValidationRoom(2, 0x0f);
                FailIf(!new WarpDatabase().TryGetEdgeWarp(2, 0x0f, Vector2I.Down, new(80, _currentRoom.Height + 2),
                    new(_currentRoom.Width, _currentRoom.Height), out var warp), "Cheval house must retain its original exit into 1:79/$17.");
                _player.WarpTo(new(80, _currentRoom.Height + 2)); _player.Face(Vector2I.Down); _transitions.ApplyWarp(_player, warp);
            }
            int textHold = 0;
            for (int limit = 0; (IsTransitioning || owner.HasState || _rooms.ActiveGroup != 1) && limit < 1100; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(IsTransitioning || owner.HasState || owner.MenusDisabled || _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(1, destination, 0x40) || trace.MotionVisits == 0,
                $"Ralph $37:${(currents ? 0x0b : 0x10):x2}: actual warp/script/control sequence did not complete.");
            allVisited.UnionWith(trace.Visited); _roomEvents.CommandTraceSink = null;
            LoadValidationRoom(1, destination);
            FailIf(_entities.Entities<RalphAfterChevalCharacter>().Single().Active, "Completed Ralph sequence must remain suppressed on re-entry.");
        }
        FailIf(allVisited.Count != 9, $"Ralph source movement comparison reached {allVisited.Count}/9 operations.");
    }
}
