using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom165Ralph()
    {
        // ralph.s:@initSubid12 deletes outside linked games or before room
        // $4:fc bit $80. ralphSubid12 faces Link before running TX_2a23.
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (bool towerCleared in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, towerCleared);
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter ralph = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: InteractionId.Ralph, SubId: 0x12 });
            bool present = linked && towerCleared;
            FailIf(ralph.Active != present || ralph.Visible != present ||
                ralph.Record is not { TextId: 0x2a23, CanFace: true } ||
                ralph.Position != new Vector2(0x58, 0x38),
                "Room 1:65 Ralph $37:$12 lost his linked/$4:fc gate, TX_2a23 or placement.");
            if (!present)
            {
                FailIf(ralph.CanTalkTo(_player) || _entities.BlocksLink(ralph.Position),
                    "Room 1:65 Ralph $37:$12 stayed solid or talkable after state-zero deletion.");
                continue;
            }

            ApproachLibraryActor(ralph, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(ralph.Message) ||
                    !ralph.Message.Contains("Twinrova", StringComparison.Ordinal),
                    $"Room 1:65 Ralph $37:$12 TX_2a23 failed on conversation {repeat}.");
                int frame = ralph.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(frame != ralph.CurrentAnimationFrame,
                    "Room 1:65 Ralph $37:$12 animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }

            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            Vector2I towardLink = ralph.FacingVector;
            Vector2I opposite = -towardLink;
            ralph.FaceToward(ralph.Position + (Vector2)opposite * 20);
            ralph.SetNativeAngle(opposite == Vector2I.Up ? 0 :
                opposite == Vector2I.Right ? 8 : opposite == Vector2I.Down ? 16 : 24);
            bool scriptSawUpdatedFacing = false;
            NpcRoomEntity adapter = _entities.EntityAdapters<NpcRoomEntity>()
                .Single(e => ReferenceEquals(e.Npc, ralph));
            adapter.TryQueueDialogue(() =>
            {
                scriptSawUpdatedFacing = ralph.FacingVector == towardLink;
                return true;
            });
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!scriptSawUpdatedFacing,
                "ralph.s:ralphSubid12 did not face Link before interactionRunScript.");

            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, false);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!ralph.Active,
                "Room 1:65 Ralph $37:$12 repeated his state-zero flag gate mid-visit.");
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n =>
                    n.Record is { Id: InteractionId.Ralph, SubId: 0x12 }).Active,
                "Room 1:65 Ralph $37:$12 ignored his $4:fc gate on re-entry.");
        }
        GD.Print("Validated room 1:65 Ralph $37:$12 linked visibility, source-facing order, reachable repeated TX_2a23 and re-entry.");
    }
}
