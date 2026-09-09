using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSymmetryDungeonEntrance()
    {
        // loadTilesetData.s:getAdjustedRoomGroup uses ROOMFLAG_LAYOUTSWAP,
        // not GLOBALFLAG_TUNI_NUT_PLACED. tuniNutMain.s sets the six village
        // room bits together; a debug state may independently alter one bit.
        _saveData.SetGlobalFlag(0x29, false);
        _saveData.SetRoomFlag(0, 0x03, 1, false);
        LoadValidationRoom(0, 0x03);
        byte[] roomFlagsBefore = new byte[0x400];
        _saveData.ReadWramBytes(0xc700, roomFlagsBefore);
        _inventory.GiveTreasure(TreasureDatabase.TreasureBoomerang, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        for (int seed = 0; seed < 5; seed++)
        {
            _inventory.SelectSatchelSeeds(seed);
            _inventory.SelectShooterSeeds(seed);
            FailIf(_saveData.ReadWramByte(0xc6c4) != seed ||
                _saveData.ReadWramByte(0xc6c5) != seed,
                "Selected seeds did not use clean US WRAM $c6c4/$c6c5.");
        }
        byte[] roomFlagsAfter = new byte[0x400];
        _saveData.ReadWramBytes(0xc700, roomFlagsAfter);
        FailIf(!roomFlagsBefore.SequenceEqual(roomFlagsAfter),
            "Inventory acquisition or seed selection corrupted room flags at $c700-$caff.");
        var warps = new WarpDatabase();
        void CheckEntrance(bool restored)
        {
            FailIf(_currentRoom.TilesetId != (restored ? 0x07 : 0x06),
                "Symmetry $0:$03 selected the wrong restoration tileset.");
            for (int x = 4; x <= 5; x++)
            {
                byte tile = _currentRoom.Layout[30 + x];
                FailIf(tile != (restored ? 0xee + x - 4 : 0xfe),
                    $"Symmetry $0:$03 entrance ${0x30 + x:x2} has tile ${tile:x2}.");
                bool warp = warps.TryGetTileWarp(0, 0x03, 0x30 + x, tile, out _);
                FailIf(warp != restored || (!restored &&
                    !_currentRoom.IsSolid(new Vector2(x * 16 + 8, 3 * 16 + 8))),
                    "Unrestored Symmetry dungeon entrance must be solid and reject warping.");
            }
        }
        CheckEntrance(false);
        StepRoomEventFrames(180);
        FailIf(_saveData.HasRoomFlag(0, 0x03, 1),
            "Symmetry $0:$03 room events unexpectedly restored the dungeon entrance.");
        CheckEntrance(false);

        _saveData.SetRoomFlag(0, 0x03, 1);
        LoadValidationRoom(0, 0x03);
        CheckEntrance(true);

        // Revisiting the cached ruined room after a debug flag correction
        // must restore its original lava tiles, collision and warp gate.
        _saveData.SetRoomFlag(0, 0x03, 1, false);
        LoadValidationRoom(0, 0x03);
        CheckEntrance(false);
        GD.Print("Validated Symmetry $0:$03 dungeon entrance restoration, collision, warp gating and cached reload.");
    }

    private void ValidateSymmetryFidelity()
    {
        var commands = _roomEvents.Get<SymmetryEvent>().Database.Commands;
        var jumps = commands.OfType<CutsceneBranchYieldCommand>().ToArray();
        FailIf(jumps.Length == 0 || jumps.Any(j => !j.Source.Label.StartsWith("symmetryNpcSubid", StringComparison.Ordinal)),
            "Symmetry helper scripts lost their wBigBuffer jump classification.");
        foreach (var jump in jumps)
        {
            var runner = new CutsceneCommandRunner(new ValidationSymmetryBranchHost());
            // Keep the source branch but use a destination that would fail if
            // dispatched in the same update by the default-deny host.
            var branchSource = jump.Source with { CommandIndex = 0 };
            var targetSource = jump.Source with { CommandIndex = 1, Opcode = "native" };
            runner.Start(new CutsceneCommand[] {
                new CutsceneBranchYieldCommand(branchSource, 1),
                new CutsceneNativeCommand(targetSource, "MustWaitForNextUpdate") });
            runner.AdvanceFrame();
            FailIf(runner.Instruction != 1 || !runner.Active,
                $"{jump.Source} did not yield at its relocated target.");
        }

        var quest = _roomEvents.Get<SymmetryEvent>();
        _saveData.SetGlobalFlag(quest.Database.Constant("placed-flag"), false);
        _saveData.SetGlobalFlag(quest.Database.Constant("finished-flag"), false);
        _inventory.GiveTreasure(TreasureDatabase.TreasureTuniNut, 2);
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        LoadValidationRoom(5, 0xf6);
        _player.WarpTo(new Vector2(0x78, 0x30), recordSafe: false);
        var nut = _entities.EntityAdapters<TuniNutRoomEntity>().Single();
        _player.AdvanceTopDownAirUpdateForValidation(startJump: true);
        FailIf(!_player.TopDownAirborne || !_player.AcceptsGroundInteractionContact,
            "Ground interaction rejected the low beginning of an ordinary Feather arc.");
        for (int i = 0; i < 20 && _player.TopDownAirZ >= -7; i++) _player.AdvanceTopDownAirUpdateForValidation();
        FailIf(_player.AcceptsGroundInteractionContact, "Ground interaction accepted Link above the source seven-unit Z range.");
        StepRoomEventFrames(1);
        FailIf(nut.State != 1, "Tuni Nut triggered while Link was above its collision height.");
        for (int i = 0; i < 90 && _player.TopDownAirZ < -7; i++) _player.AdvanceTopDownAirUpdateForValidation();
        FailIf(!_player.TopDownAirborne || !_player.AcceptsGroundInteractionContact,
            "Ground interaction did not accept Link's descending low Feather arc.");
        var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new Vector2(32, 96), Vector2I.Up,
            new SeedSatchelDatabase().Scent, 5));
        var beam = _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new Vector2(32, 96), 0));
        var bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, new BombDatabase().Data, 5, _ => { }));
        StepRoomEventFrames(1);
        FailIf(nut.State != 3 || nut.Counter != 60 || _player.TopDownAirborne ||
            _entities.Entities<EmberSeedEffect>().Contains(seed) || _entities.Entities<SwordBeamEffect>().Contains(beam) ||
            _entities.Entities<BombEffect>().Contains(bomb),
            "Tuni Nut did not clear physical Item slots and ground low airborne Link before starting its placement.");
        LoadValidationRoom(0, 0x22);
        FailIf(quest.BlocksGameplay || _player.CutsceneControlled, "Cancelling audited Tuni Nut placement retained input control.");
        GD.Print("Validated Symmetry copied-script jump yield, low airborne trigger height, physical item cleanup, and cancellation.");
    }

    private void ValidateSymmetrySecrets()
    {
        var quest = _roomEvents.Get<SymmetryEvent>();
        var commands = quest.Database.Commands;
        _saveData.SetGlobalFlag(quest.Database.Constant("finished-flag"));
        _saveData.SetGlobalFlag(quest.Database.Constant("placed-flag"));
        void Enter()
        {
            LoadValidationRoom(5, 0xf6);
            _player.WarpTo(new Vector2(0x58, 0x68), recordSafe: false);
            StepRoomEventFrames(8);
        }
        void AwaitText(int text)
        {
            for (int i = 0; i < 180 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
            string message = commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == text).Message;
            if (text == 0x2d2b) message = message.Replace("\\secret1", new LinkedGameNpcDatabase().GenerateSecret(0x19, _saveData), StringComparison.Ordinal);
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(message),
                $"Symmetry secret expected TX_{text:x4}, got '{_dialogue.CurrentMessage}'.");
        }
        void Talk(int text)
        {
            StepRoomEventFrames(8);
            var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xbf && n.Record.SubId == 8);
            FailIf(!quest.TryInteractNpc(npc), "Postgame symmetry sister $bf:$08 did not accept A.");
            AwaitText(text);
        }
        foreach (int level in new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            quest = _roomEvents.Get<SymmetryEvent>();
            _saveData.SetGlobalFlag(quest.Database.Constant("finished-flag"));
            _saveData.SetGlobalFlag(quest.Database.Constant("placed-flag"));
            _saveData.SetGlobalFlag(0x6d, false);
            _saveData.SetGlobalFlag(0x77, false);
            _saveData.SetRoomFlag(5, 0xf6, 0x20, false);
            _inventory.LoseTreasure(TreasureDatabase.TreasureRingBox);
            if (level != 0) _inventory.GiveTreasure(_treasures.GetObject($"TREASURE_OBJECT_RING_BOX_{level - 1:x2}"));
            Enter();
            Talk(0x2d24);
            _dialogue.SubmitChoiceForValidation(1);
            StepRoomEventFrames(30);
            FailIf(_dialogue.IsOpen || !quest.BlocksGameplay, "Symmetry secret refusal omitted its exact wait 30 boundary.");
            AwaitText(0x2d25);
            _dialogue.Close();
            StepRoomEventFrames(8);
            FailIf(quest.BlocksGameplay || _saveData.HasGlobalFlag(0x6d), "Symmetry refusal retained control or began the secret.");
            int opened = 0;
            quest.OpenSecretMenu = (index, complete) =>
            {
                FailIf(index != 0x09, "Symmetry sister requested a secret other than SYMMETRY_SECRET $09.");
                opened++;
                complete(false);
                return true;
            };
            Talk(0x2d24);
            _dialogue.SubmitChoiceForValidation(0);
            AwaitText(0x2d27);
            FailIf(opened != 1 || _saveData.HasGlobalFlag(0x6d) || _saveData.HasGlobalFlag(0x77),
                "Invalid symmetry secret advanced completion flags.");
            _dialogue.Close();
            quest.OpenSecretMenu = (index, complete) => { opened++; complete(true); return true; };
            Talk(0x2d24);
            _dialogue.SubmitChoiceForValidation(0);
            AwaitText(0x2d26);
            FailIf(!_saveData.HasGlobalFlag(0x6d) || _saveData.HasGlobalFlag(0x77),
                "Valid symmetry secret did not set BEGAN $6d before the reward.");
            _dialogue.Close();
            AwaitText(level == 0 ? 0x2d2a : 0x2d28);
            _dialogue.Close();
            if (level != 0) { AwaitText(0x2d29); _dialogue.Close(); }
            for (int i = 0; i < 180 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
            FailIf(!_dialogue.IsOpen || _inventory.RingBoxLevel != (level <= 1 ? 2 : 3),
                $"Symmetry secret gave ring-box level ${_inventory.RingBoxLevel:x2} for starting level ${level:x2}; dialogue='{_dialogue.CurrentMessage}'.");
            _dialogue.Close();
            AwaitText(0x2d2b);
            FailIf(!_saveData.HasGlobalFlag(0x77) || !_saveData.HasRoomFlag(5, 0xf6, 0x20) || opened != 2,
                "Symmetry reward did not persist DONE $77 and room item flag $20.");
            _dialogue.Close();
            Enter();
            Talk(0x2d2b);
            FailIf(opened != 2, "Completed symmetry secret reopened MENU_SECRET on re-entry.");
            _dialogue.Close();
        }
        _saveData.SetGlobalFlag(0x77, false);
        Enter();
        Action<bool>? pending = null;
        quest.OpenSecretMenu = (_, complete) => { pending = complete; return true; };
        Talk(0x2d24);
        _dialogue.SubmitChoiceForValidation(0);
        for (int i = 0; i < 60 && pending is null; i++) StepRoomEventFrames(1);
        FailIf(pending is null || !quest.BlocksGameplay, "Symmetry secret did not retain its input lease while MENU_SECRET owns the prompt.");
        LoadValidationRoom(0, 0x22);
        pending!(true);
        FailIf(quest.HasState || quest.BlocksGameplay || _player.CutsceneControlled,
            "Cancelling Symmetry during secret entry retained an actor or input lease.");
    }

    private void ValidateTuniNutPlacement()
    {
        var quest = _roomEvents.Get<SymmetryEvent>();
        int placedFlag = quest.Database.Constant("placed-flag");
        _saveData.SetGlobalFlag(placedFlag, false);
        _saveData.SetGlobalFlag(quest.Database.Constant("finished-flag"), false);
        _inventory.GiveTreasure(TreasureDatabase.TreasureTuniNut, 2);
        LoadValidationRoom(5, 0xf6);
        _player.WarpTo(new Vector2(0x7b, 0x30), recordSafe: false);
        var nut = _entities.EntityAdapters<TuniNutRoomEntity>().Single();
        FailIf(nut.State != 1 || nut.Visible, "Room $5:$f6 did not initialize the repaired Tuni Nut $b1:$00 as an invisible trigger.");
        StepRoomEventFrames(1);
        FailIf(nut.State != 2 || nut.Counter != 3 || !_player.CutsceneControlled,
            "Tuni Nut did not latch Link's three-pixel centering distance.");
        StepRoomEventFrames(2);
        FailIf(nut.State != 2 || _player.Position.X != 0x79, "Tuni Nut centering did not move Link one pixel per update.");
        StepRoomEventFrames(1);
        FailIf(nut.State != 3 || nut.Counter != 60 || _player.Position.X != 0x78 || !nut.Visible,
            "Tuni Nut centering did not begin the 60-update rise wait on its zero update.");
        StepRoomEventFrames(59);
        FailIf(nut.Substate != 0 || nut.Counter != 1 || nut.Height != 0, "Tuni Nut rose before its 60-update wait expired.");
        StepRoomEventFrames(1);
        FailIf(nut.Substate != 1 || nut.Counter != 16, "Tuni Nut did not initialize its 16 even-frame rise updates.");
        StepRoomEventFrames(32);
        FailIf(nut.Substate != 2 || nut.Height != -0x1000 || nut.Position.Y != 0x28,
            "Tuni Nut rise did not use the global frame parity or preserve logical Y.");
        StepRoomEventFrames(1);
        FailIf(nut.Position.Y != 0x27 || nut.ZIndex != NpcCharacter.InFrontOfLinkZIndex,
            "Tuni Nut did not render the high byte of its $40-speed movement at source priority $c0.");
        StepRoomEventFrames(63);
        FailIf(nut.Substate != 2 || nut.Position.Y != 0x18, "Tuni Nut north movement must retain Y=$18 until it crosses below the target.");
        StepRoomEventFrames(1);
        FailIf(nut.Substate != 3 || nut.Position.Y != 0x18, "Tuni Nut did not center on the pedestal after crossing Y=$18.");
        int z = -0x1000, speed = 0, falling = 0;
        do { z += speed; if (z < 0) speed += 0x20; falling++; } while (z < 0);
        StepRoomEventFrames(falling - 1);
        FailIf(nut.Substate != 3, "Tuni Nut landed early.");
        StepRoomEventFrames(1);
        FailIf(nut.Substate != 4 || nut.Counter != 90 || nut.Height != 0, "Tuni Nut landing did not start the 90-update solve wait.");
        StepRoomEventFrames(89);
        FailIf(nut.Substate != 4 || _saveData.HasGlobalFlag(placedFlag), "Tuni Nut restored Symmetry before the solve wait ended.");
        StepRoomEventFrames(1);
        FailIf(nut.Substate != 5 || _saveData.HasGlobalFlag(placedFlag), "Tuni Nut did not wait for brightening.");
        StepRoomEventFrames(9);
        FailIf(_saveData.HasGlobalFlag(placedFlag), "Tuni Nut completed before the palette thread's terminal update.");
        StepRoomEventFrames(1);
        FailIf(nut.State != 4 || !_saveData.HasGlobalFlag(placedFlag) || quest.BlocksGameplay || _player.CutsceneControlled ||
            _inventory.HasTreasure(TreasureDatabase.TreasureTuniNut) || _runtimeState.ReadWramByte(0xcfc0) != 1,
            "Tuni Nut completion did not restore input, consume $4c, and signal the sisters.");
        foreach (int room in new[] { 0x02, 0x03, 0x04, 0x12, 0x13, 0x14 })
            FailIf(!_saveData.HasRoomFlag(0, room, 1), $"Tuni Nut omitted present restoration room $0:${room:x2}.");
        FailIf(!nut.BlocksLink(new Vector2(0x78, 0x20)), "Placed Tuni Nut did not block Link.");
        LoadValidationRoom(5, 0xf6);
        nut = _entities.EntityAdapters<TuniNutRoomEntity>().Single();
        FailIf(nut.State != 4 || nut.Position != new Vector2(0x78, 0x18) || !nut.Visible,
            "Placed Tuni Nut did not reconstruct from the saved global flag on re-entry.");
    }

    private void ValidateSymmetryNutHandoff()
    {
        var quest = _roomEvents.Get<SymmetryEvent>();
        var data = quest.Database;
        foreach (int room in new[] { 0x6f, 0x6e })
        foreach (bool batched in new[] { false, true })
        {
            _inventory.LoseTreasure(TreasureDatabase.TreasureTuniNut);
            _saveData.SetGlobalFlag(data.Constant("sister-flag"));
            _saveData.SetGlobalFlag(data.Constant("brother-flag"));
            _saveData.SetGlobalFlag(data.Constant("placed-flag"), false);
            _saveData.SetRoomFlag(3, room, 0x40, false);
            LoadValidationRoom(3, room);
            _player.WarpTo(new Vector2(0x50, 0x68), recordSafe: false);
            var scheduler = new ApplicationFixedUpdateScheduler();
            void Step(int frames)
            {
                // GameRoot updates the room event before the shared reward owner.
                void Tick()
                {
                    StepRoomEventFrames(1);
                    _interactions.Update(1.0 / 60.0, _player);
                }
                if (batched) scheduler.Advance(frames / 60.0, Tick);
                else for (int i = 0; i < frames; i++) scheduler.Advance(1.0 / 60.0, Tick);
            }
            void AwaitText(int text)
            {
                for (int i = 0; i < 150 && !_dialogue.IsOpen; i++) Step(1);
                string expected = DialogueBox.PlainText(data.Commands.OfType<CutsceneShowTextCommand>()
                    .First(c => c.TextId == text).Message);
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != expected,
                    $"Symmetry $3:${room:x2} expected TX_{text:x4} during nut handoff.");
            }
            Step(8);
            var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xbf);
            FailIf(!quest.TryInteractNpc(npc), $"Symmetry $3:${room:x2} rejected the nut request.");
            AwaitText(0x2d02);
            _dialogue.Close();
            AwaitText(0x2d04);
            _dialogue.SubmitChoiceForValidation(0);
            AwaitText(0x2d05);
            _dialogue.Close();
            for (int i = 0; i < 150 && !_inventory.HasTreasure(TreasureDatabase.TreasureTuniNut); i++) Step(1);
            var reward = _interactions.GroundTreasureForValidation;
            FailIf(!_inventory.HasTreasure(TreasureDatabase.TreasureTuniNut) || _inventory.TuniNutState != 0 ||
                !_dialogue.IsOpen || reward is null || !reward.Held || !_player.IsHoldingItemTwoHands,
                $"Symmetry $3:${room:x2} did not present broken Tuni Nut $4c:$00.");
            Step(8);
            FailIf(reward!.Finished || !quest.BlocksGameplay,
                $"Symmetry $3:${room:x2} finished the nut handoff before its textbox closed.");
            _dialogue.Close();
            Step(1);
            FailIf(_interactions.DialogueOpen || _interactions.GroundTreasureForValidation is not null ||
                !reward.Finished || _player.IsHoldingItemTwoHands || _player.CutsceneControlled || quest.BlocksGameplay,
                $"Symmetry $3:${room:x2} retained dialogue/input ownership after closing the Tuni Nut $4c:$00 message.");
            Step(8);
            FailIf(_dialogue.IsOpen || !quest.TryInteractNpc(npc),
                $"Symmetry $3:${room:x2} did not return to waiting for another conversation.");
            AwaitText(0x2d08);
            _dialogue.Close();
            Step(8);
            LoadValidationRoom(0, 0x56);
            LoadValidationRoom(3, room);
            Step(8);
            npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xbf);
            FailIf(!quest.TryInteractNpc(npc), $"Returning to Symmetry $3:${room:x2} retained the reward lock.");
            AwaitText(0x2d08);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0,
                $"Symmetry $3:${room:x2} awarded a duplicate Tuni Nut $4c:$00 on re-entry.");
            _dialogue.Close();
            Step(8);
        }
        GD.Print("Validated both Symmetry brothers' Tuni Nut textbox completion, shared reward ownership, repeat dialogue and re-entry with split/batched updates.");
    }

    private void ValidateSymmetryNpcs()
    {
        var quest = _roomEvents.Get<SymmetryEvent>();
        var data = quest.Database;
        foreach (string flag in new[] { "placed-flag", "sister-flag", "brother-flag", "finished-flag" })
            _saveData.SetGlobalFlag(data.Constant(flag), false);
        _saveData.SetRoomFlag(3, 0x6e, 0x40, false);
        _saveData.SetRoomFlag(3, 0x6f, 0x40, false);
        _saveData.SetRoomFlag(5, 0xf6, 0x01, false);
        void Enter(int group, int room)
        {
            LoadValidationRoom(group, room);
            _player.WarpTo(new Vector2(0x50, 0x68), recordSafe: false);
            StepRoomEventFrames(8);
        }
        NpcCharacter Actor(int subid) => _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xbf && n.Record.SubId == subid);
        void AwaitText(int text)
        {
            for (int i = 0; i < 150 && !_dialogue.IsOpen; i++) StepRoomEventFrames(1);
            string expected = DialogueBox.PlainText(data.Commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == text).Message);
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != expected,
                $"Symmetry room ${_rooms.ActiveGroup:x}:${_rooms.CurrentRoom.Id:x2} expected TX_{text:x4}, got '{_dialogue.CurrentMessage}'.");
        }
        void Talk(int subid, int text)
        {
            StepRoomEventFrames(8);
            FailIf(!quest.TryInteractNpc(Actor(subid)), $"Symmetry NPC $bf:${subid:x2} did not accept A.");
            AwaitText(text);
        }
        Enter(3, 0x6e);
        Talk(6, 0x2d0b);
        _dialogue.Close();
        FailIf(_saveData.HasGlobalFlag(data.Constant("brother-flag")), "Symmetry brother claimed a role before speaking to a sister.");
        Enter(5, 0xf6);
        FailIf(!_entities.EntityAdapters<SymmetryRoomEntity>().Select(a => a.Npc.Record.SubId).SequenceEqual(new[] { 8, 9 }),
            "Room $5:$f6 lost the original sister slot order.");
        Talk(9, 0x2d10);
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2d13);
        _dialogue.Close();
        StepRoomEventFrames(8);
        FailIf(quest.BlocksGameplay || _saveData.HasGlobalFlag(data.Constant("sister-flag")), "Refusing sister $bf:$09 retained input or set the quest flag.");
        Talk(9, 0x2d10);
        _dialogue.SubmitChoiceForValidation(0);
        AwaitText(0x2d11);
        FailIf(!_saveData.HasGlobalFlag(data.Constant("sister-flag")) || !_saveData.HasRoomFlag(5, 0xf6, 1),
            "Sister $bf:$09 did not persist her identity before TX_2d11.");
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2d14);
        _dialogue.SubmitChoiceForValidation(0);
        AwaitText(0x2d12);
        _dialogue.Close();
        StepRoomEventFrames(8);
        FailIf(!quest.TryInteractNpc(Actor(8)), "Other sister did not accept initial A.");
        StepRoomEventFrames(8);
        Talk(8, 0x2d15);
        _dialogue.Close();
        Enter(5, 0xf6);
        // Script selection for returning sisters occurs only after the first A probe.
        FailIf(!quest.TryInteractNpc(Actor(9)), "Returning sister did not accept A.");
        StepRoomEventFrames(8);
        Talk(9, 0x2d12);
        _dialogue.Close();
        Enter(3, 0x6f);
        FailIf(!_saveData.HasRoomFlag(3, 0x6f, 0x40) || !_saveData.HasGlobalFlag(data.Constant("brother-flag")),
            "The first visited brother did not claim the no-nut role on entry.");
        Talk(7, 0x2d00);
        _dialogue.Close();
        Enter(3, 0x6e);
        Talk(6, 0x2d02);
        _dialogue.Close();
        AwaitText(0x2d04);
        _dialogue.SubmitChoiceForValidation(1);
        AwaitText(0x2d07);
        _dialogue.Close();
        Talk(6, 0x2d04);
        _dialogue.SubmitChoiceForValidation(0);
        AwaitText(0x2d05);
        _dialogue.Close();
        for (int i = 0; i < 150 && !_inventory.HasTreasure(TreasureDatabase.TreasureTuniNut); i++) StepRoomEventFrames(1);
        FailIf(!_inventory.HasTreasure(TreasureDatabase.TreasureTuniNut) || _inventory.TuniNutState != 0,
            "Brother $bf:$06 did not grant broken TREASURE_TUNI_NUT $4c:$00.");
        if (_dialogue.IsOpen) _dialogue.Close();
        StepRoomEventFrames(8);
        Talk(6, 0x2d08);
        _dialogue.Close();
        Enter(3, 0x6f);
        Talk(7, 0x2d01);
        _dialogue.Close();
        _saveData.SetGlobalFlag(data.Constant("placed-flag"));
        foreach (var (group, room, subid, text) in new[] {
            (3,0x6e,6,0x2d0a), (3,0x7e,10,0x2d22), (3,0x7f,11,0x2d22),
            (3,0x8e,4,0x2d23), (3,0xea,0,0x2d0c), (3,0xeb,2,0x2d19),
            (3,0xec,2,0x2d19), (1,0x03,12,0x2d2c), (5,0xf6,8,0x2d18) })
        {
            Enter(group, room);
            Talk(subid, text);
            _dialogue.Close();
        }
        _saveData.SetGlobalFlag(data.Constant("placed-flag"), false);
        Enter(1, 0x03);
        FailIf(Actor(12).Active, "Symmetry $bf:$0c did not delete before TUNI_NUT_PLACED.");
        Enter(5, 0xf6);
        FailIf(!quest.TryInteractNpc(Actor(8)), "Sister with broken nut did not accept initial A.");
        StepRoomEventFrames(8);
        Talk(8, 0x2d16);
        _dialogue.Close();
        _runtimeState.SetWramByte(0xcfc0, 1);
        StepRoomEventFrames(8);
        Talk(8, 0x2d18);
        _dialogue.Close();
        Talk(9, 0x2d18);
        _dialogue.Close();
    }
}
