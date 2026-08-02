using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2f3DepressedBoy()
    {
        const int group = 2;
        const int room = 0xf3;
        const int tradeItemAddress = 0xc6c0;
        const int tradeObtainedAddress = 0xc69a +
            (TreasureDatabase.TreasureTradeItem >> 3);
        const int tradeObtainedMask =
            1 << (TreasureDatabase.TreasureTradeItem & 7);

        DepressedBoyEvent boyEvent = _roomEvents.DepressedBoy;
        DepressedBoyEventDatabase database = boyEvent.Database;
        DepressedBoyEventRecord record = database.Record;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FailIf(reloadInventory is null,
            "Could not reload depressed-boy validation inventory.");

        void SetTradeItem(int tradeItem, bool obtained)
        {
            byte flags = _saveData.ReadWramByte(tradeObtainedAddress);
            flags = obtained
                ? (byte)(flags | tradeObtainedMask)
                : (byte)(flags & ~tradeObtainedMask);
            _saveData.WriteWramByte(tradeItemAddress, (byte)tradeItem);
            _saveData.WriteWramByte(tradeObtainedAddress, flags);
            _saveData.CommitInventoryChange();
            reloadInventory.Invoke(_inventory, null);
        }

        CutsceneShowTextCommand Text(int textId) =>
            database.Commands.OfType<CutsceneShowTextCommand>().Single(
                text => text.TextId == textId);

        void ExpectDialogue(int textId, string phase)
        {
            CutsceneShowTextCommand text = Text(textId);
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(text.Message),
                $"Room 2:f3 {phase} did not show TX_{textId:x4}.");
        }

        DepressedBoyCharacter Boy() =>
            _entities.Entities<DepressedBoyCharacter>().Single();

        void PositionForTalk()
        {
            _player.WarpTo(new Vector2(0x50, 0x37));
            _player.Face(Vector2I.Up);
        }

        void ReachNpcLoop()
        {
            StepRoomEventFrames(9);
            FailIf(
                _currentRoom.TemporaryBackgroundPaletteOffset != -9 ||
                !boyEvent.PaletteFadeActive ||
                boyEvent.CurrentCommandIndex != 3,
                "darkenRoomLightly did not render offsets $ff-$f7 before " +
                "finishing its palette thread.");
            StepRoomEventFrames(1);
            FailIf(
                boyEvent.PaletteFadeActive || boyEvent.PaletteOffset != -9 ||
                boyEvent.CurrentCommandIndex != 3,
                "darkenRoomLightly did not stop one update after rendering $f7.");
            StepRoomEventFrames(1);
            FailIf(
                boyEvent.CurrentCommandIndex != 4 || !boyEvent.ButtonSensitive,
                "boySubid07Script did not enter @npcLoop after its palette gate.");
        }

        void BeginTalk(int expectedText)
        {
            PositionForTalk();
            FailIf(
                _entities.FindTalkTarget(_player) != Boy() ||
                !_interactions.TryInteract(_player),
                "Room 2:f3 depressed boy was not reachable through the normal " +
                "A-button path.");
            StepRoomEventFrames(1);
            ExpectDialogue(expectedText, "interaction");
            FailIf(
                !boyEvent.BlocksGameplay || !_player.CutsceneControlled,
                "boySubid07Script did not disable input before dialogue.");
        }

        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlagItem, value: false);
        SetTradeItem(tradeItem: 0, obtained: false);
        LoadValidationRoom(group, room);

        DepressedBoyCharacter boy = Boy();
        FailIf(
            boy.Record is not { Id: 0x3c, SubId: 0x07, Var03: 0x00 } ||
            boy.Position != new Vector2(0x50, 0x28) ||
            boy.Record.SpriteName != "spr_kids" ||
            boy.Record.Implementation !=
                NpcImplementationClassification.SpecializedNative ||
            boy.AnimationRate != 0.0f || boy.CutscenePose ||
            !boyEvent.HasState || boyEvent.BlocksGameplay ||
            boyEvent.CurrentCommandIndex != 2 ||
            _currentRoom.TemporaryBackgroundPaletteOffset != 0,
            "Room 2:f3 did not instantiate INTERAC_BOY $3c:$07 at $28,$50 " +
            "with its one-update native/script initialization.");
        ReachNpcLoop();

        BeginTalk(0x2517);
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            boyEvent.BlocksGameplay || _player.CutsceneControlled ||
            boyEvent.CurrentCommandIndex != 4,
            "The missing-Funny-Joke branch did not restore input immediately.");

        SetTradeItem(record.RequiredTradeItem, obtained: true);
        LoadValidationRoom(group, room);
        ReachNpcLoop();
        BeginTalk(0x2515);
        FailIf(!_dialogue.ChoiceActive,
            "TX_2515 did not expose the Funny Joke Yes/No options.");
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(31);
        ExpectDialogue(0x2517, "declined Funny Joke branch");
        FailIf(
            _inventory.TradeItem != record.RequiredTradeItem ||
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem),
            "Declining the Funny Joke changed inventory or room bit $20.");
        _dialogue.Close();
        StepRoomEventFrames(1);

        BeginTalk(0x2515);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(31);
        FailIf(
            boyEvent.CurrentCommandIndex != 16 || !Boy().CutscenePose,
            "The accepted text-option branch did not yield after writing var3d=$01.");
        StepRoomEventFrames(1);
        FailIf(
            boyEvent.CurrentCommandIndex != 17 || !Boy().CutscenePose ||
            !boyEvent.LinkApproachActive ||
            _player.PrecisePosition.Y != 0x38,
            "Accepting the Funny Joke did not set var3d=$01 and begin " +
            "linkCutscene5 path $02 after the exact 30-update wait " +
            $"(command={boyEvent.CurrentCommandIndex}, pose={boy.CutscenePose}, " +
            $"approach={boyEvent.LinkApproachActive}, y={_player.PrecisePosition.Y}).");

        int approachUpdates = 0;
        while (boyEvent.LinkApproachActive && approachUpdates++ < 32)
            StepRoomEventFrames(1);
        FailIf(
            boyEvent.LinkApproachActive || approachUpdates >= 32 ||
            Mathf.FloorToInt(_player.PrecisePosition.Y) != record.ApproachY,
            "linkCutscene5 path $02 did not reach Y=$48 at SPEED_100.");
        StepRoomEventFrames(3);
        FailIf(
            Boy().CutscenePose || _player.FacingVector != Vector2I.Down ||
            boyEvent.CurrentCommandIndex != 21,
            "The completed Link path did not clear var3d and force DIR_DOWN " +
            $"(command={boyEvent.CurrentCommandIndex}, pose={Boy().CutscenePose}, " +
            $"facing={_player.FacingVector}, approachUpdates={approachUpdates}).");

        StepRoomEventFrames(39);
        FailIf(
            _sound.ActiveMusic == record.DanceMusic,
            "The pre-dance wait started MUS_CRAZY_DANCE one update early.");
        StepRoomEventFrames(1);
        FailIf(
            _sound.ActiveMusic != record.DanceMusic ||
            boyEvent.CurrentCommandIndex != 23,
            "The 40-update wait did not start MUS_CRAZY_DANCE $31.");

        StepRoomEventFrames(120);
        FailIf(
            _player.ScriptedLinkAnimationMode.HasValue ||
            boyEvent.CurrentCommandIndex != 23,
            "The 120-update dance lead-in ended one update early.");
        StepRoomEventFrames(1);
        FailIf(
            _player.ScriptedLinkAnimationMode != 0x08 ||
            boyEvent.DanceIndex != 1 || boyEvent.DanceCounter != 0x14,
            "boy_runFunnyJokeCutscene did not consume initial var3f=$01 " +
            "into animation mode $08 for $14 updates.");

        var seenModes = new Dictionary<int, ulong>();
        void ObserveMode()
        {
            if (_player.ScriptedLinkAnimationMode is int mode)
                seenModes[mode] = _player.ScriptedLinkAnimationPixelHash;
        }
        ObserveMode();
        StepRoomEventFrames(19);
        ObserveMode();
        FailIf(
            _player.ScriptedLinkAnimationMode != 0x08 ||
            boyEvent.DanceCounter != 1,
            "Funny Joke animation $08 did not retain its exact $14 counter.");
        StepRoomEventFrames(1);
        ObserveMode();
        FailIf(
            _player.ScriptedLinkAnimationMode != 0x09 ||
            boyEvent.DanceIndex != 2 || boyEvent.DanceCounter != 0x14,
            "Funny Joke dance did not switch to mode $09 on the counter-zero update.");

        int danceUpdates = 21;
        while (!boyEvent.DanceComplete && danceUpdates++ < 500)
        {
            StepRoomEventFrames(1);
            ObserveMode();
        }
        int[] expectedModes = [0x06, 0x07, 0x08, 0x09, 0x0e, 0x1c];
        FailIf(
            !boyEvent.DanceComplete || danceUpdates != 451 ||
            boyEvent.DanceIndex != record.DanceCount ||
            expectedModes.Any(mode =>
                !seenModes.TryGetValue(mode, out ulong hash) || hash == 0) ||
            seenModes.Values.Distinct().Count() != expectedModes.Length ||
            _sound.ActiveMusic != 0 || boyEvent.CurrentCommandIndex != 28,
            "The imported 20-of-21 Funny Joke animation sequence, static Link " +
            "poses, or restartSound boundary diverged " +
            $"(complete={boyEvent.DanceComplete}, updates={danceUpdates}, " +
            $"index={boyEvent.DanceIndex}, modes={string.Join(',', seenModes.Keys)}, " +
            $"hashes={seenModes.Values.Distinct().Count()}, music={_sound.ActiveMusic:x2}, " +
            $"command={boyEvent.CurrentCommandIndex}).");

        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(39);
        FailIf(
            _sound.PlayRequestsFor(record.RewardSound) != 0,
            "SND_SWORD_OBTAINED played before the 40-update post-dance wait.");
        StepRoomEventFrames(1);
        FailIf(
            _sound.PlayRequestsFor(record.RewardSound) != 1 ||
            boyEvent.CurrentCommandIndex != 30,
            "SND_SWORD_OBTAINED did not play on the post-dance wait boundary.");
        StepRoomEventFrames(1);
        FailIf(
            _player.ScriptedLinkAnimationMode != 0x0f ||
            _player.ScriptedLinkAnimationPixelHash == 0,
            "wcc50 did not select LINK_ANIM_MODE_GETITEM2HAND $0f.");
        StepRoomEventFrames(119);
        FailIf(
            _player.ScriptedLinkAnimationMode != 0x0f,
            "The scripted two-hand pose ended before 120 updates.");
        StepRoomEventFrames(1);
        FailIf(
            _player.ScriptedLinkAnimationMode.HasValue ||
            _player.FacingVector != Vector2I.Up ||
            boyEvent.CurrentCommandIndex != 33,
            "The scripted two-hand pose did not end with forced DIR_UP.");

        StepRoomEventFrames(30);
        ExpectDialogue(0x2516, "post-joke response");
        _dialogue.Close();
        StepRoomEventFrames(31);
        GroundTreasurePickup reward =
            _entities.Entities<GroundTreasurePickup>().Single();
        TreasureObjectRecord rewardObject =
            _treasures.GetObject(record.RewardObject);
        FailIf(
            reward.Record.TreasureObject != record.RewardObject ||
            reward.Record.GrabMode != 2 || !reward.Held ||
            !_player.IsHoldingItemTwoHands ||
            _inventory.TradeItem != record.RewardParameter ||
            !_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(rewardObject.Message),
            "giveitem did not grant the Touching Book through grab mode $02 " +
            "with inventory, dialogue, held pose, and room bit $20.");

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(31);
        FailIf(
            boyEvent.BlocksGameplay || _player.CutsceneControlled ||
            boyEvent.CurrentCommandIndex != 4 ||
            _sound.ActiveMusic == record.DanceMusic,
            "The Touching Book tail did not reset room music and restore input.");

        LoadValidationRoom(0, 0x55);
        LoadValidationRoom(group, room);
        ReachNpcLoop();
        BeginTalk(0x2518);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room bit $20 did not suppress a second Touching Book reward.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        ValidateInteractiveInfiniteScriptCancellation(
            boyEvent,
            Boy(),
            "Depressed Boy");
        FailIf(
            _player.ScriptedLinkAnimationMode.HasValue ||
            boyEvent.LinkApproachActive || boyEvent.DanceComplete,
            "Depressed-boy cancellation retained Link or dance state.");

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "boySubid07Script").ToArray();
        string[] requiredOpcodes =
        [
            "checkmemoryeq", "jumpifroomflagset", "jumpiftradeitemeq",
            "writeobjectbyte", "jumpifmemoryeqyieldonmiss", "giveitem"
        ];
        FailIf(
            commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)),
            "Depressed-boy typed trace lost source lines or a required opcode.");

        LoadValidationRoom(0, 0x55);
        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        reloadInventory.Invoke(_inventory, null);
        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                group, room, flag, (originalRoomFlags & flag) != 0);
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 2:f3 depressed boy $3c:$07: source-typed " +
            "darkenRoomLightly, missing/No/Yes Funny Joke paths, exact " +
            "linkCutscene5 Y=$48 approach, 40/120/451/40/120/30 waits, " +
            "20-of-21 dance modes, SND_SWORD_OBTAINED, two-hand Touching Book, " +
            "room bit $20 re-entry, music reset, and cancellation.");
    }
}
