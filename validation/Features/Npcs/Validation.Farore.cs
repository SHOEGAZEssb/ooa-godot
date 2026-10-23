using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFaroreDialogueInput()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50);
            LoadValidationRoom(5, 0xcf);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            void AdvanceUntil(Func<bool> completed, string boundary)
            {
                for (int i = 0; i < 250 && !completed(); i++) { Press(); Step(7); }
                FailIf(!completed(), $"Farore dialogue input stalled at {boundary}: '{_dialogue.CurrentMessage}'.");
            }
            Step(3);
            var farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            string greeting = _dialogue.CurrentMessage;
            FailIf(_dialogue.TextboxFlagsForValidation != 0x08,
                "farore.s state zero requires TEXTBOXFLAG_DONTCHECKPOSITION $08, independently of position $02.");
            AdvanceUntil(() => _dialogue.CurrentMessage != greeting, "TX_5506 final-page dismissal");
            FailIf(!_dialogue.ChoiceActive, "Farore TX_5506 did not hand off to the TX_5507 Yes/No prompt.");
            FailIf(_dialogue.TextboxFlagsForValidation != 0x08,
                "Farore's choice prompt lost TEXTBOXFLAG_DONTCHECKPOSITION $08.");
            AdvanceUntil(() => _dialogue.ChoiceCursorVisible, "TX_5507 pages and choice cursor");
            Press(); Step(5);
            FailIf(!_secretEntry.IsActive, "Farore's input-confirmed Yes did not open MENU_SECRET.");
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(0x30, _saveData));
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            Step(240);
            FailIf(!_dialogue.IsOpen || _inventory.SwordLevel != 2,
                "Farore's input-confirmed secret did not reach the held sword message.");
            string pickup = _dialogue.CurrentMessage;
            AdvanceUntil(() => !_dialogue.IsOpen || _dialogue.CurrentMessage != pickup, "sword pickup dismissal");
            Step(120);
            FailIf(!_saveData.HasGlobalFlag(0x5a) || !_dialogue.IsOpen ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains("power", StringComparison.OrdinalIgnoreCase),
                "Farore's completed upgrade did not show TX_5509.");
            AdvanceUntil(() => !_dialogue.IsOpen, "TX_5509 reward acknowledgment");
            Step(2);
            FailIf(_player.CutsceneControlled || _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
                "Farore's input-dismissed reward retained control or the disabled-object mask.");
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            AdvanceUntil(() => _dialogue.ChoiceCursorVisible, "repeat TX_5507 prompt");
            StepGameplayUpdates(1, Vector2.Zero, ["move_right"], ["move_right"], batched);
            Step(); Press(); Step(5);
            FailIf(!_dialogue.IsOpen || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Really?", StringComparison.OrdinalIgnoreCase),
                "Farore's input-selected No did not show TX_5508.");
            AdvanceUntil(() => !_dialogue.IsOpen, "TX_5508 refusal dismissal");
            Step(2);
            FailIf(_player.CutsceneControlled || _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
                "Farore's input-dismissed refusal retained control.");
        }
        GD.Print("Validated Farore greeting, multipage Yes/No prompt, secret handoff, sword pickup, reward acknowledgment and repeat refusal through A/direction input in split/batched gameplay.");
    }

    private void StartFaroreRewardForValidation(int quest, bool batched)
    {
        ReinitializeGameplayForValidation();
        _saveData.SetLinkedGame(true);
        _saveData.SetGlobalFlag(0x50 + quest);
        LoadValidationRoom(5, 0xcf);
        void Step() => StepGameplayUpdates(1, Vector2.Zero, batched: batched);
        StepGameplayUpdates(3, Vector2.Zero, batched: batched);
        var farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
        ApproachLibraryActor(farore, batched);
        StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched); Step();
        _dialogue.Close(); Step();
        _dialogue.SubmitChoiceForValidation(0); Step();
        for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
        _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(0x30 + quest, _saveData));
        for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
        for (int i = 0; i < 10 && !_entities.EntityAdapters<FaroreRewardRoomEntity>().Any(); i++) Step();
        FailIf(!_entities.EntityAdapters<FaroreRewardRoomEntity>().Any(), "Farore did not allocate reward $d9 through MENU_SECRET.");
    }

    private void ValidateFaroreUpgradePresentation()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool exhaustSlots in new[] { false, true })
        {
            StartFaroreRewardForValidation(0, batched);
            int initialSwordLevel = _inventory.SwordLevel;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step(); // $d9 state2/substate0: take Link's position, counter1=30.
            if (exhaustSlots)
            {
                var visuals = new FaroreVisualDatabase();
                while (_entities.InteractionSlotAvailable)
                    _entities.TryCreateInteraction(new InteractionSparkleSpawn(new(0x38, 0x58), 0, visuals.UpgradeSparkle));
            }
            Step(29);
            FailIf(_entities.Entities<InteractionSparkleEffect>().Any(c => c.Position == new Vector2(0x58, 0x28)),
                "$d9 upgrade glow appeared before its 30-update wait.");
            Step();
            if (exhaustSlots)
            {
                Step(59);
                FailIf(_entities.Entities<InteractionSparkleEffect>().Any(),
                    "$d9 failed glow allocation did not retain the full 60-update retry counter.");
                Step();
            }
            FailIf(_entities.Entities<InteractionSparkleEffect>().Count(c => c.Position == new Vector2(0x58, 0x28)) != 1,
                "$d9 upgrade did not allocate its $84:$00 glow at Farore's source position.");
            var glow = _entities.Entities<InteractionSparkleEffect>().Single();
            FailIf(glow.ElapsedUpdates != 1 || glow.SourceAngle != 0 || glow.ZIndex != 9 ||
                glow.AnimationFrame != 0 || glow.AnimationParameter != 0,
                "$d9 shared $84:$00 glow lost its setup-only update or visible82 priority.");
            Step(34);
            FailIf(glow.AnimationFrame != 3 || glow.AnimationParameter != 0 || glow.Finished,
                "$d9 shared $84:$00 glow lost the source 9/9/9/8 animation boundaries.");
            Step();
            FailIf(glow.AnimationParameter != 0xff || glow.Finished,
                "$d9 shared $84:$00 glow did not retain its terminal parameter for one update.");
            Step();
            FailIf(_entities.Entities<InteractionSparkleEffect>().Any(),
                "$d9 shared $84:$00 glow did not delete on the update after parameter $ff.");
            Step(23);
            FailIf(_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any(), "$d9 energy swirl appeared before the 60-update glow wait.");
            Step();
            FailIf(_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Count() != 8,
                "$d9 did not create eight native PART $53 beads after the glow.");
            Step(20);
            FailIf(_transitions.NativePaletteThreadMode != 2, "$d9 first flash did not follow the 20-update energy wait.");
            Step(20); Step(20);
            FailIf(_transitions.NativePaletteThreadMode != 0x0a || _inventory.SwordLevel != initialSwordLevel,
                "$d9 final delay-$02 flash must precede the sword grant.");
            Step(64);
            FailIf(_inventory.SwordLevel != initialSwordLevel, "$d9 granted the sword before palette update 65.");
            Step();
            FailIf(_inventory.SwordLevel != 2 || !_dialogue.IsOpen || _saveData.HasGlobalFlag(0x5a),
                "$d9 did not grant its held sword on palette completion while retaining the done flag.");
            LoadValidationRoom(5, 0xd0);
            FailIf(_transitions.NativePaletteFadeActive || _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any(),
                "Farore upgrade cancellation retained its palette or PART energy swirl.");
        }
        GD.Print("Validated Farore upgrade glow, failed-allocation retry, native energy swirl, three flashes and palette-gated grant in split/batched updates.");
    }

    private void ValidateFaroreChestAllocationFailure()
    {
        foreach (bool batched in new[] { false, true })
        {
            StartFaroreRewardForValidation(1, batched);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            var parent = _entities.EntityAdapters<FaroreMakeChestRoomEntity>().Single();
            var visuals = new FaroreVisualDatabase();
            void FillSlots()
            {
                while (_entities.InteractionSlotAvailable)
                    _entities.TryCreateInteraction(new InteractionSparkleSpawn(new(0x38, 0x58), 0, visuals.UpgradeSparkle));
            }
            Step(89); FillSlots(); Step();
            FailIf(parent.State != 2 || _entities.EntityAdapters<FaroreSparkleRoomEntity>().Any(c => c.SubId != 0),
                "$11 parent did not continue when objectData_faroreSparkle ran out of interaction slots.");
            Step(48 + 30 + 80 + 39); FillSlots();
            var writes = new System.Collections.Generic.List<(int Address, byte Value)>();
            void Capture(int address, byte value) => writes.Add((address, value));
            _entities.NativeSoundAllocationAliasWritten += Capture;
            int page = 0xd0 + _entities.NativeInteractionSlot(parent);
            try { Step(); }
            finally { _entities.NativeSoundAllocationAliasWritten -= Capture; }
            FailIf(!writes.SequenceEqual(new[] { (0xc056, (byte)0x40), (0xc057, (byte)page) }) ||
                parent.State != 6 || _currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != 0xf1,
                "$11 failed $84:$0c allocation lost the unchecked $e056/$e057 sound aliases or skipped chest creation.");
            LoadValidationRoom(5, 0xd0);
        }
        GD.Print("Validated Farore exhausted interaction stream, unchecked related-object sound aliases and continued chest creation.");
    }

    private void ValidateFaroreChestPresentation()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int cancelState in new[] { 1, 4, 10, -1 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x51);
            LoadValidationRoom(5, 0xcf);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step(3);
            var farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            ApproachLibraryActor(farore, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched); Step();
            _dialogue.Close(); Step();
            _dialogue.SubmitChoiceForValidation(0); Step();
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(new LinkedGameNpcDatabase().GenerateSecretValues(0x31, _saveData));
            for (int i = 0; i < 22; i++) _secretEntry.Update(1.0 / 60.0);
            for (int i = 0; i < 10 && !_entities.EntityAdapters<FaroreMakeChestRoomEntity>().Any(); i++) Step();
            var parent = _entities.EntityAdapters<FaroreMakeChestRoomEntity>().Single();
            FailIf(parent.State != 1 || parent.Counter != 0x5a ||
                _transitions.NativeCameraFocus != new Vector2(0x58, 0x78) ||
                _transitions.NativePaletteThreadMode != 5,
                "$11:$00 initialization lost its $5a wait, chest camera focus or BG-only darkening.");
            if (cancelState != 1)
            {
                Step(9);
                FailIf(_currentRoom.TemporaryBackgroundPaletteOffset != -9,
                    "Ages darkenRoomLightly did not reach BG component offset $f7 after nine updates.");
                Step(80);
                FailIf(_entities.EntityAdapters<FaroreSparkleRoomEntity>().Any(),
                    "$11 sparkles appeared before the $5a counter expired.");
                Step();
                var children = _entities.EntityAdapters<FaroreSparkleRoomEntity>().OrderBy(c => c.SubId).ToArray();
                FailIf(children.Length != 8 || children.Where((c, i) => c.SubId != i * 16 + 1 ||
                    c.Angle != 2 + i * 4 || c.State != 1 || c.Node.Position != new Vector2(0x58, 0x78)).Any(),
                    "objectData_faroreSparkle lost eight source-ordered slots, initial angles or setup-only birth updates.");
                Step(47);
                FailIf(children.Any(c => c.State != 1), "$11:$x1 outward flight ended before 48 speed-$28 updates.");
                Step();
                FailIf(children.Any(c => c.State != 2), "$11:$x1 outward flight did not end on update 48.");
                Step(30);
                FailIf(parent.State != 4 || parent.Counter != 0x50 || parent.SparkleRadius != 0x30,
                    "$11:$00 did not preserve the $30/$1e waits before contracting.");
                if (cancelState != 4)
                {
                    Step(80);
                    FailIf(parent.State != 5 || parent.SparkleRadius != 8,
                        "$11:$00 contraction must decrement $cfd8 on exactly 40 even updates.");
                    Step(40);
                    FailIf(parent.State != 6 || _entities.RuntimeState.ReadWramByte(0xcfd9) != 1 ||
                        children.Any(c => c.State != 3) ||
                        _currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != 0xf1 ||
                        !_entities.EntityAdapters<FaroreSparkleRoomEntity>().Any(c => c.SubId == 0x0c),
                        "$11:$00 chest creation lost the flyaway signal, $84:$0c glow or tile $f1.");
                    Step(8 + 16 * 3);
                    FailIf(parent.State != 10 || !_transitions.NativePaletteFadeActive || parent.Finished ||
                        _saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting),
                        "$11 state $0a must wait for the fourth flash before publishing completion.");
                    if (cancelState == -1)
                    {
                        Step(128);
                        FailIf(parent.Finished, "$11:$00 completed before the delay-$04 palette thread's terminal update.");
                        Step();
                        FailIf(!parent.Finished || _transitions.NativePaletteFadeActive ||
                            _transitions.NativeCameraFocus is not null,
                            "$11:$00 did not finish on palette update 129 and return camera ownership to Link.");
                        Step(60);
                        FailIf(_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting),
                            "$d9 reward signal skipped its following-slot observation and $3c wait.");
                        Step();
                        FailIf(!_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) || !_dialogue.IsOpen,
                            "$d9 did not publish TX_5509 after the palette completion and $3c wait.");
                        _dialogue.Close(); Step();
                    }
                }
            }
            LoadValidationRoom(5, 0xd0);
            FailIf(_transitions.NativePaletteFadeActive || _transitions.NativeCameraFocus is not null ||
                _entities.EntityAdapters<FaroreSparkleRoomEntity>().Any() ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) == 0x91,
                $"Farore cancellation at state ${cancelState:x2} retained palette, camera, children or object mask.");
        }
        GD.Print("Validated Farore native sparkle births, 48-update flight, orbit/contraction, flyaway, " +
            "four flashes, palette completion, reward signal and cancellation in split/batched gameplay.");
    }

    private void ValidateFaroreScript()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (int index in new[] { 0x20, 0x30, 0x39 })
        {
            byte[] values = codec.GenerateSecretValues(index, _saveData);
            FailIf(!codec.TryDecodeShortSecret(values, _saveData, out int decoded) || decoded != index,
                $"Farore MENU_SECRET $ff lost decoded short index ${index:x2}.");
        }
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Farore, SubId: 0 });
            FailIf(farore.Position != new Vector2(0x58, 0x28) ||
                farore.ObjectCollisionBounds.Size != new Vector2(12, 40),
                "Farore $10 state-one position or $14/$06 collision changed.");
            ApproachLibraryActor(farore, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.IsOpen || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("secrets", StringComparison.OrdinalIgnoreCase),
                "Unlinked pre-completion Farore did not show TX_5501 from room floor geometry.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Farore room exit retained input ownership.");

            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(GlobalFlag.SecretChestWaiting);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            FailIf(_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting),
                "Farore $10 state zero did not clear GLOBALFLAG_SECRET_CHEST_WAITING $2c.");
            ApproachLibraryActor(farore, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.IsOpen || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Farore", StringComparison.OrdinalIgnoreCase),
                "Linked Farore did not show TX_5506 on first A contact.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("secret", StringComparison.OrdinalIgnoreCase),
                "Linked Farore did not offer TX_5507 after TX_5506 closed.");
            _dialogue.SubmitChoiceForValidation(1);
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("anytime", StringComparison.OrdinalIgnoreCase),
                "Linked Farore's No branch did not show TX_5508.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Linked Farore cancellation retained input control.");
        }
        GD.Print("Validated Farore $10 source position/radii, secret decoder, " +
            "unlinked opening and linked greeting/No branch through geometry " +
            "in split/batched gameplay updates.");
    }

    private void ValidateFaroreSecretResponses()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (int shortIndex in new[] { 0x20, 0x30 })
        foreach (int status in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50);
            _saveData.SetGlobalFlag(0x5a, status == 2);
            _saveData.SetGlobalFlag(0x50, status != 0);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            _dialogue.Close(); Step();
            FailIf(!_dialogue.ChoiceActive,
                "Linked Farore did not offer TX_5507 before MENU_SECRET.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased,
                "Farore askforsecret $ff did not acquire the shared MENU_SECRET lifecycle.");
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive,
                "Invalid Farore checksum closed MENU_SECRET.");
            _secretEntry.Submit(codec.GenerateSecretValues(shortIndex, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(2);
            string message = DialogueBox.PlainText(_dialogue.CurrentMessage);
            int expectedText = shortIndex == 0x20 ? 0x550b : status switch
            {
                0 => 0x551c, 2 => 0x550c, _ => -1
            };
            if (expectedText >= 0)
                FailIf(message != DialogueBox.PlainText(new FaroreScriptDatabase().Text(expectedText)),
                    $"Farore secret ${shortIndex:x2} status {status} lost TX_{expectedText:x4}: '{message}'.");
            else
                FailIf(!_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects).Equals((byte)0x91),
                    "Accepted Farore secret released object mask before reward signal.");
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled ||
                _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) == 0x91,
                "Farore room exit retained secret script input or object mask.");
        }
        GD.Print("Validated Farore $ff MENU_SECRET entry, invalid checksum, " +
            "wrong-game $20, inactive $30, already-done $30 and accepted $30 " +
            "through room geometry in split/batched gameplay updates.");
    }

    private void ValidateFaroreRewards()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        foreach (int quest in new[] { 0, 1, 2, 3, 6, 8 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50 + quest);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            _dialogue.Close(); Step();
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30 + quest, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(3);
            FailIf(_saveData.HasGlobalFlag(0x5a + quest),
                $"Farore marked quest ${quest:x2} done before its reward.");
            Step(quest == 0 ? 240 : 600);
            if (quest == 0)
            {
                FailIf(_inventory.SwordLevel != 2 || _saveData.HasGlobalFlag(0x5a),
                    $"Farore sword upgrade did not grant TREASURE_OBJECT_SWORD_01 parameter $02 before cleanup " +
                    $"(level={_inventory.SwordLevel}, done={_saveData.HasGlobalFlag(0x5a)}, " +
                    $"mask={_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}, " +
                    $"text='{_dialogue.CurrentMessage}').");
                FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "Noble", StringComparison.OrdinalIgnoreCase),
                    $"Farore sword upgrade did not use TREASURE_OBJECT_SWORD_01 pickup text " +
                    $"(text='{_dialogue.CurrentMessage}').");
                _dialogue.Close();
                Step(30);
                FailIf(_saveData.HasGlobalFlag(0x5a),
                    "Farore sword marked the secret done before its silent spin-sword grant.");
                Step(90);
                FailIf(!_saveData.HasGlobalFlag(0x5a) ||
                    !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                        "power", StringComparison.OrdinalIgnoreCase),
                    "Farore sword upgrade did not mark secret $00 and show TX_5509.");
                _dialogue.Close(); Step();
                FailIf(_player.CutsceneControlled,
                    "Farore sword upgrade did not return Link input after reward signal.");
            }
            else
            {
                Step(120);
                int expectedTreasure = quest switch
                {
                    1 => TreasureId.HeartContainer,
                    2 => TreasureId.Bombchus,
                    3 or 6 => TreasureId.Ring,
                    8 => TreasureId.BiggoronSword,
                    _ => throw new InvalidOperationException("Unexpected Farore chest quest.")
                };
                int expectedSubid = quest switch { 3 => 0x0c, 6 => 0x0d, _ => 1 };
                FailIf(_currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != ChestDatabase.ClosedTile ||
                    !_saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                    _saveData.HasGlobalFlag(0x5a + quest) ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wChestContentsOverride) != expectedTreasure ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != expectedSubid,
                    $"Farore chest quest ${quest:x2} did not publish tile $f1, " +
                    "source treasure override and waiting flag $2c before collection.");
                _dialogue.Close(); Step();
                FailIf(_player.CutsceneControlled,
                    "Farore chest signal did not return Link input before collection.");
                Vector2 south = new(0x58, 0x98);
                FailIf(_collision.Collides(south) || _entities.BlocksLink(south) ||
                    _currentRoom.IsSolid(south),
                    "Farore chest's south approach is blocked by room geometry.");
                _player.WarpTo(south); _player.Face(Vector2I.Up);
                for (int i = 0; i < 24 && !_player.FacesTileWallForInteraction; i++)
                    StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"], batched);
                FailIf(!_player.FacesTileWallForInteraction,
                    $"Farore chest approach did not publish both wall probes " +
                    $"(Link={_player.Position}, frontTile={_currentRoom.GetMetatile(_player.Position + Vector2.Up * 8):x2}, " +
                    $"frontSolid={_currentRoom.IsSolid(_player.Position + Vector2.Up * 8)}).");
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    "Farore chest approach entered solid geometry.");
                Press();
                FailIf(!_interactions.ChestRewardActive,
                    $"Farore secret chest quest ${quest:x2} did not open from the actual south floor " +
                    $"(Link={_player.Position}, tile={_currentRoom.GetMetatile(_player.Position + Vector2.Up * 8):x2}).");
                Step(36);
                FailIf(quest switch
                {
                    1 => !_inventory.HasTreasure(TreasureId.HeartContainer),
                    2 => _inventory.Bombchus != 0x10,
                    3 or 6 => _inventory.UnappraisedRingCount != 1,
                    8 => !_inventory.HasTreasure(TreasureId.BiggoronSword),
                    _ => true
                }, $"Farore chest quest ${quest:x2} did not grant source treasure ${expectedTreasure:x2}:${expectedSubid:x2}.");
                _dialogue.Close(); Step(2);
                Step(32);
                FailIf(!_saveData.HasGlobalFlag(0x5a + quest) ||
                    _saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                    _currentRoom.GetMetatile(new Vector2(0x58, 0x78)) != 0xac,
                    $"Farore chest quest ${quest:x2} completion did not set done flag, clear $2c and restore tile $ac.");
            }
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive, "Farore could not be approached again after completing a reward.");
            _dialogue.SubmitChoiceForValidation(0); Step();
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30 + quest, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(2);
            FailIf(DialogueBox.PlainText(_dialogue.CurrentMessage) !=
                DialogueBox.PlainText(new FaroreScriptDatabase().Text(0x550c)) ||
                _entities.EntityAdapters<FaroreRewardRoomEntity>().Any(),
                $"Farore repeat secret ${quest:x2} did not show TX_550c without spawning another reward.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Farore reward room exit retained input control.");
        }
        GD.Print("Validated Farore quest $00 upgrade and five native chest rewards " +
            "through MENU_SECRET, shared chest opening, item collection, " +
            "done flags and room exit in split/batched gameplay updates.");
    }

    private void ValidateFaroreUnlinkedSecrets()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            ApproachLibraryActor(farore, batched);
            Press(); Step();
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Farore", StringComparison.OrdinalIgnoreCase),
                "Completed unlinked Farore did not offer TX_5502.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_secretEntry.IsActive,
                "Completed unlinked Farore did not request askforsecret $ff.");
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(2);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "King Zora", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore valid Ages secret $30 did not show TX_550f hint.");
            _dialogue.Close(); Step(30);
            FailIf(!DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "I, too", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore did not show TX_5504 after wait 30.");
            _dialogue.Close(); Step();
            FailIf(!_dialogue.ChoiceActive || !DialogueBox.PlainText(_dialogue.CurrentMessage)
                .Contains("Holodrum", StringComparison.OrdinalIgnoreCase),
                "Unlinked Farore did not offer the game-transfer secret after hint.");
            _dialogue.SubmitChoiceForValidation(0);
            Step();
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage.Contains("\\secret", StringComparison.Ordinal) ||
                _saveData.ReadWramByte(WramAddress.wFileIsLinkedGame) != 0,
                "Unlinked Farore did not generate and substitute both transfer-secret halves.");
            _dialogue.Close();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                "Unlinked Farore transfer-secret room exit retained input control.");
        }
        GD.Print("Validated completed unlinked Farore $30 hint, 30-update wait, " +
            "TX_5504, Holodrum offer and generated transfer secret in split/batched gameplay.");
    }

    private void ValidateFaroreUpgradeVariants()
    {
        var codec = new LinkedGameNpcDatabase();
        foreach (int quest in new[] { 4, 5, 7, 9 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetGlobalFlag(0x50 + quest);
            if (quest == 7) _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            LoadValidationRoom(5, 0xcf);
            StepGameplayUpdates(3, Vector2.Zero);
            NpcCharacter farore = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == InteractionId.Farore);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero);
            ApproachLibraryActor(farore, false);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]); Step();
            _dialogue.Close(); Step();
            _dialogue.SubmitChoiceForValidation(0); Step();
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit(codec.GenerateSecretValues(0x30 + quest, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(240);
            FailIf(!_dialogue.IsOpen || _saveData.HasGlobalFlag(0x5a + quest),
                $"Farore upgrade quest ${quest:x2} skipped the held treasure text or set done early.");
            switch (quest)
            {
                case 4:
                    FailIf(_inventory.ShieldLevel != 2,
                        "Farore quest $04 did not grant TREASURE_OBJECT_SHIELD_01 parameter $02.");
                    break;
                case 5:
                    FailIf(_inventory.MaxBombs != 0x30 || _inventory.Bombs != 0x30,
                        "Farore quest $05 did not add packed-BCD $20 to both bomb bytes.");
                    break;
                case 7:
                    FailIf(_inventory.SeedSatchelLevel < 2,
                        "Farore quest $07 did not grant the Seed Satchel upgrade.");
                    break;
                case 9:
                    FailIf(_inventory.RingBoxLevel != 2,
                        "Farore quest $09 did not retain the source L-2 Ring Box grant.");
                    break;
            }
            _dialogue.Close(); Step(31);
            FailIf(!_saveData.HasGlobalFlag(0x5a + quest) ||
                _saveData.HasGlobalFlag(GlobalFlag.SecretChestWaiting) ||
                !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                    "power", StringComparison.OrdinalIgnoreCase),
                $"Farore upgrade quest ${quest:x2} did not finish with TX_5509 and done flag.");
            _dialogue.Close(); Step();
            LoadValidationRoom(5, 0xd0);
            FailIf(_player.CutsceneControlled,
                $"Farore upgrade quest ${quest:x2} retained input control after room exit.");
        }
        GD.Print("Validated Farore native upgrades $04/$05/$07/$09, held item text, " +
            "packed-BCD bomb capacity, satchel and ring-box levels, completion flags and exit.");
    }
}
