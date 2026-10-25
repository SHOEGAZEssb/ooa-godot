using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom1cbRosa()
    {
        // rosa.s:$68:$00 and rosa_subid00Script: linked only, no third
        // essence, room bit $40 selects TX_1c12 after the shovel sequence.
        TokayInteractionDatabase text = new();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            _saveData.SetRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40, false);
            _inventory.LoseTreasure(TreasureId.Shovel);
            LoadValidationRoom(1, 0xcb);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            RosaNpcRoomEntity rosa = _entities.EntityAdapters<RosaNpcRoomEntity>().Single();
            TokayAttachedVisualRoomEntity shovel = _entities.Entities<TokayAttachedVisualRoomEntity>().Single();
            RosaShovelEvent script = _roomEvents.Get<RosaShovelEvent>();
            FailIf(!rosa.Npc.Active || rosa.Npc.Position != new Vector2(0x38, 0x38) ||
                shovel.FollowParent || shovel.ParentOffset != new Vector2(0x48, 0x38) ||
                _saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40),
                "Room 1:cb Rosa $68:$00 lost source placement, stationary shovel, or initial room bit $40.");

            ApproachLibraryActor(rosa.Npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(script.Stage != RosaShovelStage.FirstText || !_dialogue.IsOpen ||
                !_player.CutsceneControlled,
                "Room 1:cb Rosa $68:$00 was unreachable or did not lock Link for TX_1c10.");
            FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(text.Text(0x1c10)),
                "Rosa's first shovel text was not TX_1c10.");
            _dialogue.Close();
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            FailIf(script.Stage != RosaShovelStage.MoveRight,
                "Rosa's source wait $1e did not hand off to SPEED_020 movement.");
            Vector2 start = rosa.Npc.Position;
            StepGameplayUpdates(47, Vector2.Zero, batched: batched);
            FailIf(rosa.Npc.Position != start + new Vector2(5, 0) ||
                _saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40),
                "Rosa's $30 movement lost its 47-update fixed-point position or set bit $40 early.");
            StepGameplayUpdates(22, Vector2.Zero, batched: batched);
            FailIf(!shovel.FollowParent || shovel.ParentOffset != new Vector2(9, 0),
                "Rosa's related shovel missed its first $14 wait and +$09 offset.");
            StepGameplayUpdates(20, Vector2.Zero, batched: batched);
            FailIf(shovel.ParentOffset != new Vector2(-9, 0) ||
                shovel.ZIndex != ObjectDrawPriority.FixedLowPriorityZIndex,
                "Rosa's shovel missed the second $14 wait, -$09 offset, or priority change.");
            StepGameplayUpdates(30, Vector2.Zero, batched: batched);
            FailIf(script.Stage != RosaShovelStage.SecondText,
                "Rosa's final $1e wait did not open TX_1c11.");
            FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(text.Text(0x1c11)),
                "Rosa's second shovel text was not TX_1c11.");
            _dialogue.Close();
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            GroundTreasurePickup reward = _entities.Entities<GroundTreasurePickup>().Single();
            InitializeGetItemStateForValidation();
            FailIf(script.Stage != RosaShovelStage.Reward ||
                reward.Record.TreasureObject != "TREASURE_OBJECT_SHOVEL_01" ||
                !reward.Held || !_inventory.HasTreasure(TreasureId.Shovel) ||
                _saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40),
                "Rosa's giveitem did not grant the source shovel before setting room bit $40.");
            _dialogue.Close();
            StepGameplayUpdates(33, Vector2.Zero, batched: batched);
            FailIf(script.HasState || _player.CutsceneControlled ||
                !_saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40),
                "Rosa's reward completion did not set room bit $40 and release Link.");

            ApproachLibraryActor(rosa.Npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(script.Stage != RosaShovelStage.DialogueOnly ||
                _entities.Entities<GroundTreasurePickup>().Count != 0,
                "Rosa's repeated talk incorrectly restarted the shovel reward.");
            FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(text.Text(0x1c12)),
                "Rosa's repeated talk was not TX_1c12.");
            LoadValidationRoom(1, 0xcc);
            FailIf(script.HasState, "Leaving room 1:cb retained Rosa's dialogue state.");
            LoadValidationRoom(1, 0xcb);
            FailIf(!_entities.EntityAdapters<RosaNpcRoomEntity>().Single().Npc.Active ||
                _entities.Entities<TokayAttachedVisualRoomEntity>().Count != 0 ||
                !_saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40),
                "Re-entering room 1:cb lost Rosa or recreated her claimed shovel.");

            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            LoadValidationRoom(1, 0xcb);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            rosa = _entities.EntityAdapters<RosaNpcRoomEntity>().Single();
            ApproachLibraryActor(rosa.Npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(_roomEvents.Get<RosaShovelEvent>().Stage != RosaShovelStage.FirstText,
                "Rosa's unclaimed shovel sequence could not begin before room-exit cancellation.");
            LoadValidationRoom(1, 0xcc);
            FailIf(_roomEvents.Get<RosaShovelEvent>().HasState ||
                _saveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40) ||
                _inventory.HasTreasure(TreasureId.Shovel),
                "Leaving Rosa during TX_1c10 retained control or awarded the shovel early.");
            LoadValidationRoom(1, 0xcb);
            FailIf(!_entities.EntityAdapters<RosaNpcRoomEntity>().Single().Npc.Active ||
                _entities.Entities<TokayAttachedVisualRoomEntity>().Count != 1,
                "Rosa's cancelled shovel sequence did not reset on room re-entry.");
        }

        foreach ((bool linked, byte essences) in new[] { (false, (byte)0), (true, (byte)0x04) })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            if (_saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, essences))
                _saveData.CommitInventoryChange();
            LoadValidationRoom(1, 0xcb);
            FailIf(_entities.EntityAdapters<RosaNpcRoomEntity>().Single().Npc.Active ||
                _entities.Entities<TokayAttachedVisualRoomEntity>().Count != 0,
                $"Rosa $68:$00 survived source deletion for linked={linked}, essences=${essences:x2}.");
        }
        GD.Print("Validated linked Rosa $68:$00 through real approach, source waits, shovel reward, room bit $40, repeat talk and re-entry.");
    }
}
