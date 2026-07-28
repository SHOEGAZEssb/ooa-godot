using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2e6MaskSalesman()
    {
        const int group = 2;
        const int room = 0xe6;
        const int exteriorGroup = 0;
        const int exteriorRoom = 0x53;
        const int tradeItemAddress = 0xc6c0;
        const int tradeObtainedAddress = 0xc69a +
            (TreasureDatabase.TreasureTradeItem >> 3);
        const int tradeObtainedMask =
            1 << (TreasureDatabase.TreasureTradeItem & 7);

        MaskSalesmanEvent maskEvent = _roomEvents.MaskSalesman;
        MaskSalesmanEventDatabase database = maskEvent.Database;
        MaskSalesmanEventRecord record = database.Record;
        byte originalRoomFlags = _saveData.GetRoomFlags(group, room);
        var inventorySnapshot = new byte[0x39];
        _saveData.ReadWramBytes(0xc688, inventorySnapshot);
        MethodInfo? reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FailIf(reloadInventory is null, "Could not reload Mask Salesman validation inventory.");

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
            database.Commands.OfType<CutsceneShowTextCommand>().First(
                text => text.TextId == textId);

        void ExpectDialogue(int textId, string phase)
        {
            CutsceneShowTextCommand text = Text(textId);
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(text.Message),
                $"Room 2:e6 {phase} did not show TX_{textId:x4}.");
        }

        MaskSalesmanCharacter Salesman() =>
            _entities.Entities<MaskSalesmanCharacter>().Single();

        void PositionForTalk()
        {
            _player.WarpTo(new Vector2(0x70, 0x44));
            _player.Face(Vector2I.Up);
        }

        void BeginPreamble()
        {
            PositionForTalk();
            FailIf(
                !_interactions.TryInteract(_player),
                "Room 2:e6 Mask Salesman was not reachable through the normal A-button path.");
            StepRoomEventFrames(2);
            ExpectDialogue(0x0b0d, "hunger preamble");
            FailIf(
                Salesman().CurrentScriptAnimationSource != record.Animation0,
                "TX_0b0d did not select Mask Salesman animation $00.");
        }

        void FinishHungerPreamble(bool expectTradePrompt)
        {
            _dialogue.Close();
            StepRoomEventFrames(17);
            ExpectDialogue(0x0b0e, "first hunger outburst");
            FailIf(
                Salesman().CurrentScriptAnimationSource != record.Animation1,
                "The first TX_0b0e did not select Mask Salesman animation $01.");

            _dialogue.Close();
            StepRoomEventFrames(17);
            ExpectDialogue(0x0b0f, "apology");
            FailIf(
                Salesman().CurrentScriptAnimationSource != record.Animation0,
                "TX_0b0f did not restore Mask Salesman animation $00.");

            _dialogue.Close();
            StepRoomEventFrames(17);
            ExpectDialogue(0x0b0e, "second hunger outburst");
            FailIf(
                Salesman().CurrentScriptAnimationSource != record.Animation1,
                "The second TX_0b0e did not select Mask Salesman animation $01.");

            _dialogue.Close();
            StepRoomEventFrames(31);
            if (expectTradePrompt)
            {
                ExpectDialogue(0x0b10, "Tasty Meat prompt");
                FailIf(!_dialogue.ChoiceActive, "TX_0b10 did not expose its Yes/No options.");
            }
            else
            {
                FailIf(
                    _dialogue.IsOpen || maskEvent.BlocksGameplay ||
                    _player.CutsceneControlled ||
                    maskEvent.CurrentCommandIndex != 2,
                    "Missing Tasty Meat did not restore input after the exact 30-update wait.");
            }
        }

        _saveData.SetRoomFlag(
            group, room, OracleSaveData.RoomFlagItem, value: false);
        SetTradeItem(tradeItem: 0, obtained: false);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(group, room);

        MaskSalesmanCharacter salesman = Salesman();
        FailIf(
            salesman.Record is not { Id: 0x5c, SubId: 0x00 } ||
            salesman.Position != new Vector2(0x70, 0x38) ||
            salesman.Record.SpriteName != "spr_masksalesman_rafton" ||
            !maskEvent.HasState || maskEvent.BlocksGameplay ||
            maskEvent.ButtonSensitive || maskEvent.CurrentCommandIndex != 1 ||
            salesman.CurrentScriptAnimationSource !=
                record.Animation(record.InitialAnimation) ||
            salesman.CurrentAnimationTextureSize != new Vector2I(16, 32) ||
            salesman.CurrentAnimationOffset != new Vector2(-8, -32) ||
            salesman.AnimationRate != 0.0f ||
            salesman.CurrentAnimationOpaquePixels == 0,
            "Room 2:e6 did not preserve INTERAC_MASK_SALESMAN's placement, " +
            "complete signed-OAM animation-$00 visual, or one-update state-0 " +
            "script initialization.");

        PositionForTalk();
        FailIf(
            _entities.FindTalkTarget(_player) is not null,
            "INTERAC_MASK_SALESMAN became A-sensitive before makeabuttonsensitive.");
        StepRoomEventFrames(1);
        FailIf(
            !maskEvent.ButtonSensitive || maskEvent.CurrentCommandIndex != 2 ||
            _entities.FindTalkTarget(_player) != salesman,
            "maskSalesmanScript did not reach checkabutton on its second script update.");
        _player.WarpTo(new Vector2(0x70, 0x46));
        FailIf(
            _entities.FindTalkTarget(_player) is not null,
            "Mask Salesman talk targeting ignored strict collision radius Y=$04.");
        _player.WarpTo(new Vector2(0x70, 0x45));
        FailIf(
            _entities.FindTalkTarget(_player) != salesman,
            "Mask Salesman talk targeting rejected the final point inside radius Y=$04.");

        BeginPreamble();
        FailIf(
            !maskEvent.BlocksGameplay || !_player.CutsceneControlled,
            "maskSalesmanScript did not disable input before TX_0b0d.");
        FinishHungerPreamble(expectTradePrompt: false);

        _inventory.GiveTreasure(
            TreasureDatabase.TreasureTradeItem, record.RequiredTradeItem);
        LoadValidationRoom(group, room);
        StepRoomEventFrames(1);
        BeginPreamble();
        FinishHungerPreamble(expectTradePrompt: true);
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(31);
        ExpectDialogue(0x0b14, "declined Tasty Meat trade");
        FailIf(
            _dialogue.Position.Y != 96 ||
            _inventory.TradeItem != record.RequiredTradeItem ||
            _saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem),
            "Declining the Tasty Meat trade changed inventory/room bit $20 " +
            "or ignored TX_0b14's lower textbox.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            maskEvent.BlocksGameplay || _player.CutsceneControlled ||
            maskEvent.CurrentCommandIndex != 2,
            "The declined Mask Salesman trade did not restore input on the next update.");

        BeginPreamble();
        FinishHungerPreamble(expectTradePrompt: true);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(31);
        ExpectDialogue(0x0b45, "accepted Tasty Meat response");

        _dialogue.Close();
        StepRoomEventFrames(17);
        ExpectDialogue(0x0b11, "Tasty Meat consumption");
        FailIf(
            _dialogue.Position.Y != 96 ||
            Salesman().CurrentScriptAnimationSource != record.Animation0,
            "TX_0b11 did not use its lower textbox and animation $00.");
        _dialogue.Close();
        StepRoomEventFrames(17);
        ExpectDialogue(0x0b12, "greedy response");
        FailIf(
            Salesman().CurrentScriptAnimationSource != record.Animation1,
            "TX_0b12 did not select Mask Salesman animation $01.");
        _dialogue.Close();
        StepRoomEventFrames(17);
        ExpectDialogue(0x0b13, "reward offer");
        FailIf(
            Salesman().CurrentScriptAnimationSource != record.Animation0,
            "TX_0b13 did not restore Mask Salesman animation $00.");
        _dialogue.Close();
        StepRoomEventFrames(17);
        ExpectDialogue(0x0b45, "final meat outburst");
        FailIf(
            Salesman().CurrentScriptAnimationSource != record.Animation1,
            "The final TX_0b45 did not select Mask Salesman animation $01.");

        _sound.ClearPlayRequestAudit();
        _dialogue.Close();
        StepRoomEventFrames(31);
        GroundTreasurePickup reward =
            _entities.Entities<GroundTreasurePickup>().Single();
        TreasureObjectRecord rewardObject =
            _treasures.GetObject(record.RewardObject);
        FailIf(
            reward.Record.TreasureObject != record.RewardObject ||
            reward.Record.SpawnMode != 0 || reward.Record.GrabMode != 2 ||
            !reward.Held || !_player.IsHoldingItemTwoHands ||
            reward.Position != _player.Position + new Vector2(0, -14) ||
            _inventory.TradeItem != record.RewardParameter ||
            !_saveData.HasRoomFlag(group, room, OracleSaveData.RoomFlagItem) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(rewardObject.Message) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2,
            "maskSalesmanScript giveitem did not grant the Doggie Mask through " +
            "grab mode $02 with text, sounds, inventory, and room bit $20.");

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _player.IsHoldingItemTwoHands ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Closing the Doggie Mask text did not release Link and delete the reward.");
        StepRoomEventFrames(2);
        FailIf(
            maskEvent.BlocksGameplay || _player.CutsceneControlled ||
            maskEvent.CurrentCommandIndex != 2 ||
            Salesman().CurrentScriptAnimationSource != record.Animation0,
            "maskSalesmanScript did not restore animation $00 and input after its reward.");

        LoadValidationRoom(exteriorGroup, exteriorRoom);
        LoadValidationRoom(group, room);
        StepRoomEventFrames(1);
        PositionForTalk();
        FailIf(
            !_interactions.TryInteract(_player),
            "Completed room 2:e6 did not retain the Mask Salesman talk target.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x0b15, "completed-trade branch");
        FailIf(
            _dialogue.Position.Y != 96 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Room bit $20 did not select lower TX_0b15 or suppress another reward.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            maskEvent.BlocksGameplay || _player.CutsceneControlled ||
            maskEvent.CurrentCommandIndex != 2,
            "Completed Mask Salesman dialogue did not restore input immediately.");

        var warps = new WarpDatabase();
        OracleRoomData exterior = _world.LoadRoom(exteriorGroup, exteriorRoom);
        OracleRoomData interior = _world.LoadRoom(group, room);
        int[] entryPositions = Enumerable.Range(0, 8)
            .SelectMany(row => Enumerable.Range(0, 10)
                .Select(column => (row << 4) | column))
            .Where(position =>
            {
                byte tile = exterior.GetMetatile(PackedPosition(position));
                return warps.TryGetTileWarp(
                        exteriorGroup, exteriorRoom, position, tile,
                        out Warp candidate) &&
                    candidate.DestinationGroup == group &&
                    candidate.DestinationRoom == room;
            }).ToArray();
        bool hasEntry = warps.TryGetTileWarp(
            exteriorGroup, exteriorRoom, 0x52, 0xef, out Warp entry);
        bool hasExit = warps.TryGetEdgeWarp(
            group, room, Vector2I.Down,
            new Vector2(0x70, interior.Height + 2),
            new Vector2(interior.Width, interior.Height), out Warp exit);
        FailIf(
            !entryPositions.SequenceEqual(new[] { 0x51, 0x52 }) ||
            exterior.GetMetatile(PackedPosition(0x51)) != 0xee ||
            exterior.GetMetatile(PackedPosition(0x52)) != 0xef ||
            !hasEntry ||
            entry is not
            {
                SourcePosition: -1, EdgeMask: 0, SourceTransition: 4,
                DestinationGroup: 2, DestinationRoom: 0xe6,
                DestinationPosition: 0xf7, DestinationParameter: 9,
                DestinationTransition: 3
            } ||
            !hasExit ||
            exit is not
            {
                SourcePosition: -1, EdgeMask: 8, SourceTransition: 3,
                DestinationGroup: 0, DestinationRoom: 0x53,
                DestinationPosition: 0x52, DestinationParameter: 0,
                DestinationTransition: 14
            },
            "Rooms 0:53/2:e6 did not retain Rafton's two-tile tree entry " +
            $"and right-half bottom exit (positions=" +
            $"{string.Join(',', entryPositions.Select(value => value.ToString("x2")))}, " +
            $"tiles=${exterior.GetMetatile(PackedPosition(0x51)):x2}/" +
            $"${exterior.GetMetatile(PackedPosition(0x52)):x2}, " +
            $"entry={entry}, exit={exit}).");

        LoadValidationRoom(exteriorGroup, exteriorRoom);
        _player.WarpTo(PackedPosition(0x52));
        FailIf(
            !CheckTileWarp(_player) ||
            _activeGroup != group || _currentRoom.Id != room ||
            !IsTransitioning ||
            _player.Position != new Vector2(0x70, interior.Height),
            "Room 0:53/$52 did not begin the source transition-4 entry into 2:e6.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        FailIf(
            !IsTransitioning ||
            _player.Position != new Vector2(0x70, interior.Height - WarpEnterFrames),
            "Room 2:e6 entry did not complete its 28-update upward walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        FailIf(IsTransitioning, "Room 2:e6 entry fade did not finish on update 32.");

        _player.WarpTo(new Vector2(0x70, interior.Height + 2));
        _player.Face(Vector2I.Down);
        CheckRoomExit(_player);
        FailIf(
            !IsTransitioning || _activeGroup != group || _currentRoom.Id != room,
            "Room 2:e6's right-half bottom edge did not begin source transition 3.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        FailIf(
            _activeGroup != exteriorGroup ||
            _currentRoom.Id != exteriorRoom || !IsTransitioning,
            "Room 2:e6 did not load exterior 0:53 after its 16-update exit walk.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        FailIf(
            IsTransitioning ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x62,
            "Room 2:e6's transition-$0e exit did not step below exterior 0:53/$52.");

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "maskSalesmanScript").ToArray();
        string[] requiredOpcodes =
        [
            "setcollisionradii", "makeabuttonsensitive",
            "jumpifroomflagset", "jumpiftradeitemeq",
            "jumpiftextoptioneq", "giveitem"
        ];
        FailIf(
            commandStarts.Any(entry => entry.Source.SourceLine <= 0) ||
            requiredOpcodes.Any(opcode =>
                !commandStarts.Any(entry => entry.Source.Opcode == opcode)),
            "Mask Salesman typed trace lost source lines or a required script opcode.");

        _saveData.WriteWramBytes(0xc688, inventorySnapshot);
        _saveData.CommitInventoryChange();
        reloadInventory.Invoke(_inventory, null);
        foreach (byte flag in new byte[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
        {
            _saveData.SetRoomFlag(
                group, room, flag, (originalRoomFlags & flag) != 0);
        }
        _roomEvents.CommandTraceSink = null;

        GD.Print("Validated room 2:e6 Mask Salesman $5c:$00: one-update typed " +
            "script initialization, strict $04/$06 talk geometry, alternating " +
            "TX_0b0d-$0b15/$0b45 hunger sequence, exact 15/30-update waits, " +
            "missing/No/Yes Tasty Meat paths, two-hand Doggie Mask reward, room " +
            "bit $20 re-entry, and the bidirectional 0:53 tree warp.");
    }
}
