using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2e8DumbbellMan()
    {
        DumbbellManEvent script = _roomEvents.Get<DumbbellManEvent>();
        DumbbellManEventRecord record = script.Database.Record;
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        _saveData.SetRoomFlag(2, 0xe8, 0x20, false);

        void SetTrade(int value, bool obtained = true)
        {
            const int address = 0xc6a2; // TREASURE_TRADEITEM $41 obtained bit.
            _saveData.WriteWramByte(address, (byte)(
                (_saveData.ReadWramByte(address) & ~2) | (obtained ? 2 : 0)));
            _saveData.WriteWramByte(0xc6c0, (byte)value);
            _saveData.CommitInventoryChange();
            typeof(InventoryState).GetMethod("LoadFromSaveData",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_inventory, null);
        }

        DumbbellManCharacter Man() => _entities.Entities<DumbbellManCharacter>().Single();
        void ExpectText(int id)
        {
            string message = script.Database.Commands.OfType<CutsceneShowTextCommand>()
                .First(command => command.TextId == id).Message;
            FailIf(!_dialogue.IsOpen ||
                _dialogue.CurrentMessage != DialogueBox.PlainText(message),
                $"Room 2:e8 expected TX_{id:x4}, command {script.CurrentCommandIndex}.");
        }
        void Talk(int text = 0x0b1d)
        {
            _player.WarpTo(new Vector2(0x50, 0x24));
            _player.Face(Vector2I.Up);
            FailIf(!_interactions.TryInteract(_player),
                "Room 2:e8 $51:$00 is missing its normal A-button route.");
            StepRoomEventFrames(1);
            ExpectText(text);
            FailIf(!script.BlocksGameplay || !_player.CutsceneControlled,
                "dumbbellManScript failed to disable input before dialogue.");
        }
        void WaitAfterText()
        {
            _dialogue.Close();
            StepRoomEventFrames(1);
            FailIf(script.Counter != 30 || _dialogue.IsOpen,
                "Room 2:e8 wait 30 did not load its counter on the first update.");
            StepRoomEventFrames(29);
            FailIf(script.Counter != 1 || _dialogue.IsOpen || !script.BlocksGameplay,
                "Room 2:e8 wait 30 completed before its zero update.");
            StepRoomEventFrames(1);
        }
        void FinishPreamble(bool hasDumbbell)
        {
            WaitAfterText();
            ExpectText(0x0b20);
            WaitAfterText();
            if (hasDumbbell) ExpectText(0x0b1e);
            else FailIf(_dialogue.IsOpen || script.BlocksGameplay ||
                _player.CutsceneControlled || script.CurrentCommandIndex != 5,
                "Room 2:e8 missing Dumbbell failed to restore the idle loop.");
        }
        void ReachChoice()
        {
            Talk();
            FinishPreamble(true);
            foreach (int text in new[] { 0x0b20, 0x0b1f, 0x0b20, 0x0b20, 0x0b21 })
            {
                WaitAfterText();
                ExpectText(text);
            }
            FailIf(!_dialogue.ChoiceActive, "TX_0b21 omitted its Yes/No choices.");
        }

        SetTrade(0x05, obtained: false);
        LoadValidationRoom(2, 0xe8);
        DumbbellManCharacter man = Man();
        FailIf(man.Record is not { Id: 0x51, SubId: 0, SpriteName: "spr_gymnast_troy_scrub" } ||
            man.Position != new Vector2(0x50, 0x18) ||
            man.CurrentScriptAnimationSource != record.Animation0 ||
            man.CurrentAnimationTextureSize != new Vector2I(16, 24) ||
            man.CurrentAnimationOffset != new Vector2(-8, -16) ||
            man.CurrentAnimationOpaquePixels == 0 || man.AnimationRate != 0 ||
            !script.HasState || script.ButtonSensitive || script.CurrentCommandIndex != 2,
            $"Room 2:e8 lost $51:$00 placement, graphics, or state-0 update: " +
            $"position={man.Position}, size={man.CurrentAnimationTextureSize}, " +
            $"offset={man.CurrentAnimationOffset}, pixels={man.CurrentAnimationOpaquePixels}, " +
            $"command={script.CurrentCommandIndex}, button={script.ButtonSensitive}.");
        StepRoomEventFrames(1);
        FailIf(script.CurrentCommandIndex != 4 || script.ButtonSensitive,
            "Copied dumbbellManScript jump ++ must yield before initcollisions.");
        StepRoomEventFrames(1);
        FailIf(!script.ButtonSensitive || script.CurrentCommandIndex != 5,
            "Room 2:e8 initcollisions did not register the idle A-button target.");
        StepRoomEventFrames(14);
        FailIf(man.CurrentAnimationFrame != 0,
            "Room 2:e8 swaying animation advanced before its 18th native update.");
        StepRoomEventFrames(1);
        FailIf(man.CurrentAnimationFrame != 1,
            "Room 2:e8 swaying animation missed its 18-update frame boundary.");
        _player.WarpTo(new Vector2(0x50, 0x27));
        _player.Face(Vector2I.Up);
        FailIf(_entities.FindTalkTarget(_player) != man,
            "Room 2:e8 rejected the last point within collision radius Y=$06.");
        _player.WarpTo(new Vector2(0x50, 0x28));
        FailIf(_entities.FindTalkTarget(_player) is not null,
            "Room 2:e8 accepted a talk point outside collision radius Y=$06.");
        _player.WarpTo(man.Position + new Vector2(0, 1));
        StepRoomEventFrames(1);
        FailIf(_player.Position == man.Position + new Vector2(0, 1),
            "interactionAnimateAsNpc did not push Link away from $51:$00.");

        Talk();
        int commandDuringText = script.CurrentCommandIndex;
        int animationFrame = man.CurrentAnimationFrame;
        StepRoomEventFrames(18);
        FailIf(man.CurrentAnimationFrame == animationFrame ||
            script.CurrentCommandIndex != commandDuringText,
            "Dumbbell Man must animate during dialogue while his script waits.");
        FinishPreamble(false); // Value $05 alone is insufficient without the obtained bit.
        SetTrade(0x04);
        Talk();
        FinishPreamble(false);

        SetTrade(0x05);
        ReachChoice();
        _dialogue.SubmitChoiceForValidation(1);
        StepRoomEventFrames(31);
        ExpectText(0x0b20);
        FailIf(_inventory.TradeItem != 0x05 || _saveData.HasRoomFlag(2, 0xe8, 0x20),
            "Declining the Dumbbell trade changed item $05 or room bit $20.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(script.BlocksGameplay || script.CurrentCommandIndex != 5,
            "Declined Dumbbell trade did not release input at its copied loop jump.");

        ReachChoice();
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(31);
        ExpectText(0x0b22);
        WaitAfterText();
        ExpectText(0x0b20);
        WaitAfterText();
        ExpectText(0x0b23);
        WaitAfterText();
        FailIf(Man().CurrentScriptAnimationSource != record.Animation1 ||
            _inventory.TradeItem != 0x05 || script.CurrentCommandIndex != 38,
            "Dumbbell Man must select lifting animation $01 one update before giveitem.");
        _sound.ClearPlayRequestAudit();
        StepRoomEventFrames(1);
        GroundTreasurePickup reward = _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(reward.Record.TreasureObject != "TREASURE_OBJECT_TRADEITEM_06" ||
            reward.Record.GrabMode != 2 || !reward.Held ||
            !_player.IsHoldingItemTwoHands || _inventory.TradeItem != 0x06 ||
            !_saveData.HasRoomFlag(2, 0xe8, 0x20) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(_treasures.GetObject(record.RewardObject).Message) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2,
            "Room 2:e8 failed Cheesy Mustache $41:$06 presentation, audio, inventory, or room flag.");
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        StepRoomEventFrames(1);
        ExpectText(0x0b24);
        FailIf(_player.IsHoldingItemTwoHands || _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Closing TX_0060 did not release Link and delete the held Mustache.");
        WaitAfterText();
        FailIf(script.BlocksGameplay || script.CurrentCommandIndex != 5 ||
            Man().CurrentScriptAnimationSource != record.Animation1,
            "Completed trade failed to restore input while retaining lifting animation $01.");

        FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) ||
            restored is null || !restored.HasRoomFlag(2, 0xe8, 0x20) ||
            restored.ReadWramByte(0xc6c0) != 0x06,
            "Room 2:e8 trade state did not survive save serialization.");
        LoadValidationRoom(0, 0x56);
        SetTrade(0x05); // The room flag takes precedence even if Link holds a Dumbbell again.
        LoadValidationRoom(2, 0xe8);
        FailIf(Man().CurrentScriptAnimationSource != record.Animation1 ||
            script.CurrentCommandIndex != 4,
            "Room 2:e8 re-entry failed its room-bit-$20 lifting branch.");
        StepRoomEventFrames(1);
        Talk(0x0b24);
        WaitAfterText();
        FailIf(_inventory.TradeItem != 0x05 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "Completed Dumbbell Man awarded a second Mustache.");
        ValidateInteractiveInfiniteScriptCancellation(script, Man(), "Dumbbell Man $51:$00");

        // Cancelling the actual introductory dialogue cannot perform the trade.
        _saveData.SetRoomFlag(2, 0xe8, 0x20, false);
        LoadValidationRoom(2, 0xe8);
        StepRoomEventFrames(2);
        Talk();
        _dialogue.Close();
        LoadValidationRoom(0, 0x56);
        FailIf(script.HasState || script.ButtonSensitive || script.BlocksGameplay ||
            _player.CutsceneControlled || _inventory.TradeItem != 0x05 ||
            _saveData.HasRoomFlag(2, 0xe8, 0x20),
            "Leaving room 2:e8 retained the script or committed an unfinished trade.");

        CutsceneCommandTraceEntry[] starts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started &&
            entry.Source.Script == "dumbbellManScript").ToArray();
        FailIf(starts.Any(entry => entry.Source.SourceLine <= 0) ||
            Enumerable.Range(0, 43).Any(index => !starts.Any(entry => entry.Source.CommandIndex == index)),
            "Dumbbell Man regression did not execute every imported command with source identity.");
        _roomEvents.CommandTraceSink = null;

        // Compare native animation and wait completion across host frame sizes.
        (int Command, int Counter, int Frame, bool Dialogue, string Text, bool Input) RunCadence(bool batched)
        {
            LoadValidationRoom(2, 0xe8);
            StepRoomEventFrames(2);
            Talk();
            _dialogue.Close();
            var scheduler = new ApplicationFixedUpdateScheduler();
            void Tick() => StepRoomEventFrames(1);
            if (batched) scheduler.Advance(32.0 / 60.0, Tick);
            else for (int tick = 0; tick < 32; tick++) scheduler.Advance(1.0 / 60.0, Tick);
            var result = (script.CurrentCommandIndex, script.Counter, Man().CurrentAnimationFrame,
                _dialogue.IsOpen, _dialogue.CurrentMessage, script.BlocksGameplay);
            _dialogue.Close();
            return result;
        }
        FailIf(RunCadence(false) != RunCadence(true),
            "Room 2:e8 script wait/dialogue/animation diverged in a batched host frame.");
        GD.Print("Validated room 2:e8 Dumbbell Man $51:$00: copied-script cadence, " +
            "source animation and collision, all TX_0b1d-$0b24 branches, exact waits, " +
            "Mustache reward, persistent lifting, re-entry and cancellation.");
    }
}
