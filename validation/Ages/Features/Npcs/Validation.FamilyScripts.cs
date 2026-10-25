using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateFamilyNpcScripts()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 1);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 1);
            _saveData.WriteWramByte(AgesWramAddress.wChildStatus, 2);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            _inventory.AddRupees(200);
            LoadValidationRoom(2, 0xea);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter blossom = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Blossom, SubId: 1 });
            FailIf(blossom.Position != new Vector2(0x48, 0x18) ||
                !blossom.ScriptButtonSensitive,
                "Room 2:ea Blossom $2b:$01 did not initialize its doctor talk lane.");

            void Press() => StepGameplayUpdates(1, Vector2.Zero,
                ["attack"], ["attack"], batched);
            void Step(int count = 1) => StepGameplayUpdates(count,
                Vector2.Zero, batched: batched);

            ApproachLibraryActor(blossom, batched);
            Press();
            Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "lend us some", StringComparison.OrdinalIgnoreCase),
                "Blossom's reachable first talk did not show TX_440b Yes/No.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(30);
            FailIf(_dialogue.IsOpen,
                "Blossom's No response appeared before the source wait 30 completed.");
            Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "understand", StringComparison.OrdinalIgnoreCase) ||
                _inventory.Rupees != 200 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 1,
                "Blossom's No response changed money or child stage.");
            _dialogue.Close(); Step(2);

            ApproachLibraryActor(blossom, batched);
            Press();
            Step();
            _dialogue.SubmitChoiceForValidation(0);
            Step(31);
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "150", StringComparison.Ordinal),
                "Blossom's Yes path did not show TX_440c's four payments.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(2);
            FailIf(_inventory.Rupees != 150 ||
                _saveData.ReadWramByte(AgesWramAddress.wChildStatus) != 7 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 2 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 2) == 0,
                "Blossom's 50-Rupee doctor payment did not write source money/status/bit/stage.");
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "Thank", StringComparison.OrdinalIgnoreCase),
                "Blossom did not show TX_440e after paying 50 Rupees.");
            _dialogue.Close(); Step(2);
            ApproachLibraryActor(blossom, batched);
            Press();
            Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "just enough", StringComparison.OrdinalIgnoreCase) ||
                _inventory.Rupees != 150,
                $"Repeating Blossom's payment loop charged again or skipped TX_440e: " +
                $"text='{DialogueBox.PlainText(_dialogue.CurrentMessage)}', " +
                $"rupees={_inventory.Rupees}.");
            _dialogue.Close();

            LoadValidationRoom(2, 0xeb);
            FailIf(_player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                "Leaving the family room retained its talk mask or Link control.");
            LoadValidationRoom(2, 0xea);
            Step(3);
            blossom = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Blossom, SubId: 1 });
            ApproachLibraryActor(blossom, batched);
            Press(); Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "doctor", StringComparison.OrdinalIgnoreCase) ||
                _inventory.Rupees != 150,
                "Reloaded Blossom did not read wc6e2 bit $01 and show TX_4431.");
            _dialogue.Close();
        }
        GD.Print("Validated family stage-$01 doctor choice, 30-update wait, " +
            "50-Rupee status/flag/stage writes, repeat and cancellation through " +
            "room geometry in split and batched gameplay updates.");
    }

    private void ValidateFamilyChildSlackerReward()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 0);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage8Response, 0xff);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            _inventory.AddRupees(100);
            LoadValidationRoom(2, 0xea);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x0e });
            FailIf(child.Position != new Vector2(0x78, 0x44) ||
                !child.ScriptButtonSensitive,
                "Room 2:ea child $35 var03=$0e did not initialize slacker stage $08.");

            void Step(int count = 1) => StepGameplayUpdates(count,
                Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero,
                ["attack"], ["attack"], batched);

            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "contribution", StringComparison.OrdinalIgnoreCase),
                "Child $35 stage $08 did not show TX_4b01's request.");
            _dialogue.SubmitChoiceForValidation(0);
            Step(31);
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "50", StringComparison.Ordinal),
                "Slacker's Yes branch did not show TX_4b02's four payments.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(30);
            FailIf(_inventory.Rupees != 50 ||
                _saveData.ReadWramByte(AgesWramAddress.wChildStage8Response) != 1 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 9 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x20) == 0,
                "Slacker's 50-Rupee offer did not write response $01/bit $05/next stage $09.");
            Step(2);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "That's it", StringComparison.Ordinal),
                "Slacker did not show TX_4b05 after the payment wait 30.");
            _dialogue.Close(); Step(2);
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(_inventory.Rupees != 50 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "That's it", StringComparison.Ordinal),
                "Slacker's same-room response charged again.");
            _dialogue.Close();

            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0x3f);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 2);
            LoadValidationRoom(2, 0xea);
            Step(3);
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStage) != 9 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wSeedTreeRefilledBitset) != 0,
                "The source six-Essence/refill gate did not advance slacker to stage $09.");
            child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x12 });
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "donation", StringComparison.OrdinalIgnoreCase) ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x40) != 0,
                "Stage-$09 slacker skipped TX_4b0a or set reward bit $06 before text closed.");
            _dialogue.Close();
            Step();
            FailIf((_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x40) == 0,
                "Closing TX_4b0a did not set child reward bit $06.");
            Step(30);
            FailIf(_inventory.Rupees != 250 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "200", StringComparison.Ordinal),
                "Stage-$09 response $01 did not grant 200 Rupees and show TX_0009.");
            _dialogue.Close(); Step(31);
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(_inventory.Rupees != 250 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "buds", StringComparison.OrdinalIgnoreCase),
                "Repeating stage-$09 slacker talk regranted the reward.");
            _dialogue.Close();
            LoadValidationRoom(2, 0xeb);
            FailIf(_player.CutsceneControlled,
                "Room exit retained slacker stage-$09 cutscene control.");
        }
        GD.Print("Validated slacker stage-$08 50-Rupee response, six-Essence " +
            "stage-$09 advance, 200-Rupee reward, repeat, room exit and " +
            "split/batched gameplay updates.");
    }

    private void ValidateFamilyChildArboristSeed()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 2);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            LoadValidationRoom(2, 0xeb);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x10 });
            ApproachLibraryActor(child, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "trees", StringComparison.OrdinalIgnoreCase) ||
                _inventory.GashaSeeds != 0,
                "Arborist stage-$08 did not show TX_4801 before granting its Gasha Seed.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            GroundTreasurePickup? seed = _entities.Entities<GroundTreasurePickup>().SingleOrDefault();
            FailIf(_inventory.GashaSeeds != 1 || seed is null || !seed.Held ||
                seed.Record is not { SpawnMode: TreasureSpawnMode.Instant,
                    GrabMode: TreasureGrabMode.TwoHands,
                    CompletionOwner: GroundTreasureCompletionOwner.Caller } ||
                !_player.IsHoldingItemTwoHands ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x20) != 0,
                $"Arborist giveitem handoff differs: seeds={_inventory.GashaSeeds}, " +
                $"pickup={seed is not null}, held={seed?.Held}, " +
                $"grab={seed?.Record.GrabMode}, owner={seed?.Record.CompletionOwner}, " +
                $"playerHeld={_player.IsHoldingItemTwoHands}, " +
                $"flags=${_saveData.ReadWramByte(AgesWramAddress.wc6e2):x2}.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 ||
                _player.IsHoldingItemTwoHands ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x20) == 0 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 9,
                "Closing the Gasha Seed text did not finish the item and advance arborist stage.");
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "greenery", StringComparison.OrdinalIgnoreCase),
                "Arborist did not show TX_4802 after wait 30.");
            _dialogue.Close();
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            ApproachLibraryActor(child, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_inventory.GashaSeeds != 1 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "trees", StringComparison.OrdinalIgnoreCase),
                "Repeating arborist stage-$08 talk regranted the Gasha Seed.");
            _dialogue.Close();
            LoadValidationRoom(2, 0xea);
            FailIf(_player.CutsceneControlled,
                "Room exit retained arborist item control.");
        }
        GD.Print("Validated arborist stage-$08 source Gasha Seed $03 held-item " +
            "handoff, bit $05/next stage $09, wait 30, repeat and cancellation " +
            "in split/batched gameplay updates.");
    }

    private void ValidateFamilyChildNativeMotion()
    {
        var motion = new FamilyChildMotionDatabase();
        FailIf(motion.AnimationBase(0) != 0 ||
            motion.AnimationBase(1) != 2 ||
            motion.AnimationBase(2) != 5 ||
            motion.HyperactiveAngle(0) != 0x18 ||
            motion.HyperactiveAngle(1) != 0x0a ||
            motion.HyperactiveAngle(3) != 0x06 ||
            motion.Waypoints(0).Count != 8 ||
            motion.Waypoints(0)[0] != new Vector2I(0x18, 0x68) ||
            motion.Waypoints(2).Count != 12 ||
            motion.Waypoints(2)[11] != new Vector2I(0x4c, 0x38),
            "child.s lost source animation bases, hyperactive angles or patrol waypoints.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 4);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            LoadValidationRoom(2, 0xea);
            NpcCharacter hyperactive = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 1 });
            FailIf(hyperactive.Position != new Vector2(0x68, 0x38),
                "Stage-$04 hyperactive child did not spawn at source X=$68,Y=$38.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(hyperactive.Position != new Vector2(0x68, 0x38) ||
                hyperactive.ScriptButtonSensitive,
                "INTERAC_CHILD state $00 moved or ran its script.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(hyperactive.Position.X >= 0x68 ||
                !hyperactive.ScriptButtonSensitive,
                "Hyperactive child did not begin SPEED_180 left motion and talk in state $01.");
            string initialAnimation = hyperactive.CurrentScriptAnimationSource;
            StepGameplayUpdates(50, Vector2.Zero, batched: batched);
            FailIf(hyperactive.Position.X < 0x28 || hyperactive.Position.X > 0x69 ||
                hyperactive.CurrentScriptAnimationSource == initialAnimation,
                "Hyperactive child missed the source X=$29..$68 turn and animation flip.");

            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 2);
            LoadValidationRoom(2, 0xea);
            NpcCharacter curious = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 3 });
            Vector2 curiousStart = curious.Position;
            _sound.ClearPlayRequestAudit();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(curious.Position != curiousStart ||
                _sound.PlayRequestsFor(SoundId.SndJump) != 1 ||
                curious.ScriptButtonSensitive,
                "Curious child did not begin its state-$01 jump before talk execution.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(curious.Position.X >= curiousStart.X ||
                curious.ScriptButtonSensitive,
                "Curious child did not move left while airborne with script suppressed.");
            StepGameplayUpdates(40, Vector2.Zero, batched: batched);
            FailIf(!curious.ScriptButtonSensitive ||
                _sound.PlayRequestsFor(SoundId.SndJump) != 1,
                "Curious child did not resume talk on landing or replayed SND_JUMP early.");
            StepGameplayUpdates(121, Vector2.Zero, batched: batched);
            FailIf(_sound.PlayRequestsFor(SoundId.SndJump) != 2,
                "Curious child did not restart its jump after source delay $78.");
        }
        GD.Print("Validated child.s stage-$04 state-zero gate, hyperactive " +
            "SPEED_180 edge turn, curious jump/script suppression, landing and " +
            "$78 replay across split/batched gameplay updates.");
    }

    private void ValidateFamilyBlossomStage6Advice()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 6);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 6);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 0);
            _saveData.WriteWramByte(AgesWramAddress.wChildStatus, 5);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            LoadValidationRoom(2, 0xeb);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter blossom = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Blossom, SubId: 6, Var03: 0 });
            ApproachLibraryActor(blossom, batched);
            void Step(int count = 1) => StepGameplayUpdates(count,
                Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero,
                ["attack"], ["attack"], batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "hyperactive", StringComparison.OrdinalIgnoreCase),
                "Blossom stage-$06 did not reach TX_4419's childhood question.");
            _dialogue.SubmitChoiceForValidation(0);
            Step(30);
            FailIf(_dialogue.IsOpen ||
                _saveData.ReadWramByte(AgesWramAddress.wChildStatus) != 5,
            "Blossom recorded her childhood response before wait 30.");
            Step();
            FailIf(_dialogue.IsOpen,
                "callscript's copied-buffer yield did not precede Blossom's wait 30.");
            Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "mischievous", StringComparison.OrdinalIgnoreCase) ||
                _saveData.ReadWramByte(AgesWramAddress.wChildStatus) != 5,
                $"Blossom skipped TX_441c before changing child status: " +
                $"text='{DialogueBox.PlainText(_dialogue.CurrentMessage)}', " +
                $"status=${_saveData.ReadWramByte(AgesWramAddress.wChildStatus):x2}.");
            _dialogue.Close(); Step(2);
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStatus) != 13 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x08) == 0 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 6,
                "Blossom Yes-$01 did not add $08, set bit $03 and leave next stage unchanged.");
            ApproachLibraryActor(blossom, batched);
            Press(); Step();
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStatus) != 13 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "hyperactive", StringComparison.OrdinalIgnoreCase),
                "Blossom repeat advice changed status or missed TX_4422.");
            _dialogue.Close();
            LoadValidationRoom(2, 0xea);
            FailIf(_player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                "Leaving Blossom stage-$06 retained input or object mask.");
        }
        GD.Print("Validated Blossom stage-$06 source callscript Yes-$01, " +
            "wait 30, child status +$08, bit $03, repeat and room exit " +
            "through split/batched gameplay updates.");
    }

    private void ValidateFamilyChildWarriorReward()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 1);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage8Response, 0xff);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            LoadValidationRoom(2, 0xeb);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x0f });
            void Step(int count = 1) => StepGameplayUpdates(count,
                Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero,
                ["attack"], ["attack"], batched);
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "Training daily", StringComparison.OrdinalIgnoreCase),
                "Warrior stage-$08 did not show TX_4a01 from a moving floor approach.");
            _dialogue.SubmitChoiceForValidation(0);
            Step(31);
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStage8Response) != 0 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x20) == 0 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 9 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "learned", StringComparison.OrdinalIgnoreCase),
                "Warrior's daily-training answer did not set response $00/bit $05/stage $09.");
            _dialogue.Close(); Step(31);

            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0x3f);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 2);
            LoadValidationRoom(2, 0xea);
            Step(3);
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStage) != 9,
                "Warrior did not advance to stage $09 after the six-Essence refill gate.");
            child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x13 });
            int rupees = _inventory.Rupees;
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "great hero", StringComparison.OrdinalIgnoreCase) ||
                _inventory.Rupees != rupees,
                "Warrior stage-$09 did not show TX_4a06 before granting a reward.");
            _dialogue.Close(); Step(31);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "thanks", StringComparison.OrdinalIgnoreCase) ||
                _inventory.Rupees != rupees,
                "Warrior stage-$09 skipped TX_4a07 or granted before the first wait 30.");
            _dialogue.Close(); Step(31);
            FailIf(_inventory.Rupees != rupees + 100 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x40) == 0 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "100", StringComparison.Ordinal),
                "Warrior response $00 did not grant 100 Rupees after the second wait 30.");
            _dialogue.Close(); Step(31);
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(_inventory.Rupees != rupees + 100 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "training", StringComparison.OrdinalIgnoreCase),
                "Repeating warrior stage-$09 talk regranted the reward.");
            _dialogue.Close();
            LoadValidationRoom(2, 0xeb);
            FailIf(_player.CutsceneControlled,
                "Room exit retained warrior cutscene input control.");
        }
        GD.Print("Validated moving warrior stage-$08 answer $00, six-Essence " +
            "stage-$09 gate, two wait-30 texts, 100-Rupee reward, repeat and " +
            "room exit through split/batched gameplay updates.");
    }

    private void ValidateFamilyChildSingerSong()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wNextChildStage, 8);
            _saveData.WriteWramByte(AgesWramAddress.wChildPersonality, 3);
            _saveData.WriteWramByte(AgesWramAddress.wChildStage8Response, 0xff);
            _saveData.WriteWramByte(AgesWramAddress.wc6e2, 0);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 0);
            LoadValidationRoom(2, 0xea);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x11 });
            void Step(int count = 1) => StepGameplayUpdates(count,
                Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero,
                ["attack"], ["attack"], batched);
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "love or courage", StringComparison.OrdinalIgnoreCase),
                "Singer stage-$08 did not show TX_4601 from room floor geometry.");
            _dialogue.SubmitChoiceForValidation(1);
            Step();
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStage8Response) != 1 ||
                (_saveData.ReadWramByte(AgesWramAddress.wc6e2) & 0x20) == 0 ||
                _saveData.ReadWramByte(AgesWramAddress.wNextChildStage) != 9,
                "Singer's Courage answer did not store response $01, bit $05 and next stage $09.");
            Step(30);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "almost complete", StringComparison.OrdinalIgnoreCase),
                "Singer stage-$08 did not show TX_4602 after the wait.");
            _dialogue.Close(); Step();
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "almost complete", StringComparison.OrdinalIgnoreCase),
                "Singer stage-$08 did not show TX_4602 on repeat.");
            _dialogue.Close();

            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0x3f);
            _entities.RuntimeState.SetWramByte(AgesWramAddress.wSeedTreeRefilledBitset, 2);
            LoadValidationRoom(2, 0xea);
            Step(3);
            FailIf(_saveData.ReadWramByte(AgesWramAddress.wChildStage) != 9,
                "Singer did not advance to stage $09 after the six-Essence refill gate.");
            child = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Child, Var03: 0x15 });
            _inventory.SetScriptedHealth(4);
            _sound.ClearPlayRequestAudit();
            ApproachLibraryActor(child, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "listen", StringComparison.OrdinalIgnoreCase),
                "Singer stage-$09 did not offer TX_4603.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(31);
            FailIf(_inventory.HealthQuarters != 4 ||
                _sound.PlayRequestsFor(0x4a) != 0 ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "anytime", StringComparison.OrdinalIgnoreCase),
                "Singer's No branch healed or played MUS_PRECREDITS before TX_4605.");
            _dialogue.Close(); Step();
            ApproachLibraryActor(child, batched);
            Press(); Step();
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(_inventory.HealthQuarters != _inventory.MaxHealthQuarters ||
                _sound.PlayRequestsFor(0x4a) != 1,
                "Singer's Yes branch did not play MUS_PRECREDITS $4a and refill health.");
            Step(30);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "How was it", StringComparison.OrdinalIgnoreCase),
                "Singer did not show TX_4604 after wait 30.");
            _dialogue.Close();
            LoadValidationRoom(2, 0xeb);
            FailIf(_player.CutsceneControlled,
                "Room exit retained singer stage-$09 cutscene control.");
        }
        GD.Print("Validated singer stage-$08 Courage response, six-Essence " +
            "stage-$09 gate, No/Yes song branches, MUS_PRECREDITS $4a, heal, " +
            "wait 30 and room exit in split/batched gameplay updates.");
    }
}
