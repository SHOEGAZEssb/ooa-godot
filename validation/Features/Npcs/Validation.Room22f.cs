using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom22fPostman()
    {
        const double frame = 1.0 / 60.0;
        var root = new Node { Name = "Room22fPostmanValidation" };
        var worldRoot = new Node { Name = "World" };
        var interfaceLayer = new Node { Name = "Interface" };
        var roomView = new RoomView { Name = "RoomView" };
        var dialogue = new DialogueBox { Name = "Dialogue" };
        root.AddChild(worldRoot);
        root.AddChild(interfaceLayer);
        root.AddChild(roomView);
        root.AddChild(dialogue);
        AddChild(root);

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        long tick = 0;
        var rooms = new RoomSession(
            2, 0x2f, () => tick, () => tick = 0, save);
        var treasures = new TreasureDatabase();
        var inventory = new InventoryState(
            treasures, save, () => rooms.CurrentDungeonIndex);
        var sounds = new List<int>();
        using var fixture = RoomEntityValidationFixture.ForRoot(
            worldRoot, new()
            {
                SaveData = save,
                Inventory = inventory,
                Treasures = treasures,
                Rooms = rooms
            });
        RoomEntityManager manager = fixture.Manager;
        manager.SoundRequested += sounds.Add;
        var interactions = new InteractionController(
            rooms, manager, new SignDatabase(), new ChestDatabase(),
            treasures, dialogue, worldRoot, roomView,
            static position => position, () => tick, inventory,
            interfaceLayer, sounds.Add);
        var scriptDatabase = new NpcInteractionScriptDatabase();
        string Text(int textId) => scriptDatabase.Postman
            .OfType<CutsceneShowTextCommand>()
            .Single(command => command.TextId == textId)
            .Message;

        manager.LoadRoom(2, rooms.CurrentRoom);
        PostmanCharacter postman =
            manager.Entities<PostmanCharacter>().Single();
        PostmanScriptHost host =
            interactions.NpcScriptsForValidation.Postman;
        var trace = new ValidationCutsceneTrace();
        interactions.NpcScriptsForValidation.TraceSink = trace;

        void Step()
        {
            manager.Update(frame, _player);
            interactions.Update(frame, _player);
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
                Step();
                if (predicate())
                    return;
            }
            FailIf(true, failure);
        }

        void RequireDialogue(int textId, string failure) =>
            FailIf(
                !dialogue.IsOpen ||
                DialogueBox.PlainText(dialogue.CurrentMessage) !=
                    DialogueBox.PlainText(Text(textId)),
                failure);

        FailIf(
            postman.Record is not
            {
                Group: 2,
                Room: 0x2f,
                Id: 0x55,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x0b03,
                DefaultAnimation: 2,
                CanFace: true,
                Implementation:
                    NpcImplementationClassification.SpecializedNative
            } ||
            postman.Position != new Vector2(0x18, 0x18) ||
            !postman.Active ||
            postman.CurrentAnimationOpaquePixels == 0,
            "Room 2:2f did not load specialized INTERAC_POSTMAN $55:$00 " +
            "at $18/$18 with animation $02 and TX_0b03.");

        _player.WarpTo(postman.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        manager.Update(frame, _player);
        FailIf(
            !manager.BlocksLink(postman.Position) ||
            !interactions.TryInteract(_player),
            "Room 2:2f's Postman was not solid or A-button sensitive.");
        RequireDialogue(
            0x0b03,
            "The Postman did not open TX_0b03 without the Poe Clock.");

        // No Poe Clock: the trade test skips directly to enableinput.
        dialogue.Close();
        AdvanceUntil(
            () => host.CurrentCommandIndex == 2 &&
                !host.InputDisabled &&
                !dialogue.IsOpen,
            40,
            "The no-Poe-Clock branch did not return to the A-button loop.");
        FailIf(
            inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) ||
            save.HasRoomFlag(2, 0x2f, OracleSaveData.RoomFlagItem),
            "The no-Poe-Clock branch granted Stationery or set room flag $20.");

        // Poe Clock, declined: prompt with TX_0b04, then show TX_0b06.
        inventory.GiveTreasure(TreasureDatabase.TreasureTradeItem, 0);
        FailIf(
            !interactions.TryInteract(_player),
            "The Postman did not accept a second A-button press.");
        RequireDialogue(
            0x0b03,
            "The Poe Clock branch did not begin with TX_0b03.");
        dialogue.Close();
        AdvanceUntil(
            () => dialogue.IsOpen,
            40,
            "The Poe Clock branch did not reach its trade prompt.");
        RequireDialogue(
            0x0b04,
            "The Poe Clock branch did not open choice TX_0b04.");
        FailIf(
            !dialogue.ChoiceActive,
            "TX_0b04 was not imported as a choice textbox.");
        dialogue.SubmitChoiceForValidation(1);
        AdvanceUntil(
            () => dialogue.IsOpen,
            40,
            "Declining the Poe Clock did not open TX_0b06.");
        RequireDialogue(
            0x0b06,
            "Declining the Poe Clock selected the wrong Postman text.");
        dialogue.Close();
        AdvanceUntil(
            () => host.CurrentCommandIndex == 2 &&
                !host.InputDisabled,
            4,
            "The declined Postman branch did not re-enable input.");
        FailIf(
            inventory.TradeItem != 0 ||
            save.HasRoomFlag(2, 0x2f, OracleSaveData.RoomFlagItem),
            "Declining the Postman consumed the Poe Clock or set room flag $20.");

        // Accept the trade and preserve the source wait/movement boundaries.
        FailIf(
            !interactions.TryInteract(_player),
            "The Postman did not accept the successful trade interaction.");
        RequireDialogue(
            0x0b03,
            "The successful Postman branch did not begin with TX_0b03.");
        dialogue.Close();
        AdvanceUntil(
            () => dialogue.IsOpen,
            40,
            "The successful Postman branch did not reach TX_0b04.");
        RequireDialogue(
            0x0b04,
            "The successful Postman branch selected the wrong choice text.");
        dialogue.SubmitChoiceForValidation(0);
        AdvanceUntil(
            () => dialogue.IsOpen,
            40,
            "Accepting the Poe Clock did not open TX_0b05.");
        RequireDialogue(
            0x0b05,
            "Accepting the Poe Clock selected the wrong Postman text.");
        dialogue.Close();

        AdvanceUntil(
            () => postman.Leaving,
            40,
            "postmanScript did not set Interaction.var3f after its 30-update wait.");
        FailIf(
            postman.Position != new Vector2(0x18, 0x18) ||
            postman.MovementCounterActive,
            "The Postman moved before setspeed/moveright initialized counter2.");
        Step();
        FailIf(
            host.CurrentCommandIndex != 18 ||
            postman.MovementCounterActive,
            "postmanScript did not yield once after SPEED_200.");
        Step();
        FailIf(
            host.CurrentCommandIndex != 18 ||
            !postman.MovementCounterActive ||
            postman.Position != new Vector2(0x18, 0x18) ||
            postman.CurrentScriptAnimationSource !=
                postman.Record.RightAnimation,
            "Postman moveright did not initialize counter $1d and " +
            "animation $01 without moving on its setup update.");

        AdvanceUntil(
            () => host.CurrentCommandIndex == 19,
            32,
            "Postman moveright did not finish its $1d counter.");
        FailIf(
            postman.Position != new Vector2(0x50, 0x18) ||
            !postman.MovementCounterActive,
            "Postman moveright did not apply 28 SPEED_200 movements.");
        Step();
        FailIf(
            host.CurrentCommandIndex != 19 ||
            postman.Position != new Vector2(0x50, 0x18) ||
            postman.CurrentScriptAnimationSource !=
                postman.Record.DownAnimation,
            "Postman movedown did not select animation $02 on its setup update.");
        AdvanceUntil(
            () => host.CurrentCommandIndex == 20,
            60,
            "Postman movedown did not finish its $39 counter.");
        FailIf(
            postman.Position != new Vector2(0x50, 0x88) ||
            postman.MovementCounterActive,
            "Postman movedown did not apply 56 SPEED_200 movements and " +
            "clear counter2 at $50/$88.");

        AdvanceUntil(
            () => interactions.PostmanTreasureForValidation is not null,
            32,
            "postmanScript did not grant Stationery after its final 30-update wait.");
        GroundTreasurePickup heldStationery =
            interactions.PostmanTreasureForValidation!;
        TreasureObjectRecord stationery =
            treasures.GetObject("TREASURE_OBJECT_TRADEITEM_01");
        FailIf(
            !inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) ||
            inventory.TradeItem != 1 ||
            !save.HasRoomFlag(2, 0x2f, OracleSaveData.RoomFlagItem) ||
            postman.Active ||
            !heldStationery.Held ||
            heldStationery.Record is not
            {
                SpawnMode: 0,
                GrabMode: 2,
                InventoryWrite: GroundTreasureInventoryWrite.TreasureObject,
                RoomFlagTiming: GroundTreasureRoomFlagTiming.OnActivation,
                SoundOrder: GroundTreasureSoundOrder.BehaviourThenGrab,
                DialogueTiming: GroundTreasureDialogueTiming.AfterGrab,
                CompletionOwner: GroundTreasureCompletionOwner.Caller
            } ||
            heldStationery.Record.TreasureObject !=
                "TREASURE_OBJECT_TRADEITEM_01" ||
            heldStationery.Position != _player.Position + Vector2.Up * 14 ||
            !_player.IsHoldingItemTwoHands ||
            DialogueBox.PlainText(dialogue.CurrentMessage) !=
                DialogueBox.PlainText(stationery.Message),
            "The Postman did not exchange the Poe Clock for held Stationery, " +
            "set room flag $20, hide himself, and open TX_005b: " +
            $"owned={inventory.HasTreasure(TreasureDatabase.TreasureTradeItem)}, " +
            $"trade={inventory.TradeItem:x2}, " +
            $"flag={save.HasRoomFlag(2, 0x2f, OracleSaveData.RoomFlagItem)}, " +
            $"active={postman.Active}, held={heldStationery.Held}, " +
            $"object={heldStationery.Record.TreasureObject}, " +
            $"text={heldStationery.Record.CompletionTextId:x4}, " +
            $"position={heldStationery.Position}, player={_player.Position}, " +
            $"holding={_player.IsHoldingItemTwoHands}, " +
            $"dialogue={DialogueBox.PlainText(dialogue.CurrentMessage)}.");

        dialogue.Close();
        Step();
        manager.Update(frame, _player);
        FailIf(
            interactions.PostmanTreasureForValidation is not null ||
            manager.Entities<GroundTreasurePickup>().Count != 0 ||
            _player.IsHoldingItemTwoHands ||
            host.HasState ||
            host.InputDisabled,
            "postmanScript did not remove the held Stationery, enable input, " +
            "and end after TX_005b.");

        int[] tradeTargets = trace.Entries
            .Where(entry =>
                entry.Source.Script == "postmanScript" &&
                entry.Source.CommandIndex == 6 &&
                entry.Phase == CutsceneCommandTracePhase.Completed)
            .Select(entry => entry.NextCommandIndex)
            .ToArray();
        int[] choiceStarts = trace.Entries
            .Where(entry =>
                entry.Source.Script == "postmanScript" &&
                entry.Source.CommandIndex is 11 or 14 &&
                entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => entry.Source.CommandIndex)
            .ToArray();
        int rightUpdates = trace.Entries.Count(entry =>
            entry.Source.Script == "postmanScript" &&
            entry.Source.CommandIndex == 18 &&
            entry.Phase == CutsceneCommandTracePhase.Updated);
        int downUpdates = trace.Entries.Count(entry =>
            entry.Source.Script == "postmanScript" &&
            entry.Source.CommandIndex == 19 &&
            entry.Phase == CutsceneCommandTracePhase.Updated);
        int waitCompletions = trace.Entries.Count(entry =>
            entry.Source.Script == "postmanScript" &&
            entry.Source.CommandIndex is 5 or 9 or 15 or 20 &&
            entry.Phase == CutsceneCommandTracePhase.Completed &&
            entry.Counter == 0);
        FailIf(
            !tradeTargets.SequenceEqual([7, 8, 8]) ||
            !choiceStarts.SequenceEqual([11, 14]) ||
            rightUpdates != 0x1d ||
            downUpdates != 0x39 ||
            waitCompletions != 7,
            "postmanScript's typed trace lost its no-clock/decline/accept " +
            "branches, seven 30-update waits, or $1d/$39 move counters: " +
            $"trade=[{string.Join(",", tradeTargets)}], " +
            $"choice=[{string.Join(",", choiceStarts)}], " +
            $"right={rightUpdates}, down={downUpdates}, waits={waitCompletions}.");

        interactions.NpcScriptsForValidation.TraceSink = null;
        rooms.Load(2, 0x2f);
        manager.LoadRoom(2, rooms.CurrentRoom);
        PostmanCharacter completedPostman =
            manager.Entities<PostmanCharacter>().Single();
        _player.WarpTo(completedPostman.Position + Vector2.Down * 12);
        _player.Face(Vector2I.Up);
        FailIf(
            completedPostman.Active ||
            interactions.TryInteract(_player),
            "Room flag $20 did not keep the completed Postman hidden and " +
            "non-interactive on re-entry.");

        manager.Clear();
        RemoveChild(root);
        root.QueueFree();
        GD.Print(
            "Validated room 2:2f Postman TX_0b03-$0b06 branches, Poe Clock " +
            "choice, SPEED_200 $1d/$39 departure, Stationery grant, room " +
            "flag $20 completion, and re-entry visibility.");
    }
}
