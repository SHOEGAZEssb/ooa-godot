using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom07cPoe()
    {
        const int group = 0;
        const int room = 0x7c;
        const int tradeItemAddress = 0xc6c0;
        const int tradeObtainedAddress = 0xc69a +
            (TreasureDatabase.TreasureTradeItem >> 3);
        const int tradeObtainedMask =
            1 << (TreasureDatabase.TreasureTradeItem & 7);

        PoeEvent poeEvent = _roomEvents.Poe;
        PoeEventDatabase database = poeEvent.Database;
        PoeEventRecord record = database.Record;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        byte originalTombFlags =
            _saveData.GetRoomFlags(record.TombGroup, record.TombRoom);
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? frameCounterField = typeof(RoomEntityManager).GetField(
            "_enemyFrameCounter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? frameAccumulatorField = typeof(RoomEntityManager).GetField(
            "_enemyFrameAccumulator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rng1Field = typeof(OracleRandom).GetField(
            "_rng1",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rng2Field = typeof(OracleRandom).GetField(
            "_rng2",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? randomCallsField = typeof(OracleRandom).GetField(
            "<Calls>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? lastRandomField = typeof(OracleRandom).GetField(
            "<LastResult>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (reloadInventory is null ||
            frameCounterField?.GetValue(_entities) is not int originalFrameCounter ||
            frameAccumulatorField?.GetValue(_entities) is not double originalFrameAccumulator ||
            rng1Field?.GetValue(_random) is not byte originalRng1 ||
            rng2Field?.GetValue(_random) is not byte originalRng2 ||
            randomCallsField?.GetValue(_random) is not int originalRandomCalls ||
            lastRandomField?.GetValue(_random) is not OracleRandomResult originalLastRandom)
        {
            throw new InvalidOperationException(
                "Could not snapshot Poe validation inventory/frame/RNG state.");
        }

        void SetTradeItemObtained(bool obtained)
        {
            byte flags = _saveData.ReadWramByte(tradeObtainedAddress);
            flags = obtained
                ? (byte)(flags | tradeObtainedMask)
                : (byte)(flags & ~tradeObtainedMask);
            _saveData.WriteWramByte(tradeItemAddress, 0);
            _saveData.WriteWramByte(tradeObtainedAddress, flags);
            _saveData.CommitInventoryChange();
            reloadInventory.Invoke(_inventory, null);
        }

        void SetPoeFlags(bool progress, bool tomb, bool item)
        {
            _saveData.SetRoomFlag(
                group, room, (byte)record.ProgressFlag, progress);
            _saveData.SetRoomFlag(
                group, room, (byte)record.ItemFlag, item);
            _saveData.SetRoomFlag(
                record.TombGroup,
                record.TombRoom,
                (byte)record.ProgressFlag,
                tomb);
        }

        PoeCharacter[] Actors() =>
            _entities.Entities<PoeCharacter>().ToArray();

        PoeCharacter ActiveActor() =>
            Actors().Single(actor => actor.Active);

        CutsceneShowTextCommand Text(int textId) =>
            database.Commands.OfType<CutsceneShowTextCommand>().Single(
                text => text.TextId == textId);

        void ExpectDialogue(int textId, string phase)
        {
            CutsceneShowTextCommand text = Text(textId);
            if (!_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(text.Message))
            {
                throw new InvalidOperationException(
                    $"Room 0:7c {phase} did not show TX_{textId:x4}.");
            }
        }

        void PositionForTalk()
        {
            _player.WarpTo(new Vector2(0x68, 0x44));
            _player.Face(Vector2I.Up);
        }

        void BeginTalk(int textId, int expectedVariant)
        {
            PositionForTalk();
            PoeCharacter actor = ActiveActor();
            if (actor.Record.Var03 != expectedVariant ||
                _entities.FindTalkTarget(_player) != actor ||
                !_interactions.TryInteract(_player))
            {
                throw new InvalidOperationException(
                    $"Room 0:7c Poe variant ${expectedVariant:x2} was not " +
                    "reachable through the native A-button path.");
            }
            StepRoomEventFrames(1);
            ExpectDialogue(textId, $"variant ${expectedVariant:x2} dialogue");
            if (!poeEvent.BlocksGameplay || !poeEvent.InputDisabled ||
                !_player.CutsceneControlled)
            {
                throw new InvalidOperationException(
                    "poeScript did not disable input before opening dialogue.");
            }
        }

        // Pin every combination of the two source $40 predicates and the
        // current room's item bit. State 0 deletes all actors for combinations
        // other than first=(0,0,*) and final=(1,1,0).
        for (int bits = 0; bits < 8; bits++)
        {
            bool progress = (bits & 1) != 0;
            bool tomb = (bits & 2) != 0;
            bool item = (bits & 4) != 0;
            int expectedVariant = !progress && !tomb
                ? record.FirstVariant
                : progress && tomb && !item
                    ? record.FinalVariant
                    : -1;
            SetPoeFlags(progress, tomb, item);
            LoadValidationRoom(group, room);
            PoeCharacter[] actors = Actors();
            PoeCharacter[] active = actors.Where(actor => actor.Active).ToArray();
            if (actors.Length != 2 ||
                actors[0].Record.Var03 != record.FirstVariant ||
                actors[1].Record.Var03 != record.FinalVariant ||
                active.Length != (expectedVariant < 0 ? 0 : 1) ||
                (expectedVariant >= 0 &&
                 active[0].Record.Var03 != expectedVariant) ||
                poeEvent.HasState != (expectedVariant >= 0))
            {
                throw new InvalidOperationException(
                    $"Room 0:7c Poe state-0 truth table diverged for " +
                    $"progress={progress}, tomb={tomb}, item={item}.");
            }
        }

        SetTradeItemObtained(obtained: false);
        SetPoeFlags(progress: false, tomb: false, item: false);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(group, room);

        PoeCharacter first = ActiveActor();
        if (first.Record is not
            {
                Id: 0x59,
                SubId: 0,
                Var03: 0,
                X: 0x68,
                Y: 0x38,
                SpriteName: "spr_friendlyghost",
                DefaultAnimation: 2,
                CanFace: true
            } ||
            first.Position != new Vector2(0x68, 0x38) ||
            first.Record.UpAnimation != record.Animation0 ||
            first.Record.RightAnimation != record.Animation1 ||
            first.Record.DownAnimation != record.Animation2 ||
            first.Record.LeftAnimation != record.Animation3 ||
            first.CurrentAnimationOpaquePixels == 0 ||
            first.AnimationRate != 0.0f ||
            !poeEvent.HasState || poeEvent.BlocksGameplay ||
            !poeEvent.ButtonSensitive || poeEvent.CurrentCommandIndex != 1)
        {
            throw new InvalidOperationException(
                "Room 0:7c did not preserve INTERAC_POE's two ordered records, " +
                "placement, directional OAM, or one-update script initialization.");
        }

        // Strict script collision is six pixels around Link's ten-pixel probe.
        _player.WarpTo(new Vector2(0x68, 0x48));
        _player.Face(Vector2I.Up);
        if (_entities.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "Poe talk targeting ignored the strict six-pixel Y boundary.");
        }
        _player.WarpTo(new Vector2(0x68, 0x47));
        if (_entities.FindTalkTarget(_player) != first)
        {
            throw new InvalidOperationException(
                "Poe talk targeting rejected the final Y point inside radius six.");
        }

        BeginTalk(0x0b00, record.FirstVariant);
        _dialogue.Close();
        StepRoomEventFrames(1);
        if (!_saveData.HasRoomFlag(
                group, room, (byte)record.ProgressFlag) ||
            !first.Active || first.Disappearing ||
            Actors().Single(actor =>
                actor.Record.Var03 == record.FinalVariant).Active ||
            poeEvent.CurrentCommandIndex != 6)
        {
            throw new InvalidOperationException(
                "First Poe dialogue did not set room bit $40 while retaining " +
                "the already-initialized actor and suppressing a live variant swap.");
        }

        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(record.DisappearWait);
        if (_sound.PlayRequestsFor(record.PoofSound) != 0 ||
            poeEvent.Counter != 1 || first.Disappearing)
        {
            throw new InvalidOperationException(
                "poeScript's first wait 40 reached SND_POOF one update early.");
        }
        StepRoomEventFrames(1);
        if (_sound.PlayRequestsFor(record.PoofSound) != 1 ||
            first.Disappearing || poeEvent.CurrentCommandIndex != 8)
        {
            throw new InvalidOperationException(
                "poeScript did not play SND_POOF on the wait-40 boundary.");
        }
        StepRoomEventFrames(1);
        if (!first.Disappearing || poeEvent.Counter != record.FlickerCount ||
            poeEvent.CurrentCommandIndex != 9 ||
            _entities.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "Poe disappearance did not install var3e=30 or retire collision/talk.");
        }
        StepRoomEventFrames(record.FlickerCount - 1);
        if (!poeEvent.HasState || !first.Active || poeEvent.Counter != 1 ||
            first.Visible != ((_entities.FrameCounter & record.FlickerMask) != 0))
        {
            throw new InvalidOperationException(
                "Poe flicker ended before the 30th decrement or ignored wFrameCounter.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.HasState || first.Active || first.Visible ||
            poeEvent.BlocksGameplay || _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "Poe disappearance did not end script/input exactly at var3e=$00.");
        }

        // With only the overworld $40 bit set, neither source state-0 branch
        // survives on re-entry.
        LoadValidationRoom(group, room);
        if (Actors().Any(actor => actor.Active) || poeEvent.HasState)
        {
            throw new InvalidOperationException(
                "Room 0:7c retained a Poe before the tomb $40 prerequisite.");
        }

        SetPoeFlags(progress: true, tomb: true, item: false);
        LoadValidationRoom(group, room);
        PoeCharacter final = ActiveActor();
        if (final.Record.Var03 != record.FinalVariant ||
            !poeEvent.ButtonSensitive || poeEvent.CurrentCommandIndex != 1)
        {
            throw new InvalidOperationException(
                "Both $40 prerequisites did not select final Poe variant $02.");
        }

        BeginTalk(0x0b02, record.FinalVariant);
        _sound.ClearPlayRequestAudit();
        _dialogue.Close();
        StepRoomEventFrames(30);
        if (_entities.Entities<GroundTreasurePickup>().Count != 0 ||
            poeEvent.Counter != 1 ||
            _saveData.HasRoomFlag(group, room, (byte)record.ItemFlag))
        {
            throw new InvalidOperationException(
                "Final Poe's wait 30 granted the Poe Clock one update early.");
        }
        StepRoomEventFrames(1);
        GroundTreasurePickup reward =
            _entities.Entities<GroundTreasurePickup>().Single();
        TreasureObjectRecord rewardObject =
            _treasures.GetObject(record.RewardObject);
        if (reward.Record.TreasureObject != record.RewardObject ||
            reward.Record.SpawnMode != 0 || reward.Record.GrabMode != 2 ||
            !reward.Held || !_player.IsHoldingItemTwoHands ||
            reward.Position != _player.Position + new Vector2(0, -14) ||
            !_inventory.HasTreasure(record.RewardTreasure) ||
            _inventory.TradeItem != record.RewardParameter ||
            !_saveData.HasRoomFlag(group, room, (byte)record.ItemFlag) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(rewardObject.Message) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2)
        {
            throw new InvalidOperationException(
                "poeScript giveitem did not grant the Poe Clock through grab " +
                "mode $02 with text, sounds, inventory, and room bit $20.");
        }

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (_player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0)
        {
            throw new InvalidOperationException(
                "Closing Poe Clock text did not release Link and delete the reward.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 6 || final.Disappearing)
        {
            throw new InvalidOperationException(
                "Final Poe did not branch to the shared wait-40 disappearance.");
        }
        StepRoomEventFrames(record.DisappearWait + 2);
        if (!final.Disappearing || poeEvent.CurrentCommandIndex != 9)
        {
            throw new InvalidOperationException(
                "Final Poe did not enter the shared var3e flicker sequence.");
        }
        StepRoomEventFrames(record.FlickerCount);
        if (poeEvent.HasState || final.Active ||
            poeEvent.BlocksGameplay || _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "Final Poe did not complete its shared disappearance cleanly.");
        }

        LoadValidationRoom(group, room);
        if (Actors().Any(actor => actor.Active) || poeEvent.HasState)
        {
            throw new InvalidOperationException(
                "Room bit $20 did not suppress final Poe on re-entry.");
        }

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "poeScript").ToArray();
        string[] requiredOpcodes =
        [
            "initcollisions", "checkabutton", "jumptablememory",
            "orroomflag", "playsound", "writeobjectbyte", "flicker", "giveitem"
        ];
        if (commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)))
        {
            throw new InvalidOperationException(
                "Poe typed trace lost source lines or a required script opcode.");
        }

        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        reloadInventory.Invoke(_inventory, null);
        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                group, room, flag, (originalRoomFlags & flag) != 0);
            _saveData.SetRoomFlag(
                record.TombGroup,
                record.TombRoom,
                flag,
                (originalTombFlags & flag) != 0);
        }
        _roomEvents.CommandTraceSink = null;
        // This scenario intentionally advances wFrameCounter to validate the
        // source flicker mask and reloads the room for every state-0 predicate,
        // regenerating the shared placement buffer each time. Restore the
        // harness phase and RNG so later independent enemy trajectories do not
        // inherit this test's duration or room-parse calls.
        frameCounterField.SetValue(_entities, originalFrameCounter);
        frameAccumulatorField.SetValue(_entities, originalFrameAccumulator);
        rng1Field.SetValue(_random, originalRng1);
        rng2Field.SetValue(_random, originalRng2);
        randomCallsField.SetValue(_random, originalRandomCalls);
        lastRandomField.SetValue(_random, originalLastRandom);

        GD.Print("Validated room 0:7c Poe $59:$00 variants: source state-0 " +
            "truth table, one-update typed script initialization, strict " +
            "$06 talk geometry, exact TX_0b00/TX_0b02 and 40/30-update waits, " +
            "fixed selected-actor lifetime, 30-update frame-mask flicker, " +
            "two-hand Poe Clock reward, and room-bit persistence.");
    }

    private void ValidateRoom22ePoe()
    {
        PoeEvent poeEvent = _roomEvents.Poe;
        PoeEventDatabase database = poeEvent.Database;
        PoeEventRecord record = database.Record;
        int group = record.TombGroup;
        int room = record.TombRoom;
        byte originalRoomFlags =
            _saveData.GetRoomFlags(record.Group, record.Room);
        byte originalTombFlags = _saveData.GetRoomFlags(group, room);
        FieldInfo? frameCounterField = typeof(RoomEntityManager).GetField(
            "_enemyFrameCounter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? frameAccumulatorField = typeof(RoomEntityManager).GetField(
            "_enemyFrameAccumulator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rng1Field = typeof(OracleRandom).GetField(
            "_rng1",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? rng2Field = typeof(OracleRandom).GetField(
            "_rng2",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? randomCallsField = typeof(OracleRandom).GetField(
            "<Calls>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? lastRandomField = typeof(OracleRandom).GetField(
            "<LastResult>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (frameCounterField?.GetValue(_entities) is not int originalFrameCounter ||
            frameAccumulatorField?.GetValue(_entities) is not double originalFrameAccumulator ||
            rng1Field?.GetValue(_random) is not byte originalRng1 ||
            rng2Field?.GetValue(_random) is not byte originalRng2 ||
            randomCallsField?.GetValue(_random) is not int originalRandomCalls ||
            lastRandomField?.GetValue(_random) is not OracleRandomResult originalLastRandom)
        {
            throw new InvalidOperationException(
                "Could not snapshot room 2:2e Poe validation frame/RNG state.");
        }

        void SetPoeFlags(bool progress, bool tomb, bool item)
        {
            _saveData.SetRoomFlag(
                record.Group,
                record.Room,
                (byte)record.ProgressFlag,
                progress);
            _saveData.SetRoomFlag(
                record.Group,
                record.Room,
                (byte)record.ItemFlag,
                item);
            _saveData.SetRoomFlag(
                group,
                room,
                (byte)record.ProgressFlag,
                tomb);
        }

        PoeCharacter[] Actors() =>
            _entities.Entities<PoeCharacter>().ToArray();

        // The tomb predicate ignores room 0:7c's item bit: var03 $01 survives
        // exactly when the first encounter has set $40 and this room has not.
        for (int bits = 0; bits < 8; bits++)
        {
            bool progress = (bits & 1) != 0;
            bool tomb = (bits & 2) != 0;
            bool item = (bits & 4) != 0;
            bool expectedActive = progress && !tomb;
            SetPoeFlags(progress, tomb, item);
            LoadValidationRoom(group, room);
            PoeCharacter[] actors = Actors();
            if (actors.Length != 1 ||
                actors[0].Record.Var03 != record.TombVariant ||
                actors[0].Active != expectedActive ||
                poeEvent.HasState != expectedActive)
            {
                throw new InvalidOperationException(
                    $"Room 2:2e Poe state-0 predicate diverged for " +
                    $"progress={progress}, tomb={tomb}, item={item}.");
            }
        }

        SetPoeFlags(progress: true, tomb: false, item: false);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(group, room);

        PoeCharacter tombPoe = Actors().Single();
        Vector2 start = new(0x50, 0x20);
        if (tombPoe.Record is not
            {
                Group: 2,
                Room: 0x2e,
                Id: 0x59,
                SubId: 0,
                Var03: 1,
                X: 0x50,
                Y: 0x20,
                SpriteName: "spr_friendlyghost",
                DefaultAnimation: 2,
                CanFace: true
            } ||
            !tombPoe.Active || tombPoe.Position != start ||
            tombPoe.Record.UpAnimation != record.Animation0 ||
            tombPoe.Record.RightAnimation != record.Animation1 ||
            tombPoe.Record.DownAnimation != record.Animation2 ||
            tombPoe.Record.LeftAnimation != record.Animation3 ||
            tombPoe.CurrentAnimationOpaquePixels == 0 ||
            tombPoe.AnimationRate != 0.0f ||
            !poeEvent.HasState || poeEvent.BlocksGameplay ||
            !poeEvent.ButtonSensitive || poeEvent.CurrentCommandIndex != 1 ||
            !_entities.BlocksLink(tombPoe.Position))
        {
            throw new InvalidOperationException(
                "Room 2:2e did not instantiate its sole INTERAC_POE at " +
                "$20/$50 with native graphics, initialization, and collision.");
        }

        // Strict script collision is six pixels around Link's ten-pixel probe.
        _player.WarpTo(new Vector2(0x50, 0x30));
        _player.Face(Vector2I.Up);
        if (_entities.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "Room 2:2e Poe talk targeting ignored the strict $06 Y boundary.");
        }
        _player.WarpTo(new Vector2(0x50, 0x2f));
        if (_entities.FindTalkTarget(_player) != tombPoe ||
            !_interactions.TryInteract(_player))
        {
            throw new InvalidOperationException(
                "Room 2:2e Poe rejected the final Y point inside radius $06.");
        }
        StepRoomEventFrames(1);
        CutsceneShowTextCommand tombText =
            database.Commands.OfType<CutsceneShowTextCommand>().Single(
                text => text.TextId == 0x0b01);
        if (!_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(tombText.Message) ||
            !poeEvent.BlocksGameplay || !poeEvent.InputDisabled ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "Room 2:2e Poe did not show TX_0b01 with input disabled.");
        }

        _dialogue.Close();
        StepRoomEventFrames(1);
        if (!_saveData.HasRoomFlag(
                group, room, (byte)record.ProgressFlag) ||
            !_saveData.HasRoomFlag(
                record.Group, record.Room, (byte)record.ProgressFlag) ||
            !tombPoe.Active || poeEvent.CurrentCommandIndex != 14)
        {
            throw new InvalidOperationException(
                "The tomb branch did not set room 2:2e bit $40 while retaining " +
                "the initialized actor and room 0:7c progress.");
        }

        StepRoomEventFrames(30);
        if (poeEvent.Counter != 1 || tombPoe.NoFace ||
            tombPoe.Position != start)
        {
            throw new InvalidOperationException(
                "The tomb branch's wait 30 reached var3f=$01 one update early.");
        }
        StepRoomEventFrames(1);
        if (!tombPoe.NoFace || tombPoe.Disappearing ||
            tombPoe.Position != start || poeEvent.CurrentCommandIndex != 16 ||
            poeEvent.Counter != 1)
        {
            throw new InvalidOperationException(
                "The wait-30 boundary did not install Poe var3f=$01 exactly.");
        }

        // var3f selects interactionAnimate plus priority/terrain only. It must
        // retire normal pass prevention before scripted movement begins.
        _player.WarpTo(start);
        Vector2 playerStart = _player.Position;
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 17 ||
            tombPoe.Position != start || _player.Position != playerStart ||
            _entities.BlocksLink(tombPoe.Position) ||
            _entities.FindTalkTarget(_player) is not null)
        {
            throw new InvalidOperationException(
                "Poe var3f=$01 retained NPC blocking, pushing, or talk behavior.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 18 ||
            tombPoe.CurrentScriptAnimationSource != record.Animation2)
        {
            throw new InvalidOperationException(
                "The tomb path did not select animation $02 before moving down.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 19 || tombPoe.Position != start)
        {
            throw new InvalidOperationException(
                "The tomb path's angle $10 command moved the Poe early.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.Counter != 0x49 || tombPoe.Position != start)
        {
            throw new InvalidOperationException(
                "applyspeed $49 moved on its counter-install update.");
        }
        StepRoomEventFrames(0x49 - 1);
        Vector2 firstTurn = new(0x50, 0x68);
        if (poeEvent.Counter != 1 || tombPoe.Position != firstTurn ||
            _player.Position != playerStart)
        {
            throw new InvalidOperationException(
                "The tomb Poe did not move exactly 72 pixels down at SPEED_100.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 20 ||
            tombPoe.Position != firstTurn)
        {
            throw new InvalidOperationException(
                "applyspeed $49 moved on its terminal-zero update.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 21 ||
            tombPoe.CurrentScriptAnimationSource != record.Animation1)
        {
            throw new InvalidOperationException(
                "The tomb path did not select animation $01 before moving right.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 22 ||
            tombPoe.Position != firstTurn)
        {
            throw new InvalidOperationException(
                "The tomb path's angle $08 command moved the Poe early.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.Counter != 0x39 || tombPoe.Position != firstTurn)
        {
            throw new InvalidOperationException(
                "applyspeed $39 moved on its counter-install update.");
        }
        StepRoomEventFrames(0x39 - 1);
        Vector2 destination = new(0x88, 0x68);
        if (poeEvent.Counter != 1 || tombPoe.Position != destination ||
            _player.Position != playerStart)
        {
            throw new InvalidOperationException(
                "The tomb Poe did not move exactly 56 pixels right at SPEED_100.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 23 ||
            tombPoe.Position != destination)
        {
            throw new InvalidOperationException(
                "applyspeed $39 moved on its terminal-zero update.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.CurrentCommandIndex != 6 ||
            poeEvent.Counter != record.DisappearWait ||
            tombPoe.Position != destination)
        {
            throw new InvalidOperationException(
                "The tomb movement did not branch to and install the shared wait.");
        }

        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(record.DisappearWait - 1);
        if (_sound.PlayRequestsFor(record.PoofSound) != 0 ||
            poeEvent.Counter != 1 || tombPoe.Disappearing)
        {
            throw new InvalidOperationException(
                "The tomb Poe's shared wait 40 reached SND_POOF early.");
        }
        StepRoomEventFrames(1);
        if (_sound.PlayRequestsFor(record.PoofSound) != 1 ||
            tombPoe.Disappearing || poeEvent.CurrentCommandIndex != 8)
        {
            throw new InvalidOperationException(
                "The tomb Poe did not play SND_POOF on the wait-40 boundary.");
        }
        StepRoomEventFrames(1);
        if (!tombPoe.Disappearing ||
            poeEvent.Counter != record.FlickerCount ||
            poeEvent.CurrentCommandIndex != 9)
        {
            throw new InvalidOperationException(
                "The tomb Poe did not install the shared var3e=30 flicker.");
        }
        StepRoomEventFrames(record.FlickerCount - 1);
        if (!poeEvent.HasState || !tombPoe.Active ||
            poeEvent.Counter != 1 ||
            tombPoe.Visible !=
                ((_entities.FrameCounter & record.FlickerMask) != 0))
        {
            throw new InvalidOperationException(
                "The tomb Poe's frame-mask flicker ended before decrement 30.");
        }
        StepRoomEventFrames(1);
        if (poeEvent.HasState || tombPoe.Active || tombPoe.Visible ||
            poeEvent.BlocksGameplay || _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The tomb Poe did not restore input and delete at var3e=$00.");
        }

        LoadValidationRoom(group, room);
        if (Actors().Single().Active || poeEvent.HasState)
        {
            throw new InvalidOperationException(
                "Room 2:2e bit $40 did not suppress the Poe on re-entry.");
        }

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "poeScript").ToArray();
        string[] requiredOpcodes =
        [
            "showtext", "orroomflag", "writeobjectbyte", "setspeed",
            "setanimation", "setangle", "applyspeed", "scriptjump",
            "playsound", "flicker"
        ];
        if (commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)))
        {
            throw new InvalidOperationException(
                "Room 2:2e Poe trace lost source lines or a tomb-path opcode.");
        }

        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                record.Group,
                record.Room,
                flag,
                (originalRoomFlags & flag) != 0);
            _saveData.SetRoomFlag(
                group,
                room,
                flag,
                (originalTombFlags & flag) != 0);
        }
        _roomEvents.CommandTraceSink = null;
        frameCounterField.SetValue(_entities, originalFrameCounter);
        frameAccumulatorField.SetValue(_entities, originalFrameAccumulator);
        rng1Field.SetValue(_random, originalRng1);
        rng2Field.SetValue(_random, originalRng2);
        randomCallsField.SetValue(_random, originalRandomCalls);
        lastRandomField.SetValue(_random, originalLastRandom);

        GD.Print("Validated room 2:2e Poe $59:$00 var03 $01: source state-0 " +
            "predicate, sole placement, strict $06 talk geometry, TX_0b01, " +
            "room bit $40, exact wait 30, nonblocking var3f movement from " +
            "$20/$50 through $68/$50 to $68/$88, and shared poof/flicker.");
    }
}
