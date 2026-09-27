using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static double NpcAnimationTicks(NpcCharacter npc) => (double)typeof(NpcCharacter)
        .GetField("_animationTicks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(npc)!;

    private void WalkToPostman(bool batched)
    {
        _player.WarpTo(new Vector2(0x50, 0x68), recordSafe: false);
        foreach (Vector2 point in new[] { new Vector2(0x38, 0x68), new Vector2(0x38, 0x18), new Vector2(0x26, 0x18) })
        for (int axis = 0; axis < 2; axis++)
        {
            for (int count = 0; Mathf.Abs(axis == 0 ? _player.Position.X - point.X : _player.Position.Y - point.Y) > 1; count++)
            {
                if (count == 180) throw new InvalidOperationException($"Postman approach blocked at {_player.Position} -> {point}.");
                Vector2 direction = axis == 0 ? new(Mathf.Sign(point.X - _player.Position.X), 0)
                    : new(0, Mathf.Sign(point.Y - _player.Position.Y));
                string action = direction == Vector2.Left ? "move_left" : direction == Vector2.Right
                    ? "move_right" : direction == Vector2.Up ? "move_up" : "move_down";
                StepGameplayUpdates(1, direction, [action], [action]);
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"Postman approach crossed solid geometry at {_player.Position}.");
            }
        }
        _player.Face(Vector2I.Left);
        StepGameplayUpdates(32, Vector2.Zero, batched: batched);
    }

    private void ValidateRoom22fPostman()
    {
        // Independent source expectations: scriptHelper.s:postmanScript,
        // bank0.interactionRunScript/interactionAnimateBasedOnSpeed, and
        // interactionAnimation5a5a6/5a60c (two 16-update frames each).
        List<string>? baseline = null;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2, 0x2f);
            PostmanCharacter postman = _entities.Entities<PostmanCharacter>().Single();
            PostmanScriptHost host = _interactions.NpcScriptsForValidation.Postman;
            var trace = new ValidationCutsceneTrace();
            _interactions.NpcScriptsForValidation.TraceSink = trace;
            var observations = new List<string>();
            string State() => $"{postman.Position}:{postman.Active}:{postman.Leaving}:" +
                $"{postman.CurrentAnimationFrame}:{NpcAnimationTicks(postman)}:{postman.ZIndex}:" +
                $"{host.CurrentCommandIndex}:{host.Counter}:{host.MovementCounter}:{host.InputDisabled}:" +
                $"{_player.Position}:{_player.CutsceneControlled}:{_player.IsHoldingItemTwoHands}:" +
                $"{_inventory.TradeItem}:{_saveData.GetRoomFlags(2, 0x2f)}:{_dialogue.CurrentMessage}";
            void Step(int count = 1)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched);
                observations.Add(State());
            }
            void Text(string content, bool choice = false)
            {
                FailIf(!_dialogue.IsOpen || _dialogue.ChoiceActive != choice ||
                    !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(content, StringComparison.Ordinal),
                    $"Postman expected '{content}', choice={choice}; got '{_dialogue.CurrentMessage}'.");
                string frozen = State();
                Step(8);
                FailIf(State() != frozen, "Postman script/native state advanced while text was already active.");
            }
            void Wait30()
            {
                Step();
                FailIf(host.Counter != 30 || host.MovementCounter != 0,
                    "Postman wait did not load counter1=30 independently of movement counter2.");
                Step(29);
                FailIf(host.Counter != 1 || host.MovementCounter != 0,
                    "Postman counter1 dispatched before its zero update.");
                Step();
            }
            void Press()
            {
                _player.Face(Vector2I.Left);
                FailIf(!postman.CanTalkTo(_player) || _currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"Postman is not reachable from {_player.Position}.");
                int animation = postman.CurrentAnimationFrame;
                double ticks = NpcAnimationTicks(postman);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen || !host.InputDisabled || !_player.CutsceneControlled,
                    "Postman's A probe was not consumed in its eligible object slot.");
                FailIf(postman.CurrentAnimationFrame != (animation + (int)(ticks + 1) / 16) % 2 ||
                    NpcAnimationTicks(postman) != (ticks + 1) % 16,
                    "Postman skipped or duplicated its native tail on the text-opening update.");
                observations.Add(State());
            }
            void Animation(int total)
            {
                FailIf(postman.CurrentAnimationFrame != total / 16 % 2 || NpcAnimationTicks(postman) != total % 16,
                    $"Postman animation expected {total} source calls, got frame={postman.CurrentAnimationFrame}, ticks={NpcAnimationTicks(postman)}.");
                FailIf(postman.ZIndex != (postman.Position.Y > (int)_player.Position.Y + 0x0b ? 11 : 9),
                    "Postman priority did not observe the post-movement position.");
            }

            FailIf(postman.Record is not { Id: 0x55, SubId: 0, Var03: 0, DefaultAnimation: 2, TextId: 0x0b03 } ||
                postman.Position != new Vector2(0x18, 0x18) || postman.ScriptButtonSensitive,
                "Postman placement/graphics or pre-init A eligibility changed.");
            Step();
            FailIf(!postman.Initialized || !postman.ScriptButtonSensitive ||
                host.CurrentCommandIndex != 2 || !_entities.BlocksLink(postman.Position),
                "Postman state0 did not run initialization through checkabutton in its first slot.");
            WalkToPostman(batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Press();
                Text("This isn't good!");
                _dialogue.Close();
                Wait30();
                FailIf(host.CurrentCommandIndex != 12 || !host.InputDisabled,
                    "Copied-buffer no-clock jump must yield before enableinput.");
                Step();
                FailIf(host.CurrentCommandIndex != 2 || host.InputDisabled || _player.CutsceneControlled,
                    "Copied-buffer return jump did not release input and yield at the A loop.");
                FailIf(_inventory.HasTreasure(0x41) || _saveData.HasRoomFlag(2, 0x2f, 0x20),
                    "No-clock Postman changed inventory/room flag $20.");
            }
            _inventory.GiveTreasure(0x41, 0);
            Press();
            _dialogue.Close();
            Wait30();
            Text("Poe Clock", choice: true);
            _dialogue.SubmitChoiceForValidation(1);
            Wait30();
            Text("Sheesh!!!");
            _dialogue.Close();
            Step();
            FailIf(host.CurrentCommandIndex != 2 || host.InputDisabled || _inventory.TradeItem != 0 ||
                _saveData.HasRoomFlag(2, 0x2f, 0x20), "Declining Postman changed trade state or failed to release input.");

            Press();
            _dialogue.Close();
            Wait30();
            _dialogue.SubmitChoiceForValidation(0);
            Wait30();
            Text("Hmm? Tick, tock?");
            _dialogue.Close();
            Wait30();
            FailIf(!postman.Leaving || host.MovementCounter != 0 || postman.Position != new Vector2(0x18, 0x18),
                "Postman var3f write must yield without starting movement.");
            Step(); // setspeed yields independently
            FailIf(host.MovementCounter != 0, "setspeed started counter2 early.");

            foreach ((int counter, Vector2 start, Vector2 delta) in new[]
                { (0x1d, new Vector2(0x18, 0x18), new Vector2(2, 0)), (0x39, new Vector2(0x50, 0x18), new Vector2(0, 2)) })
            {
                Step();
                FailIf(host.MovementCounter != counter || postman.Position != start,
                    "Postman movement setup moved before decrementing counter2.");
                string expectedOam = delta.X != 0
                    ? "16@8,0,10,32;8,8,8,32|16@8,0,14,32;8,8,12,32"
                    : "16@8,0,0,0;8,8,2,0|16@8,0,2,32;8,8,0,32";
                FailIf(postman.CurrentScriptAnimationSource != expectedOam,
                    "Postman $55 animation $01/$02 lost source OAM/durations/flip bits.");
                int animationCalls = 3;
                Animation(animationCalls);
                int remaining = counter;
                StepGameplayUpdates(counter, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    remaining--;
                    animationCalls += remaining == 0 ? 1 : 3;
                    Animation(animationCalls);
                    Vector2 expected = start + delta * Math.Min(counter - remaining, counter - 1);
                    FailIf(host.MovementCounter != remaining || postman.Position != expected || !host.InputDisabled,
                        $"Postman counter2=${remaining:x2} expected {expected}, got {postman.Position}.");
                    observations.Add(State());
                });
            }
            int finalCalls = 3 * 0x39 + 1;
            Step(); // final wait setup, counter2 remains zero
            Animation(++finalCalls);
            FailIf(host.Counter != 30 || host.MovementCounter != 0, "Final wait confused counter1 with counter2.");
            for (int remaining = 29; remaining > 0; remaining--)
            {
                Step();
                Animation(++finalCalls);
                FailIf(host.Counter != remaining || host.MovementCounter != 0 ||
                    postman.Position != new Vector2(0x50, 0x88) || _inventory.TradeItem != 0,
                    "Final Postman wait moved the actor or granted Stationery early.");
            }
            Step();
            GroundTreasurePickup reward = _interactions.PostmanTreasureForValidation ??
                throw new InvalidOperationException("Postman final wait did not grant Stationery on zero.");
            FailIf(_inventory.TradeItem != 1 || !_saveData.HasRoomFlag(2, 0x2f, 0x20) || postman.Active ||
                !reward.Held || reward.Record.TreasureObject != "TREASURE_OBJECT_TRADEITEM_01" ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains("Stationery!", StringComparison.Ordinal) ||
                !host.InputDisabled || !_dialogue.IsOpen,
                "Postman reward lost Stationery, room flag $20, held item, text, visibility, or input ownership: " +
                $"{State()}, held={reward.Held}, object={reward.Record.TreasureObject}, text=${reward.Record.CompletionTextId:x4}.");
            Step(2); // Force-state selection, then state04's pose initialization.
            FailIf(!_player.IsHoldingItemTwoHands || reward.Position != _player.Position + Vector2.Up * 14,
                "Postman reward did not hand off to Link's actual get-item update.");
            _dialogue.Close();
            Step(2);
            FailIf(host.HasState || host.InputDisabled || _player.CutsceneControlled || _player.IsHoldingItemTwoHands ||
                _interactions.PostmanTreasureForValidation is not null || _entities.Entities<GroundTreasurePickup>().Count != 0,
                "Postman completion retained a runner, input lease, held pose, or treasure node.");
            Animation(finalCalls); // flag suppression and scriptend do not run a tail
            var localJumps = trace.Entries.Where(e => e.Phase == CutsceneCommandTracePhase.Completed &&
                e.Source.Opcode == "scriptjumpyield").ToArray();
            FailIf(localJumps.Length != 5 || localJumps.Any(e => e.NextCommandIndex is not (2 or 12)),
                "Postman no-clock/decline branches lost their independently expected copied-buffer targets.");
            observations.Add(string.Join(";", trace.Entries.Select(e => $"{e.Source.CommandIndex}:{e.ScriptUpdate}:{e.Counter}:{e.Phase}")));
            if (baseline is null) baseline = observations.ToList();
            else
            {
                int difference = Enumerable.Range(0, Math.Min(baseline.Count, observations.Count))
                    .FirstOrDefault(i => baseline[i] != observations[i], -1);
                FailIf(difference >= 0 || baseline.Count != observations.Count,
                    "Postman diverged between individual and batched gameplay updates: " +
                    (difference < 0 ? "sample count" : $"sample {difference}: {baseline[difference]} vs {observations[difference]}"));
            }
            LoadValidationRoom(2, 0x2f);
            Step();
            FailIf(_entities.Entities<PostmanCharacter>().Single().Active || _interactions.TryInteract(_player),
                "Completed Postman became visible or interactive on re-entry.");
        }
        ValidatePostmanGatesAndCancellation();
        GD.Print("Validated Postman through reachable geometry and actual gameplay: repeated no-clock/decline/accept, both counter2 zero boundaries, per-update motion/animation, final wait, reward/cleanup, gates, cancellation, re-entry, and host batching.");
    }

    private void ValidatePostmanGatesAndCancellation()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int cancelAt in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(0x41, 0);
            LoadValidationRoom(2, 0x2f);
            PostmanCharacter postman = _entities.Entities<PostmanCharacter>().Single();
            PostmanScriptHost host = _interactions.NpcScriptsForValidation.Postman;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            if (cancelAt == 0)
            {
                _dialogue.ShowGameplayMessage("Postman state0 gate", _player.Position.Y);
                Step();
                FailIf(!postman.Initialized || postman.ScriptButtonSensitive || host.CurrentCommandIndex != 0 ||
                    NpcAnimationTicks(postman) != 1,
                    "Postman state0 must initialize and run its native tail even when text blocks its script.");
                Step(3);
                FailIf(NpcAnimationTicks(postman) != 1 || host.CurrentCommandIndex != 0,
                    "Initialized Postman retained state0's text-update eligibility.");
                _dialogue.Close();
            }
            Step();
            WalkToPostman(batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(!_dialogue.IsOpen, "Postman cancellation fixture did not open TX_0b03.");
            _dialogue.Close(); Step(31);
            _dialogue.SubmitChoiceForValidation(0); Step(31);
            _dialogue.Close();
            if (cancelAt >= 1) Step(36); // setup plus three rightward movements
            if (cancelAt >= 2) Step(86); // final wait
            if (cancelAt >= 3) Step(35); // held reward
            FailIf(cancelAt == 0 && postman.Leaving || cancelAt == 1 && host.MovementCounter == 0 ||
                cancelAt == 2 && (host.MovementCounter != 0 || host.Counter != 29) ||
                cancelAt == 3 && _interactions.PostmanTreasureForValidation is not { Held: true },
                $"Postman cancellation fixture missed phase {cancelAt}.");
            if (cancelAt == 1)
            {
                int counter = host.MovementCounter;
                Vector2 position = postman.Position;
                double ticks = NpcAnimationTicks(postman);
                _entities.InitializedObjectsDisabledSource = static () => true;
                Step(3);
                FailIf(host.MovementCounter != counter || postman.Position != position || NpcAnimationTicks(postman) != ticks,
                    "Disabled-interaction mask froze only one half of Postman's update.");
                _entities.InitializedObjectsDisabledSource = static () => false;
                _dialogue.ShowGameplayMessage("Postman gate fixture", _player.Position.Y);
                Step(3);
                FailIf(host.MovementCounter != counter || postman.Position != position || NpcAnimationTicks(postman) != ticks,
                    "Postman script/native movement advanced during text.");
                _dialogue.Close();
                // death gates interactionRunScript, not its eligible native tail.
                var death = typeof(Player).GetField("_deathPending", BindingFlags.Instance | BindingFlags.NonPublic)!;
                death.SetValue(_player, true);
                Step();
                FailIf(host.MovementCounter != counter || postman.Position != position || NpcAnimationTicks(postman) != (ticks + 3) % 16,
                    "Postman death gate did not freeze counter2 while retaining its native tail.");
                death.SetValue(_player, false);
            }
            GroundTreasurePickup? held = _interactions.PostmanTreasureForValidation;
            bool completed = _saveData.HasRoomFlag(2, 0x2f, 0x20);
            if (batched)
            {
                // Direct cancellation must release its own resources before
                // any room loader can provide blanket cleanup.
                host.Cancel();
                FailIf(host.HasState || host.InputDisabled || _player.CutsceneControlled ||
                    postman.ScriptButtonSensitive || held is { Held: true },
                    $"Direct Postman cancellation phase {cancelAt} retained owned resources.");
                _dialogue.Close();
                Step(2);
                FailIf(_player.IsHoldingItemTwoHands || _entities.Entities<GroundTreasurePickup>().Count != 0,
                    "Postman cancellation did not let Link finish state04 or remove the reward node.");
            }
            LoadValidationRoom(2, 0x30);
            Step(2);
            FailIf(host.HasState || host.InputDisabled || _player.CutsceneControlled || _player.IsHoldingItemTwoHands ||
                _interactions.PostmanTreasureForValidation is not null || _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                postman.ScriptButtonSensitive || held is { Held: true },
                $"Postman cancellation phase {cancelAt} retained resources.");
            LoadValidationRoom(2, 0x2f);
            Step();
            PostmanCharacter replacement = _entities.Entities<PostmanCharacter>().Single();
            FailIf(ReferenceEquals(postman, replacement) || replacement.Leaving || replacement.Active == completed ||
                _inventory.TradeItem != (completed ? 1 : 0), "Postman re-entry retained transient movement or changed persistent trade effects.");
        }
    }
}
