using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom165Ambi()
    {
        // ambi.s:@initSubid0a reads the linked-game byte and the past
        // Black Tower room $4:fc bit $80 once; ambi_runSubid0a faces Link
        // before running the generic TX_131a script.
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (bool towerCleared in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, towerCleared);
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter ambi = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record is { Id: AgesInteractionId.Ambi, SubId: 0x0a });
            bool present = linked && towerCleared;
            FailIf(ambi.Active != present || ambi.Visible != present ||
                ambi.Record is not { TextId: 0x131a, CanFace: true } ||
                ambi.Position != new Vector2(0x38, 0x38),
                $"Room 1:65 Ambi $4d:$0a lost its linked/$4:fc gate, TX_131a or placement.");
            if (!present)
            {
                FailIf(ambi.CanTalkTo(_player) || _entities.BlocksLink(ambi.Position),
                    "Room 1:65 Ambi $4d:$0a stayed solid or talkable after state-zero deletion.");
                continue;
            }

            ApproachLibraryActor(ambi, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(ambi.Message) ||
                    !ambi.Message.Contains("Princess Zelda", StringComparison.Ordinal),
                    $"Room 1:65 Ambi $4d:$0a TX_131a failed on conversation {repeat}.");
                int frame = ambi.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(frame != ambi.CurrentAnimationFrame,
                    "Room 1:65 Ambi $4d:$0a animated during active text.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }

            // Keep Link at the reached, collision-valid talk point. Force a
            // different pose after the native $1d cooldown has expired; the
            // queued script probe must observe this update's new facing.
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            Vector2I towardLink = ambi.FacingVector;
            Vector2I opposite = -towardLink;
            ambi.FaceToward(ambi.Position + (Vector2)opposite * 20);
            ambi.SetNativeAngle(opposite == Vector2I.Up ? 0 :
                opposite == Vector2I.Right ? 8 : opposite == Vector2I.Down ? 16 : 24);
            bool scriptSawUpdatedFacing = false;
            NpcRoomEntity adapter = _entities.EntityAdapters<NpcRoomEntity>()
                .Single(e => ReferenceEquals(e.Npc, ambi));
            adapter.TryQueueDialogue(() =>
            {
                scriptSawUpdatedFacing = ambi.FacingVector == towardLink;
                return true;
            });
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(!scriptSawUpdatedFacing,
                "ambi.s:ambi_runSubid0a did not face Link before interactionRunScript.");

            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80, false);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!ambi.Active,
                "Room 1:65 Ambi $4d:$0a repeated its state-zero flag gate mid-visit.");
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n =>
                    n.Record is { Id: AgesInteractionId.Ambi, SubId: 0x0a }).Active,
                "Room 1:65 Ambi $4d:$0a ignored its $4:fc gate on re-entry.");
        }
        GD.Print("Validated room 1:65 Ambi $4d:$0a linked visibility, source-facing order, reachable repeated TX_131a and re-entry.");
    }
}
