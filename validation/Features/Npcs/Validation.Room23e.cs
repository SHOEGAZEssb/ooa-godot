using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom23eToiletHand()
    {
        const int group = 2;
        const int room = 0x3e;
        const int tradeItemAddress = 0xc6c0;
        const int tradeObtainedAddress = 0xc69a +
            (TreasureDatabase.TreasureTradeItem >> 3);
        const int tradeObtainedMask =
            1 << (TreasureDatabase.TreasureTradeItem & 7);

        ToiletHandEvent toiletEvent = _roomEvents.ToiletHand;
        ToiletHandEventDatabase database = toiletEvent.Database;
        ToiletHandEventRecord record = database.Record;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        OracleRandomState randomSnapshot = _random.CaptureState();
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FailIf(reloadInventory is null, "Could not reload Toilet Hand validation inventory.");

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
            database.Commands
                .Concat(database.ReactionCommands)
                .OfType<CutsceneShowTextCommand>()
                .First(text => text.TextId == textId);

        void ExpectDialogue(int textId, string phase)
        {
            CutsceneShowTextCommand text = Text(textId);
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(text.Message),
                $"Room 2:3e {phase} did not show TX_{textId:x4}.");
        }

        void AdvanceUntil(
            Func<bool> predicate,
            int maximumUpdates,
            string failure)
        {
            if (predicate())
                return;
            for (int update = 0; update < maximumUpdates; update++)
            {
                StepRoomEventFrames(1);
                if (predicate())
                    return;
            }
            FailIf(true, failure);
        }

        ToiletHandCharacter Hand() =>
            _entities.Entities<ToiletHandCharacter>().Single();

        void PositionClose()
        {
            // Packed position $68 is in the native proximity table. Fifteen
            // pixels below the hand is outside the combined $06+$06 solid
            // radii while Link's ten-pixel A probe remains inside the hand's
            // strict $06 talk radius.
            _player.WarpTo(new Vector2(0x88, 0x63), recordSafe: false);
            _player.Face(Vector2I.Up);
        }

        void Emerge()
        {
            PositionClose();
            AdvanceUntil(
                () => Hand().ScriptVisible &&
                    Hand().Direction == 1 &&
                    toiletEvent.CurrentCommandIndex == 7,
                32,
                "The Toilet Hand did not complete animation $00 and enter " +
                "its animation-$01 proximity loop.");
        }

        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlagItem, value: false);
        SetTradeItem(tradeItem: 0, obtained: false);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(group, room);

        ToiletHandCharacter hand = Hand();
        FailIf(
            hand.Record is not
            {
                Group: group,
                Room: room,
                Id: 0x5b,
                SubId: 0x00,
                Var03: 0x00,
                Implementation:
                    NpcImplementationClassification.SpecializedNative
            } ||
            hand.Position != new Vector2(0x88, 0x54) ||
            hand.ScriptVisible ||
            hand.Direction != 0 ||
            hand.CurrentScriptAnimationSource != record.Animation0 ||
            hand.CurrentAnimationTextureSize != new Vector2I(8, 16) ||
            hand.CurrentAnimationOpaquePixels == 0 ||
            hand.NativeCollisionRadiusY != 0x06 ||
            hand.NativeCollisionRadiusX != 0x06 ||
            !toiletEvent.HasState ||
            !toiletEvent.ButtonSensitive ||
            toiletEvent.CurrentCommandIndex != 3 ||
            toiletEvent.Counter != 1 ||
            !record.AlwaysUpdate,
            "Room 2:3e did not load specialized INTERAC_TOILET_HAND " +
            "$5b:$00 at $88/$54, initialize animation $00, hide it, and " +
            "reach the one-update proximity wait: " +
            $"record={hand.Record.Group}:{hand.Record.Room:x2} " +
            $"{hand.Record.Id:x2}:{hand.Record.SubId:x2}/v{hand.Record.Var03:x2} " +
            $"impl={hand.Record.Implementation}, pos={hand.Position}, " +
            $"visible={hand.ScriptVisible}, dir={hand.Direction}, " +
            $"animation={hand.CurrentScriptAnimationSource == record.Animation0}, " +
            $"size={hand.CurrentAnimationTextureSize}, " +
            $"pixels={hand.CurrentAnimationOpaquePixels}, " +
            $"radii={hand.NativeCollisionRadiusY:x2}/" +
            $"{hand.NativeCollisionRadiusX:x2}, " +
            $"state={toiletEvent.HasState}, button={toiletEvent.ButtonSensitive}, " +
            $"command={toiletEvent.CurrentCommandIndex}, " +
            $"counter={toiletEvent.Counter}, always={record.AlwaysUpdate}.");

        // Before the hand has ever emerged, the source's buggy low-priority
        // test is zero and the object-in-hole script takes its 90-update path.
        _roomEvents.NotifyObjectFellInHole(ObjectFellInHoleKind.PushBlock);
        StepRoomEventFrames(1);
        FailIf(
            !toiletEvent.ReactionActive ||
            toiletEvent.CurrentCommandIndex != 2 ||
            toiletEvent.Counter != 0 ||
            hand.ScriptVisible,
            "A first hidden pushblock drop did not yield after the source " +
            "jumpifmemoryset miss before its 90-update wait.");
        _roomEvents.NotifyObjectFellInHole(ObjectFellInHoleKind.Bomb);
        FailIf(
            toiletEvent.PendingHoleReaction >= 0,
            "An object dropped during the active reaction escaped the source " +
            "buffer clear and queued a second response.");
        StepRoomEventFrames(1);
        FailIf(
            toiletEvent.CurrentCommandIndex != 2 ||
            toiletEvent.Counter != 90,
            "The hidden object reaction did not install its 90-update wait " +
            "on the update after jumpifmemoryset yielded.");
        AdvanceUntil(
            () => _dialogue.IsOpen,
            90,
            "The hidden pushblock reaction did not finish its 90-update wait.");
        ExpectDialogue(0x0b0a, "pushblock reaction");
        _dialogue.Close();
        AdvanceUntil(
            () => !toiletEvent.ReactionActive &&
                toiletEvent.CurrentCommandIndex == 3,
            3,
            "The pushblock reaction did not reload toiletHandScript and " +
            "clear the object-in-hole state.");

        Emerge();
        hand = Hand();
        FailIf(
            !toiletEvent.LinkClose ||
            !hand.HasVisiblePriority ||
            hand.CurrentScriptAnimationSource != record.Animation1 ||
            !_entities.BlocksLink(hand.Position) ||
            _entities.FindTalkTarget(_player) != hand,
            "The close-position table $57/$68/$67 did not expose a solid, " +
            "A-sensitive Toilet Hand with animation $01 and native priority.");
        FailIf(
            !_entities.BlocksLink(hand.Position + Vector2.Down * 11.0f) ||
            _entities.BlocksLink(hand.Position + Vector2.Down * 12.0f),
            "The Toilet Hand did not use the strict combined $06+$06 solid " +
            "boundary from initcollisions.");
        _player.WarpTo(
            hand.Position + Vector2.Down * 16.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            _entities.FindTalkTarget(_player) == hand,
            "The Toilet Hand accepted Link's A-button probe at the strict " +
            "$06 talk-radius boundary.");
        PositionClose();
        FailIf(
            _entities.FindTalkTarget(_player) != hand,
            "The Toilet Hand rejected Link's A-button probe one pixel inside " +
            "the strict $06 talk-radius boundary.");

        for (int idleUpdate = 0; idleUpdate < 8; idleUpdate++)
        {
            StepRoomEventFrames(1);
            FailIf(
                toiletEvent.CurrentCommandIndex != 10 ||
                !hand.ScriptVisible ||
                hand.Direction != 1 ||
                _dialogue.IsOpen,
                "Holding Link in a Toilet Hand proximity cell did not yield " +
                "once per update at the jumpifmemoryset miss: " +
                $"idle={idleUpdate}, command={toiletEvent.CurrentCommandIndex}.");
        }

        // Once interactionAnimateAsNpc has written a nonzero priority, the
        // source's mistaken bit test sends reactions through the fast retreat.
        int explosionRequests =
            _sound.PlayRequestsFor(OracleSoundEngine.SndExplosion);
        _roomEvents.NotifyObjectFellInHole(ObjectFellInHoleKind.Bomb);
        StepRoomEventFrames(1);
        FailIf(
            !toiletEvent.ReactionActive ||
            hand.Direction != 2 ||
            toiletEvent.CurrentCommandIndex != 27,
            "A visible bomb drop did not select animation $02 and the " +
            "fast reaction retreat.");
        AdvanceUntil(
            () => _sound.PlayRequestsFor(OracleSoundEngine.SndExplosion) >
                explosionRequests,
            64,
            "The visible bomb reaction did not retreat, wait 45 updates, " +
            "and request SND_EXPLOSION.");
        FailIf(
            _entities.ScreenShakeCounter != 60 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndExplosion) !=
                explosionRequests + 1,
            "The bomb reaction did not start the source 60-update screen " +
            "shake with one explosion cue.");
        AdvanceUntil(
            () => _dialogue.IsOpen,
            62,
            "The bomb reaction did not wait 60 updates before its response.");
        ExpectDialogue(0x0b25, "bomb reaction");
        _dialogue.Close();
        AdvanceUntil(
            () => !toiletEvent.ReactionActive &&
                toiletEvent.CurrentCommandIndex == 3,
            3,
            "The bomb reaction did not reload the normal proximity loop.");

        Emerge();
        FailIf(
            !_interactions.TryInteract(_player),
            "The emerged Toilet Hand was not reachable through the normal " +
            "A-button interaction path.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x0b07, "paper request");
        FailIf(
            !toiletEvent.BlocksGameplay ||
            !_player.CutsceneControlled,
            "TX_0b07 did not disable Link input.");

        _dialogue.Close();
        AdvanceUntil(
            () => !hand.ScriptVisible &&
                !toiletEvent.BlocksGameplay &&
                toiletEvent.CurrentCommandIndex == 15,
            64,
            "The missing-Stationery branch did not wait 30 updates, retreat, " +
            "hide, restore input, and wait for Link to leave.");
        FailIf(
            toiletEvent.BlocksGameplay ||
            _player.CutsceneControlled ||
            _saveData.HasRoomFlag(
                group, room, OracleSaveData.RoomFlagItem),
            "The missing-Stationery branch changed room completion or kept " +
            "input disabled.");
        for (int closeUpdate = 0; closeUpdate < 3; closeUpdate++)
        {
            StepRoomEventFrames(1);
            FailIf(
                toiletEvent.CurrentCommandIndex != 15 ||
                hand.ScriptVisible,
                "The hidden Toilet Hand did not yield while Link remained in " +
                "the $68 proximity cell.");
        }
        _player.WarpTo(new Vector2(0x38, 0x38));
        AdvanceUntil(
            () => toiletEvent.CurrentCommandIndex == 3,
            3,
            "Leaving the Toilet Hand proximity cells did not restart its " +
            "one-update approach loop.");

        SetTradeItem(record.RequiredTradeItem, obtained: true);
        Emerge();
        FailIf(
            !_interactions.TryInteract(_player),
            "The Toilet Hand did not accept the Stationery interaction.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x0b07, "Stationery preamble");
        _dialogue.Close();
        AdvanceUntil(
            () => _dialogue.IsOpen,
            32,
            "The Stationery branch did not reach its 30-update trade prompt.");
        ExpectDialogue(0x0b08, "Stationery prompt");
        FailIf(
            !_dialogue.ChoiceActive,
            "TX_0b08 did not expose its Yes/No options.");
        _dialogue.SubmitChoiceForValidation(0);
        AdvanceUntil(
            () => _dialogue.IsOpen,
            32,
            "Accepting Stationery did not reach TX_0b09 after its 30-update wait.");
        ExpectDialogue(0x0b09, "accepted Stationery response");

        _dialogue.Close();
        AdvanceUntil(
            () => _dialogue.IsOpen,
            100,
            "The accepted trade did not retreat and reach TX_0b0b.");
        ExpectDialogue(0x0b0b, "accepted-trade pause");
        FailIf(
            hand.ScriptVisible,
            "The Toilet Hand was not hidden for TX_0b0b.");

        _dialogue.Close();
        AdvanceUntil(
            () => _dialogue.IsOpen,
            100,
            "The accepted trade did not re-emerge and reach TX_0b0c.");
        ExpectDialogue(0x0b0c, "Stink Bag offer");
        FailIf(
            !hand.ScriptVisible ||
            hand.Direction != 1 ||
            hand.CurrentScriptAnimationSource != record.Animation1,
            "The Toilet Hand did not complete animation $00 and idle in " +
            "animation $01 for TX_0b0c.");

        _sound.ClearPlayRequestAudit();
        _dialogue.Close();
        AdvanceUntil(
            () => _entities.Entities<GroundTreasurePickup>().Any(),
            32,
            "toiletHandScript did not grant the Stink Bag after its final " +
            "30-update wait.");
        GroundTreasurePickup reward =
            _entities.Entities<GroundTreasurePickup>().Single();
        TreasureObjectRecord rewardObject =
            _treasures.GetObject(record.RewardObject);
        FailIf(
            reward.Record.TreasureObject != record.RewardObject ||
            reward.Record.SpawnMode != 0 ||
            reward.Record.GrabMode != 2 ||
            !reward.Held ||
            !_player.IsHoldingItemTwoHands ||
            reward.Position != _player.Position + new Vector2(0, -14) ||
            _inventory.TradeItem != record.RewardParameter ||
            !_saveData.HasRoomFlag(
                group, room, OracleSaveData.RoomFlagItem) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage !=
                DialogueBox.PlainText(rewardObject.Message) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2,
            "toiletHandScript giveitem did not exchange Stationery for the " +
            "two-hand Stink Bag, set room bit $20, and open TX_005c.");

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Closing the Stink Bag text did not release Link and delete the reward.");
        AdvanceUntil(
            () => !toiletEvent.BlocksGameplay &&
                !hand.ScriptVisible &&
                toiletEvent.CurrentCommandIndex == 15,
            64,
            "The post-reward retreat did not hide the hand and restore input.");

        LoadValidationRoom(0, 0x55);
        LoadValidationRoom(group, room);
        Emerge();
        FailIf(
            !_interactions.TryInteract(_player),
            "Completed room 2:3e did not retain the Toilet Hand talk target.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x0b09, "completed-trade branch");
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room bit $20 did not suppress a second Stink Bag reward.");
        _dialogue.Close();
        AdvanceUntil(
            () => !toiletEvent.BlocksGameplay &&
                !Hand().ScriptVisible &&
                toiletEvent.CurrentCommandIndex == 15,
            64,
            "The completed-room dialogue did not retreat and restore input.");

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started).ToArray();
        string[] requiredOpcodes =
        [
            "initcollisions", "jumpifmemoryeq",
            "jumpifmemoryeqyieldonmiss", "jumpifroomflagset",
            "jumpiftradeitemeq", "jumpiftextoptioneq", "jumptablememory",
            "giveitem"
        ];
        FailIf(
            commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)),
            "Toilet Hand typed traces lost source lines or a required " +
            "room-specific script opcode.");

        LoadValidationRoom(0, 0x55);
        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        reloadInventory.Invoke(_inventory, null);
        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                group, room, flag, (originalRoomFlags & flag) != 0);
        }
        _random.RestoreState(randomSnapshot);
        _roomEvents.CommandTraceSink = null;

        GD.Print(
            "Validated room 2:3e Toilet Hand placement, $57/$68/$67 " +
            "proximity emergence, $00/$01/$02 terminal animation cadence, " +
            "90-update hidden and fast-priority object-in-hole reactions, " +
            "bomb shake/sound, Stationery branches, Stink Bag grant, room " +
            "flag $20 completion, and re-entry dialogue.");
    }
}
