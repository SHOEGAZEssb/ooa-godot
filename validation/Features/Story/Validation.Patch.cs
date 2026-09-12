using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePatchRestoration()
    {
        var patch = _roomEvents.Get<PatchEvent>();
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        // Drive the actual application scheduler with explicit original input
        // edges; Godot's host-frame justPressed bit persists throughout a
        // synchronous headless scenario, even after ActionRelease.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        string? held = null, edge = null;
        void Step(int count = 1)
        {
            input.CaptureForValidation(held is null ? [] : [held], edge is null ? [] : [edge], held == "move_up" ? Vector2.Up : Vector2.Zero);
            edge = null; scheduler.Advance(count / 60.0, update);
        }
        void PressA() { held = edge = "attack"; Step(); held = null; }
        void Close()
        {
            for (int i = 0; i < 180 && _dialogue.IsOpen; i++) { Step(30); PressA(); }
            FailIf(_dialogue.IsOpen, "Patch dialogue did not close through A input.");
        }
        void Until(Func<bool> condition, int limit, string description)
        {
            for (int i = 0; i < limit && !condition(); i++) Step();
            FailIf(!condition(), $"Patch timed out: {description}; state={patch.State}, command={patch.ScriptIndex}, text={_dialogue.CurrentMessage}.");
        }
        void Text(string content)
        {
            Until(() => _dialogue.IsOpen, 400, "dialogue " + content);
            FailIf(!_dialogue.CurrentMessage.Contains(content, StringComparison.Ordinal), $"Patch expected '{content}', got '{_dialogue.CurrentMessage}'.");
        }
        void Choice(int value) { _dialogue.SubmitChoiceForValidation(value); Step(2); }
        void CheckVictoryHoleEffects(bool batched)
        {
            var falls = _entities.Entities<FallingDownHoleEffect>().ToArray();
            var initialUpdates = falls.Select(fall => fall.ElapsedUpdates).ToArray();
            FailIf(falls.Length < 4 || falls.Any(fall => fall.Finished),
                "Patch victory did not retain the final beetles' live $0f hole interactions.");
            // interaction0f animation 0 has durations 8,12,12 followed by
            // parameter $80. It deletes on update 33 even while Patch sets
            // DISABLE_ALL_BUT_INTERACTIONS and starts the white palette fade.
            for (int elapsed = 0; elapsed < 35;)
            {
                int count = batched ? Math.Min(7, 35 - elapsed) : 1;
                Step(count); elapsed += count;
                for (int i = 0; i < falls.Length; i++)
                {
                    int expected = Math.Min(initialUpdates[i] + elapsed, 33);
                    int frame = expected < 8 ? 0 : expected < 20 ? 1 : expected < 32 ? 2 : 3;
                    FailIf(falls[i].ElapsedUpdates != expected || falls[i].AnimationFrame != frame ||
                        falls[i].Finished != (expected == 33),
                        $"Patch victory froze or changed $0f animation timing: expected update {expected}/frame {frame}, " +
                        $"got {falls[i].ElapsedUpdates}/{falls[i].AnimationFrame}, fade={patch.Fading}, batch={count}.");
                }
            }
            FailIf(!patch.Fading || patch.State != 3 || _entities.Entities<FallingDownHoleEffect>().Any(),
                "Patch's white victory fade retained completed $0f hole animations.");
        }
        NpcCharacter Actor() => _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x94 && n.Record.SubId < 2 && n.Active);
        void ApproachAndTalk()
        {
            var npc = Actor();
            _player.WarpTo(npc.Position + new Vector2(0, 48));
            FailIf(_currentRoom.IsSolid(_player.Position), "Patch test starts inside solid room geometry.");
            held = edge = "move_up"; Step(40); held = null;
            Step();
            FailIf(!npc.CanTalkTo(_player), $"Patch is unreachable through room collision from {_player.Position}; control={_player.CutsceneControlled}, text={_dialogue.IsOpen}/{_dialogue.CurrentMessage}, state={patch.State}, script={patch.ScriptIndex}, transitioning={IsTransitioning}.");
            PressA(); Step(2);
        }
        _saveData.SetGlobalFlag(0x1f, false);
        _saveData.SetRoomFlag(1, 0xbe, 6, false);
        _inventory.LoseTreasure(TreasureDatabase.TreasureTradeItem);
        _inventory.LoseTreasure(TreasureDatabase.TreasureTuniNut);
        LoadValidationRoom(1, 0x23); // $90:$10, not room-load blanket clearing.
        LoadValidationRoom(3, 0xbe);
        ApproachAndTalk(); Text("I haven't had");
        FailIf(!_dialogue.CurrentMessage.Contains("need me?", StringComparison.Ordinal), "TX_5800 lost physical fallthrough into TX_5801.");
        FailIf((_saveData.GetRoomFlags(1, 0xbe) & 6) != 6, "Patch first meeting did not write past-overworld room $be mask $06.");
        Choice(0); Text("You don't seem"); Close(); Step(3);
        ApproachAndTalk(); Text("I am Patch"); Choice(1); Text("You must have"); Close(); Step(3);

        _inventory.GiveTreasure(TreasureDatabase.TreasureTuniNut, 0);
        LoadValidationRoom(3, 0xbe); // var38 is sampled at state 0.
        ApproachAndTalk(); Text("I am Patch"); Choice(0); Text("Heh, heh!");
        FailIf(!_dialogue.CurrentMessage.Contains("Tuni Nut", StringComparison.Ordinal), "Patch TX_5804 lost dynamic item-name substitution.");
        Choice(1); Text("Then that"); Close(); Step(3);
        ApproachAndTalk(); Text("I am Patch"); Choice(0); Text("Heh, heh!"); Choice(0);
        Text("You are willing"); Close();
        Until(() => !patch.HasState, 250, "upstairs staircase departure");
        FailIf(_player.CutsceneControlled || _entities.RuntimeState.ReadWramByte(0xcfd2) != 1, "Patch upstairs departure retained input or lost patchDownstairs.");
        LoadValidationRoom(3, 0xbe);
        FailIf(_entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x94 && n.Active), "Patch reappeared upstairs while downstairs.");
        LoadValidationRoom(5, 0xe8);
        ApproachAndTalk(); Text("Welcome to"); Choice(0); Text("Very well!"); Choice(0); Text("Let the ceremony"); Close();
        Until(() => patch.State == 2, 30, "game start");
        FailIf(_inventory.TuniNutState != 1 || _currentRoom.GetMetatile(new Vector2(0x98, 0x48)) != 0xa0,
            "Patch game start did not put Tuni Nut in state $01 and replace stair tile $49.");
        Until(() => _entities.Entities<HardhatBeetleCharacter>().Count() == 4, 65, "four beetle spawns");
        var brokenNut = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x94 && n.Record.SubId == 4);
        FailIf(brokenNut.ZIndex != NpcCharacter.FixedLowPriorityZIndex || brokenNut.ZIndex >= Actor().ZIndex,
            "Patch's broken nut $94:$04 lost visible83 priority and draws over Patch.");
        FailIf(_entities.Entities<HardhatBeetleCharacter>().Any(b => b.Record.Id != 0x5f || b.Record.Palette != 6 || b.Record.DamageQuarters != 2),
            "Patch did not spawn harmless ENEMY_HARMLESS_HARDHAT_BEETLE $5f.");
        var contactBeetle = _entities.Entities<HardhatBeetleCharacter>().First();
        _player.WarpTo(contactBeetle.Position + new Vector2(-5, 0));
        int health = _player.HealthQuarters;
        Step();
        FailIf(_player.HealthQuarters != health || _player.InvincibilityFrames != -15 || _player.KnockbackFrames != 19,
            "Harmless beetle collision $42 did not apply LINKDMG_$14: no damage, -15 invincibility, 19 knockback.");
        _player.WarpTo(new Vector2(0x78, 0x48));
        // Leave the switch unpressed. The real moving cart must hit the nut.
        Until(() => patch.State == 5, 1600, "cart collision failure");
        FailIf(!_player.CutsceneControlled, "US Patch failure did not disable Link.");
        var explosion = _entities.Entities<InteractionExplosionEffect>().Single();
        // INTERAC_EXPLOSION $56 uses fixed common OBJ graphics at $8001,
        // tile base $0c, palette 2, and interaction56 animation 0. This clean-US
        // frame fingerprint also rejects the unrelated graphics-ID-zero logo.
        FailIf(explosion.Position != new Vector2(0x70, 0x18) || explosion.ZOffset != 0 ||
            explosion.TextureSize != new Vector2(32, 32) || explosion.RenderedTextureOrigin != new Vector2(0x60, 8) ||
            explosion.TexturePixelHash != 0x510f3c7716debcb4UL || explosion.AnimationFrame != 0 ||
            explosion.ZIndex != NpcCharacter.InFrontOfLinkZIndex,
            $"Patch cart-hit explosion $56 has incorrect source graphics, origin or priority (pixels={explosion.TexturePixelHash:x16}).");
        Step(124);
        FailIf(!patch.Fading || patch.State != 5 || _inventory.TuniNutState != 1,
            "Patch delay-$04 white fade completed before original update 125.");
        Step();
        FailIf(patch.Fading || patch.State != 6 || _inventory.TuniNutState != 0,
            "Patch failure did not finish its white fade on update 125.");
        Text("The ceremony");
        FailIf(_inventory.TuniNutState != 0 || _entities.Entities<HardhatBeetleCharacter>().Any(), "Patch failure did not restore the broken nut and delete beetles.");
        Close(); Until(() => patch.State == 1, 30, "retry initialization");
        FailIf(_player.CutsceneControlled || _currentRoom.GetMetatile(new Vector2(0x98, 0x48)) != 0x44,
            "Patch retry did not restore input and stairs.");
        ApproachAndTalk(); Text("Welcome to"); Choice(1); Text("Then that"); Close();
        Step(3);
        ApproachAndTalk(); Text("Welcome to"); Choice(0); Text("Very well!"); Choice(1);
        Text("Then let me");
        FailIf(!_dialogue.CurrentMessage.Contains("any hole", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains("Tuni Nut", StringComparison.Ordinal), "Patch rules lost content or dynamic substitutions.");
        Choice(0); Text("Let the ceremony"); Close(); Until(() => patch.State == 2, 30, "retry start");
        FailIf(patch.BeetlesRemaining != 60, "Patch manager did not retain its initial 60-update delay.");
        Step(59);
        FailIf(_entities.Entities<HardhatBeetleCharacter>().Any() || patch.BeetlesRemaining != 1, "Patch beetles spawned before delay update 60.");
        Step();
        var beetles = _entities.Entities<HardhatBeetleCharacter>().ToArray();
        FailIf(beetles.Length != 4 || !beetles.Select(b => b.Position).SequenceEqual(new Vector2[] {
            new(0x48,0x48), new(0xa8,0x48), new(0x58,0x78), new(0x88,0x78) }), "Patch beetle order/positions differ from $44,$4a,$75,$78.");
        _player.WarpTo(new Vector2(0x58, 0x48));
        Step();
        FailIf((_entities.ActiveTriggers & 1) == 0, "Patch's actual PART_BUTTON $09:$80 did not activate trigger bit 0.");
        // Put each spawned enemy on a source hole and let the real hazard,
        // fall-effect, and interaction passes deliver the four-slot buffer.
        // This isolates victory sequencing from the separately tested sword recoil.
        var hole = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles).Select(i => new Vector2((i % _currentRoom.WidthInTiles) * 16 + 8, (i / _currentRoom.WidthInTiles) * 16 + 8))
            .First(p => _currentRoom.GetTerrainInfo(p).Hazard == HazardType.Hole);
        foreach (var beetle in beetles) beetle.Position = hole;
        Until(() => patch.State == 3, 80, "four source hole events winning the game");
        FailIf(_inventory.TuniNutState != 1 || !_player.CutsceneControlled, "Patch granted the restored nut before its reward script.");
        CheckVictoryHoleEffects(batched: false);
        Text("Hmm..."); Close(); Text("Here you go."); Close();
        Until(() => _inventory.TuniNutState == 2, 30, "restored Tuni Nut reward");
        Close(); Text("Bring me any-"); Close(); Until(() => patch.State == 4, 30, "completed NPC state");
        FailIf(_player.CutsceneControlled || _saveData.HasGlobalFlag(0x1f) || _entities.RuntimeState.ReadWramByte(0xcfd3) != 1,
            "Tuni Nut completion retained control, set the sword-only completion flag, or lost wonMinigame.");
        ApproachAndTalk(); Text("Bring me any-"); Close();
        LoadValidationRoom(5, 0xe8); Step(3); ApproachAndTalk(); Text("Bring me any-"); Close();
        LoadValidationRoom(1, 0x23);
        FailIf(Enumerable.Range(0xcfd0, 8).Any(a => _entities.RuntimeState.ReadWramByte(a) != 0), "Talus Peaks $90:$10 did not clear all eight Patch bytes.");
        LoadValidationRoom(3, 0xbe); ApproachAndTalk(); Text("I am Patch"); Choice(1); Close();

        // Both sword levels use the same native eight-beetle manager. Compare
        // its initial delay under individual updates and one host-frame batch.
        foreach (int swordLevel in new[] { 1, 2 })
        {
            _saveData.SetGlobalFlag(0x1f, false);
            _inventory.GiveTreasure(TreasureDatabase.TreasureSword, swordLevel);
            _inventory.GiveTreasure(TreasureDatabase.TreasureTradeItem, 0x0b);
            _inventory.GiveTreasure(TreasureDatabase.TreasureTuniNut, 0);
            LoadValidationRoom(1, 0x23); LoadValidationRoom(3, 0xbe);
            ApproachAndTalk(); Text("I haven't seen");
            FailIf(_entities.RuntimeState.ReadWramByte(0xcfd0) != 1 ||
                _entities.RuntimeState.ReadWramByte(0xcfd1) != (swordLevel & 1),
                "Patch failed broken-sword priority over the broken nut or sword parity selection.");
            Choice(0); Text("Heh, heh!");
            FailIf(!_dialogue.CurrentMessage.Contains("Broken Sword", StringComparison.Ordinal), "Patch sword substitution is incorrect.");
            Choice(0); Text("You are willing"); Close(); Until(() => !patch.HasState, 250, "sword departure");
            LoadValidationRoom(5, 0xe8); ApproachAndTalk(); Text("Welcome to"); Choice(0);
            Text("Very well!"); Choice(0); Text("Let the ceremony"); Close();
            Until(() => patch.State == 2, 30, "sword game start");
            FailIf(_inventory.TradeItem != 0x0c || _inventory.TuniNutState != 0, "Sword game mutated the nut or failed to mark the sword in progress.");
            if (swordLevel == 1) for (int i = 0; i < 59; i++) Step(); else Step(59);
            FailIf(patch.BeetlesRemaining != 1 || _entities.Entities<HardhatBeetleCharacter>().Any(), "Patch sword delay depends on host-frame batching.");
            Step();
            FailIf(patch.BeetlesRemaining != 8, "Patch sword game does not require eight beetles.");
            _player.WarpTo(new Vector2(0x58, 0x48));
            var firstWave = _entities.Entities<HardhatBeetleCharacter>().ToArray();
            foreach (var beetle in firstWave) beetle.Position = hole;
            Until(() => patch.BeetlesRemaining == 4, 80, "first sword wave hole buffer");
            var secondWave = _entities.Entities<HardhatBeetleCharacter>().Where(b => !firstWave.Contains(b)).ToArray();
            FailIf(secondWave.Length != 1 || secondWave[0].Position != new Vector2(0xa8, 0x48), "Patch did not allocate exactly one extra beetle at $4a on the buffer-reading update.");
            foreach (var expected in new[] { new Vector2(0x78, 0x58), new Vector2(0x58, 0x78), new Vector2(0x88, 0x78) })
            {
                Step();
                var next = _entities.Entities<HardhatBeetleCharacter>().Where(b => !firstWave.Contains(b) && !secondWave.Contains(b)).ToArray();
                FailIf(next.Length != 1 || next[0].Position != expected, "Patch extra beetle order/cadence differs from $4a,$57,$75,$78.");
                secondWave = secondWave.Concat(next).ToArray();
            }
            foreach (var beetle in secondWave) beetle.Position = hole;
            Until(() => patch.State == 3, 80, "eight beetles winning sword game");
            CheckVictoryHoleEffects(batched: swordLevel == 2);
            Text("Hmm..."); Close(); Text("Here you go."); Close();
            Until(() => _inventory.SwordLevel == swordLevel + 1, 30, "restored sword reward");
            FailIf(!_player.IsHoldingItemOneHand, "Patch's first sword reward lost collect mode $01.");
            Close();
            Until(() => _entities.Entities<GroundTreasurePickup>().Any(t => t.Record.GrabMode == 3), 10, "silent sword collect mode $03");
            var swordPickup = _entities.Entities<GroundTreasurePickup>().Single(t => t.Record.GrabMode == 3);
            Until(() => _player.IsAttacking, 7, "forced reward sword spin");
            FailIf(swordPickup.Visible, "Sword collect mode $03 became visible during its forced spin.");
            Until(() => swordPickup.Held, 45, "forced spin completion");
            FailIf(!_player.IsHoldingItemOneHand || swordPickup.Position != _player.Position + new Vector2(-4, -14),
                "Sword collect mode $03 lost the raised sword position/pose after spinning.");
            Text("Bring me any-"); Close(); Until(() => patch.State == 4, 30, "sword completion");
            Step();
            FailIf(_player.IsHoldingItemOneHand || _player.IsAttacking, "Patch's completed sword ceremony retained the forced pose.");
            FailIf(!_saveData.HasGlobalFlag(0x1f) || _inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) ||
                _inventory.TuniNutState != 0 || _player.CutsceneControlled, "Patch sword reward lost completion, trade removal, or inventory/control boundaries.");
            LoadValidationRoom(1, 0x23); LoadValidationRoom(3, 0xbe);
            ApproachAndTalk(); Text("There is nothing"); Close(); Step(3);
            ApproachAndTalk(); Text("There is nothing"); Close();
        }

        // Cancellation is a host room-event operation. It must release owned
        // visuals/control without inventing a persistent minigame reset.
        _saveData.SetGlobalFlag(0x1f, false);
        _inventory.GiveTreasure(TreasureDatabase.TreasureTuniNut, 0);
        LoadValidationRoom(1, 0x23); LoadValidationRoom(3, 0xbe);
        ApproachAndTalk(); Text("I am Patch"); Choice(0); Text("Heh, heh!"); Choice(0);
        Text("You are willing"); Close(); Until(() => !patch.HasState, 250, "cancellation setup departure");
        LoadValidationRoom(5, 0xe8); ApproachAndTalk(); Text("Welcome to"); Choice(0);
        Text("Very well!"); Choice(0); Text("Let the ceremony"); Close(); Until(() => patch.State == 2, 30, "cancellation setup game");
        Step(60); patch.Cancel(); Step();
        FailIf(_player.CutsceneControlled || _entities.Entities<HardhatBeetleCharacter>().Any(b => !b.IsDead) ||
            _currentRoom.GetMetatile(new Vector2(0x98, 0x48)) != 0x44 || _inventory.TuniNutState != 1,
            "Patch cancellation stranded owned objects/stairs or invented an inventory recovery before upstairs state 0.");
        LoadValidationRoom(3, 0xbe);
        FailIf(_inventory.TuniNutState != 0 || _entities.Entities<NpcCharacter>().Any(n => n.Record.Id == 0x94 && n.Active),
            "Patch upstairs did not recover the in-progress nut before its downstairs suppression gate.");
        _roomEvents.CommandTraceSink = null;
        GD.Print("Validated Patch choices/fallthrough, live cart failure/retry and fade boundary, Tuni Nut and both sword rewards, ordered extra beetles, batched updates, repeat dialogue and exterior reset.");
    }
}
