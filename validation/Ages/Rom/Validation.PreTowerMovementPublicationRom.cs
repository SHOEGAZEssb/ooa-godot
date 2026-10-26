using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePreTowerMovementPublicationRom()
    {
        ValidatePreTowerEntryScrollRom();
        var allVisited = new HashSet<CutsceneCommandSource>();
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.MakuSeed, 1);
            _saveData.SetGlobalFlag(AgesGlobalFlag.PreBlackTowerCutsceneDone, false);
            _saveData.SetGlobalFlag(AgesGlobalFlag.RalphEnteredBlackTower, false);
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
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_saveData.HasGlobalFlag(0x33), "Pre-tower flag $33 must remain clear during the event.");
            LoadValidationRoom(1, 0x75); _player.WarpTo(new(80, 16));
            FailIf(!owner.HasState || _saveData.HasGlobalFlag(0x33),
                $"Pre-tower linked={linked}: cancelling through room replacement must restart without committing flag $33.");
            int textHold = 0;
            bool sawCompletionRunner = false;
            var completionRunner = runners[linked ? "Impa" : "Nayru"];
            for (int limit = 0; owner.HasState && limit < 1800; limit += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    sawCompletionRunner |= completionRunner.Active;
                    // nayruSubid09/impaSubid5 set $33 in the scriptend update.
                    if (sawCompletionRunner)
                        FailIf(_saveData.HasGlobalFlag(0x33) == completionRunner.Active,
                            $"Pre-tower linked={linked}: flag $33 must become set in the completing actor's scriptend update.");
                    if (_dialogue.IsOpen && ++textHold >= 3) { _dialogue.Close(); textHold = 0; }
                });
            FailIf(owner.HasState || !sawCompletionRunner ||
                _saveData.HasGlobalFlag(0x45) == linked ||
                !_saveData.HasGlobalFlag(AgesGlobalFlag.PreBlackTowerCutsceneDone) || trace.MotionVisits == 0,
                $"Pre-tower linked={linked}: actual gameplay movement/control handoff did not complete.");
            allVisited.UnionWith(trace.Visited); _roomEvents.CommandTraceSink = null;
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), OracleGame.Ages, out var restored) ||
                (restored!.ReadWramByte(0xc6d6) & 0x08) == 0,
                $"Pre-tower linked={linked}: flag $33 must survive the explicit save-image boundary at $c6d6 bit 3.");
            _saveData.RestoreFrom(restored!);
            LoadValidationRoom(1, 0x75);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(owner.HasState || _player.CutsceneControlled || !_saveData.HasGlobalFlag(0x33),
                $"Pre-tower linked={linked}: completion must survive room re-entry without replaying the event.");
        }
        // scripts.s plus scriptHelper.s contain twenty motion operations in
        // the seven pre-tower streams, including Zelda's initial applyspeed.
        FailIf(allVisited.Count != 20, $"Pre-tower movement comparison reached {allVisited.Count}/20 source operations.");
    }

    private void ValidatePreTowerEntryScrollRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MakuSeed, 1);
            LoadValidationRoom(1, 0x65);
            int x = Enumerable.Range(8, 144).First(x =>
                Enumerable.Range(0x70, 0x10).All(y => !_collision.Collides(new(x, y))));
            _player.WarpTo(new(x, 0x70));
            for (int update = 0; !_transitions.ScrollActive && update < 32; update++)
                StepGameplayUpdates(1, Vector2.Down, ["move_down"], batched: batched);
            FailIf(!_transitions.ScrollActive || _rooms.CurrentRoom.Id != 0x75,
                "Pre-tower fixture must enter $1:$75 through the real south exit of $1:$65.");
            var impa = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x31 && npc.Record.SubId == 4);
            var owner = _roomEvents.Get<PreBlackTowerEvent>();
            var rom = new FrontendRom();
            rom[0xd240] = 1; rom[0xd24b] = 0xf8; rom[0xd24d] = 0x58; rom[0xd25a] = 0x80;
            rom[0xff8a] = 1;
            // Execute bank0's 16-bit transition projection and capture carry.
            byte[] caller = [0x21, 0x4b, 0xd2, 0xcd, 0xc1, 0x0f, 0x3e, 0, 0xce, 0, 0xea, 0x20, 0xc1, 0xc9];
            for (int i = 0; i < caller.Length; i++) rom[0xc100 + i] = caller[i];
            void Check()
            {
                if (!_transitions.ScrollActive) return;
                int offset = (int)impa.TransitionDrawOffset.Y;
                rom[0xff90] = (byte)offset; rom[0xff91] = (byte)(offset >> 8);
                rom.Call(0xc100, 0);
                Vector2 screen = _transitions.WorldToGameplayScreen(impa.Position) +
                    impa.TransitionDrawOffset + impa.SourceOamWrapOffset;
                FailIf(rom[0xc120] != 0 || screen.Y < 0x90 || impa.Position != new Vector2(0x58, 0xf8) ||
                    owner.Stage != PreBlackTowerEventStage.RalphUnlinkedNative || _saveData.HasGlobalFlag(0x33),
                    $"Pre-tower north entry: Impa $31:$04 must remain offscreen and frozen throughout scrolling, offset={offset}, screen={screen}.");
            }
            Check();
            for (int update = 0; _transitions.ScrollActive && update < 100; update += 2)
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: Check);
            FailIf(_transitions.ScrollActive, "Pre-tower north entry scroll did not finish.");
        }
    }
}
