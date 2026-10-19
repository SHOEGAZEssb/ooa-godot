using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePreTowerMovementPublicationRom()
    {
        var allVisited = new HashSet<CutsceneCommandSource>();
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.MakuSeed, 1);
            _saveData.SetGlobalFlag(GlobalFlag.PreBlackTowerCutsceneDone, false);
            _saveData.SetGlobalFlag(GlobalFlag.RalphEnteredBlackTower, false);
            var data = new PreBlackTowerEventDatabase();
            var commands = new[] { data.RalphUnlinked, data.RalphLinked, data.ImpaUnlinked, data.ImpaLinked,
                data.NayruUnlinked, data.NayruLinked, data.ZeldaLinked }.SelectMany(lane => lane).ToDictionary(c => c.Source);
            var owner = _roomEvents.Get<PreBlackTowerEvent>();
            var positions = SomariaPrivate<Dictionary<string, Vector2>>(owner, "_precisePositions");
            var runners = new Dictionary<string, CutsceneCommandRunner> {
                ["Ralph"] = SomariaPrivate<CutsceneCommandRunner>(owner, "_ralphRunner"),
                ["Impa"] = SomariaPrivate<CutsceneCommandRunner>(owner, "_impaRunner"),
                ["Nayru"] = SomariaPrivate<CutsceneCommandRunner>(owner, "_nayruRunner"),
                ["Zelda"] = SomariaPrivate<CutsceneCommandRunner>(owner, "_zeldaRunner") };
            var trace = new ScriptMovementPublicationTrace(_runtimeState, source =>
            {
                if (!commands.TryGetValue(source, out var command)) return null;
                string actor = command switch { CutsceneMoveCommand move => move.Actor, CutsceneApplySpeedCommand move => move.Actor, _ => "" };
                if (actor.Length == 0) return null;
                int angle = command is CutsceneMoveCommand cardinal ? cardinal.Angle : runners[actor].ActorAngle(new(actor));
                return (runners[actor].ActorSpeed(new(actor)), angle, positions[actor]);
            });
            _roomEvents.CommandTraceSink = trace;
            _player.WarpTo(new(80, 16)); LoadValidationRoom(1, 0x75); _player.WarpTo(new(80, 16));
            int textHold = 0;
            for (int limit = 0; owner.HasState && limit < 1800; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(owner.HasState || !_saveData.HasGlobalFlag(GlobalFlag.PreBlackTowerCutsceneDone) || trace.MotionVisits == 0,
                $"Pre-tower linked={linked}: actual gameplay movement/control handoff did not complete.");
            allVisited.UnionWith(trace.Visited); _roomEvents.CommandTraceSink = null;
        }
        // scripts.s plus scriptHelper.s contain twenty motion operations in
        // the seven pre-tower streams, including Zelda's initial applyspeed.
        FailIf(allVisited.Count != 20, $"Pre-tower movement comparison reached {allVisited.Count}/20 source operations.");
    }
}
