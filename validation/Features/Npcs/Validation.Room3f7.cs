using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom3f7KnowItAllBirds()
    {
        // Independent expectations from mainData.s and text/ages/text.yaml.
        int[] order = [8, 9, 6, 7, 4, 5, 2, 3, 0, 1];
        int[] palettes = [0, 1, 2, 3, 2, 3, 1, 0, 0, 1];
        string[] topics = ["a hero's skill", "shield tactics", "the Mystical Seeds", "Bombs",
            "items", "maps", "saving", "ages", "the Subscreens", "essences"];
        string[] tutorials = [
            "A skill for a courageous hero! Press and hold the sword button to save power, then release it to unleash a spin attack!",
            "Deflect enemy attacks while pressing the shield button.",
            "There are five kinds of Mystical Seeds. Each seed type has a unique effect. You can't carry seeds without a satchel.",
            "After taking a Bomb out, press the button again to place it.",
            "Set items to", "Press SELECT to view the map.", "Open the Sub- screen and press SELECT twice",
            "The present age and the past age are related.", "Press START to access the Subscreen, then press SELECT",
            "Umm, even this Know-It-All Bird knows little of essences."
        ];
        string[] tutorialEnds = ["unleash a spin attack!", "the shield button.", "without a satchel.",
            "pick up a placed Bomb.", "how many you have.", "you've seen and heard is recorded.",
            "also opens the Save Screen.", "can be taken to the past!", "on the Quest Status Screen.",
            "little of essences."];
        List<string>? baseline = null;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            LoadValidationRoom(3, 0xf7);
            _player.WarpTo(new Vector2(80, 112));
            var birds = _entities.Entities<KnowItAllBirdCharacter>().ToArray();
            FailIf(!birds.Select(b => b.Record.SubId).SequenceEqual(order),
                "Room 3:f7 lost its ten $e3 slots in source order $08,$09,$06,$07,$04,$05,$02,$03,$00,$01.");
            var observations = new List<string>();
            void CheckDrawOrder(KnowItAllBirdCharacter[] actors)
            {
                if (actors.Length == 0) return;
                var parent = (Node2D)actors[0].GetParent();
                // bank0 queues each visible&3 bucket by ascending interaction
                // address; CGB's earlier OAM entry wins. Compare the resulting
                // foreground-to-background order, not just each bird's ZIndex.
                int[] expected = actors.OrderByDescending(b => b.ZIndex)
                    .ThenBy(b => _entities.InteractionSlot(b)).Select(b => b.Record.SubId).ToArray();
                int[] actual = actors.OrderByDescending(b => b.ZIndex)
                    .ThenByDescending(b => b.GetIndex()).Select(b => b.Record.SubId).ToArray();
                FailIf(!expected.SequenceEqual(actual) || actors.Any(b => b.GetParent() != parent) ||
                    parent.YSortEnabled,
                    "3:f7 equal-priority birds must draw earlier interaction slots above later slots, without Y sorting.");
            }
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched);
                CheckDrawOrder(_entities.Entities<KnowItAllBirdCharacter>().ToArray());
            }
            var random = (OracleRandom)typeof(RoomEntityManager).GetField("_random",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_entities)!;
            var rng = random.CaptureState();
            int rng1 = rng.Rng1, rng2 = rng.Rng2;
            int SourceRandom()
            {
                int product = (((rng2 << 8) | rng1) * 3) & 0xffff;
                rng2 = product >> 8;
                return rng1 = (rng1 + rng2) & 255;
            }
            int[] directions = order.Select(_ => SourceRandom() & 1).ToArray();
            Step(1);
            FailIf(_entities.RandomCalls != rng.Calls + 10 ||
                !birds.Select(b => b.Direction).SequenceEqual(directions) ||
                birds.Any(b => b.TurnCounter != 30 || !b.ScriptVisible || b.ScriptButtonSensitive || b.ZIndex != 9) ||
                !birds.Select(b => _entities.InteractionSlot(b)).SequenceEqual(Enumerable.Range(2, 10)),
                "Room 3:f7 $e3 state 0 lost its ordered RNG, 30-update counter, visibility, or deferred script init.");
            Step(29);
            FailIf(_entities.RandomCalls != rng.Calls + 10 || birds.Any(b => b.TurnCounter != 1),
                "$e3 idle RNG ran before the 30th state-1 update.");
            for (int i = 0; i < 10; i++) if ((SourceRandom() & 7) == 0) directions[i] ^= 1;
            Step(1);
            FailIf(_entities.RandomCalls != rng.Calls + 20 ||
                !birds.Select(b => b.Direction).SequenceEqual(directions) || birds.Any(b => b.TurnCounter != 30),
                "$e3 idle turn boundary changed shared RNG order or the 1-in-8 turn gate.");

            void Walk(Vector2 point)
            {
                for (int axis = 0; axis < 2; axis++)
                {
                    int limit = 220;
                    while (Mathf.Abs(axis == 0 ? _player.Position.X - point.X : _player.Position.Y - point.Y) > 1)
                    {
                        if (--limit == 0) throw new InvalidOperationException($"3:f7 approach blocked {_player.Position} -> {point}.");
                        Vector2 direction = axis == 0 ? new(Mathf.Sign(point.X - _player.Position.X), 0)
                            : new(0, Mathf.Sign(point.Y - _player.Position.Y));
                        string action = direction == Vector2.Left ? "move_left" : direction == Vector2.Right
                            ? "move_right" : direction == Vector2.Up ? "move_up" : "move_down";
                        StepGameplayUpdates(1, direction, [action], [action]);
                    }
                }
                Step(1);
                foreach (var actor in birds)
                    FailIf(actor.ZIndex != (actor.Position.Y > (int)_player.Position.Y + 0x0b ? 11 : 9),
                        $"$e3:${actor.Record.SubId:x2} has incorrect Link-relative priority at {_player.Position}.");
                FailIf(_rooms.CurrentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    $"3:f7 approach placed Link inside solid geometry at {_player.Position}.");
            }
            void Open(KnowItAllBirdCharacter bird)
            {
                int retainedPriority = bird.ZIndex;
                _player.Face(bird.Position.X < 80 ? Vector2I.Left : Vector2I.Right);
                FailIf(!bird.CanTalkTo(_player), $"$e3:${bird.Record.SubId:x2} is unreachable from {_player.Position}.");
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                FailIf(!bird.Script.ObjectsDisabled || bird.TalkingSignal || _dialogue.IsOpen,
                    "$e3 checkabutton/setdisabledobjectsto91 must yield before cplinkx and var37.");
                Step(1);
                FailIf(!bird.Talking || !bird.TalkingSignal || bird.ZFixed != 0 || _dialogue.IsOpen ||
                    bird.Direction != (bird.Position.X < _player.Position.X ? 1 : 0),
                    "$e3 cplinkx/write var37 must select animation direction+2 before showing text or jumping.");
                int firstZ = Math.Min(0, bird.SpeedZ);
                Step(1);
                FailIf(!_dialogue.IsOpen || !_dialogue.ChoiceActive || bird.ZFixed != firstZ,
                    $"$e3:${bird.Record.SubId:x2} did not open its prompt and start hopping.");
                FailIf(bird.ZIndex != retainedPriority,
                    "$e3 must retain its last idle priority when entering the talking substate.");
            }

            int flags = _saveData.GetRoomFlags(3, 0xf7);
            int rupees = _inventory.Rupees;
            foreach (var bird in birds)
            {
                int subid = bird.Record.SubId;
                int expectedX = subid is 0 or 2 or 5 or 6 or 8 ? 0x18 : 0x88;
                int expectedY = 0x21 + (subid / 2) * 0x10;
                FailIf(bird.Position != new Vector2(expectedX, expectedY) || bird.Record.Palette != palettes[subid] ||
                    bird.Record.TextId != 0x3200 + subid || bird.CurrentAnimationOpaquePixels == 0 ||
                    !PlainWords(bird.Message).Contains(topics[subid], StringComparison.Ordinal),
                    $"3:f7 $e3:${subid:x2} lost source placement, palette, prompt, or OAM.");
                Walk(new Vector2(80, _player.Position.Y));
                Walk(new Vector2(80, bird.Position.Y));
                Walk(new Vector2(expectedX == 0x18 ? 40 : 120, bird.Position.Y));
                Open(bird);

                // Native Z integrates before gravity, with no RNG while talking.
                int z = bird.ZFixed, speed = bird.SpeedZ;
                int idleCalls = _entities.RandomCalls;
                int idleCounter = bird.TurnCounter;
                int talkingPriority = bird.ZIndex;
                for (int tick = 0; tick < 30; tick++)
                {
                    z += speed;
                    if (z >= 0) { z = 0; speed = -0xc0; } else speed += 0x20;
                    Step(1);
                    FailIf(bird.ZFixed != z || bird.SpeedZ != speed || bird.ScriptDrawOffset.Y != (z >> 8) ||
                        bird.ZIndex != talkingPriority,
                        $"$e3:${subid:x2} talk jump differs at update {tick}.");
                }
                FailIf(_entities.RandomCalls != idleCalls + 9 || bird.TurnCounter != idleCounter ||
                    bird.Script.CommandIndex != 7,
                    "$e3 text must pause the script and talking counter while nine other birds keep their RNG cadence.");
                _dialogue.SubmitChoiceForValidation(1);
                Step(1);
                FailIf(bird.Script.ObjectsDisabled || !bird.Talking || !bird.TalkingSignal,
                    "$e3 No must enable all objects one update before clearing var37.");
                Step(1);
                FailIf(bird.Talking || bird.ZFixed != 0 || bird.TurnCounter != 60 || bird.TextId != 0x3200 + subid,
                    "$e3 No did not clear Z, restore idle animation/text, and set the 60-update cooldown.");
                Step(2); // copied scriptjump yields, then checkabutton waits

                Open(bird); // repeat at the same naturally reachable position
                _dialogue.SubmitChoiceForValidation(0);
                Step(1);
                FailIf(bird.Script.Counter != 30 || !bird.Script.ObjectsDisabled,
                    "$e3 Yes did not initialize wait 30 under wDisabledObjects=$91.");
                Vector2 heldPosition = _player.Position;
                StepGameplayUpdates(29, Vector2.Up, ["move_up"], ["move_up"], batched);
                FailIf(_dialogue.IsOpen || bird.Script.Counter != 1 || _player.Position != heldPosition,
                    "$e3 tutorial opened early or Link moved during the 30-update gap.");
                FailIf(_entities.PlayerMenusDisabled || !_entities.PlayerItemUsageDisabled || !_entities.PlayerUpdatesFrozen,
                    "$e3 $91 must freeze Link/items without writing wMenuDisabled.");
                Step(1);
                string tutorial = PlainWords(_dialogue.CurrentMessage);
                FailIf(!_dialogue.IsOpen || bird.TextId != 0x320a + subid ||
                    !tutorial.Contains(tutorials[subid], StringComparison.Ordinal) ||
                    !tutorial.EndsWith(tutorialEnds[subid], StringComparison.Ordinal),
                    $"$e3:${subid:x2} expected source tutorial TX_{0x320a + subid:x4}: {tutorial}");
                if (subid is 0 or 2 or 8)
                    FailIf(!bird.Message.Contains("\\stop", StringComparison.Ordinal),
                        $"TX_{bird.TextId:x4} lost its explicit page boundary.");
                observations.Add($"{subid}:{bird.Direction}:{bird.ZFixed}:{bird.SpeedZ}:{bird.Message}:{_entities.RandomCalls}");
                _dialogue.Close();
                Step(1);
                FailIf(bird.TextId != 0x3200 + subid || bird.Script.ObjectsDisabled || !bird.TalkingSignal,
                    "$e3 tutorial completion must subtract $0a and enable objects before var37 clears.");
                Step(1);
                FailIf(bird.TurnCounter != 60 || bird.ZFixed != 0 || bird.Talking,
                    "$e3 tutorial completion lost the native 60-update reset.");
                int callsBeforeCooldown = _entities.RandomCalls;
                Step(59);
                FailIf(bird.TurnCounter != 1 || _entities.RandomCalls - callsBeforeCooldown is < 9 or > 18,
                    "$e3 post-talk cooldown did not retain its 60-update boundary.");
                Step(1);
                FailIf(bird.TurnCounter != 30,
                    "$e3 post-talk cooldown did not return to the 30-update cadence on zero.");
            }
            FailIf(_saveData.GetRoomFlags(3, 0xf7) != flags || _inventory.Rupees != rupees,
                "3:f7 tutorials unexpectedly mutated room flags or inventory.");
            if (baseline is null) baseline = observations;
            else FailIf(!baseline.SequenceEqual(observations),
                "3:f7 bird script/native/RNG results differ between individual and batched host updates.");

            // Cancel in the accepted-choice wait; room re-entry must recreate all lanes.
            var last = birds[^1];
            Open(last);
            _dialogue.SubmitChoiceForValidation(0);
            Step(1);
            LoadValidationRoom(3, 0xf7);
            FailIf(_entities.PlayerUpdatesFrozen || _dialogue.IsOpen,
                "3:f7 room invalidation retained a bird's $91 input lease or text.");
            Step(3);
            FailIf(_entities.Entities<KnowItAllBirdCharacter>().Count != 10 ||
                _entities.Entities<KnowItAllBirdCharacter>().Any(b => b.Talking || b.TextId != 0x3200 + b.Record.SubId),
                "3:f7 re-entry retained tutorial text, var37, or missing birds.");

            // enabled bit $80 survives the scroll dispatcher. Preloading does
            // state 0 only; subsequent scroll slots still run the bird's tail.
            LoadValidationRoom(3, 0xf8);
            int callsBeforeScroll = _entities.RandomCalls;
            _entities.BeginScreenTransition(3, _world.LoadRoom(3, 0xf7), new Vector2(160, 0), _player);
            var incoming = _entities.Entities<KnowItAllBirdCharacter>().ToArray();
            CheckDrawOrder(incoming);
            FailIf(incoming.Length != 10 || incoming.Any(b => !b.Initialized || b.TurnCounter != 30),
                "3:f7 incoming bird state 0 was not preloaded in source order.");
            int afterPreload = _entities.RandomCalls;
            FailIf(afterPreload - callsBeforeScroll < 10,
                "3:f7 scrolling skipped the ten initial direction RNG calls.");
            _entities.Update(29.0 / 60.0, _player);
            FailIf(incoming.Any(b => b.TurnCounter != 1),
                "3:f7 enabled-bit-$80 birds froze their native timers while scrolling.");
            _entities.Update(1.0 / 60.0, _player);
            FailIf(incoming.Any(b => b.TurnCounter != 30) || _entities.RandomCalls != afterPreload + 10,
                "3:f7 scrolling lost its 30-update idle RNG boundary.");
            _entities.FinishScreenTransition();
        }
        GD.Print("Validated room 3:f7: all ten $e3 birds, source RNG, reachable repeated choices, tutorial waits, hopping, cancellation, and batched updates.");
    }
}
