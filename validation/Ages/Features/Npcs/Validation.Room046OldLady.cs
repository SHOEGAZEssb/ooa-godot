using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom046OldLady()
    {
        // oldLady.s:@initSubid2 compares her subid $02 against
        // getGameProgress_1 only in state zero. The script is TX_1809.
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 6))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            SetSharedNpcProgress(stage);
            LoadValidationRoom(0, 0x46);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var actors = _entities.Entities<NpcCharacter>().ToArray();
            FailIf(actors.Length != 3 ||
                actors[0].Record is not { Id: AgesInteractionId.MiscMan, SubId: 2 } ||
                actors[1].Record is not { Id: AgesInteractionId.MiscMan, SubId: 5 } ||
                actors[2].Record is not { Id: AgesInteractionId.OldLady, SubId: 2 },
                "Room 0:46 lost source $41:$02, $41:$05, $3d:$02 order.");
            var lady = actors[2];
            bool present = stage == 2;
            FailIf(NpcStoryState.GetGameProgress1(_saveData) != stage ||
                lady.Active != present || lady.Visible != present ||
                lady.Position != new Vector2(0x68, 0x38) ||
                lady.Record is not { TextId: 0x1809, Palette: 3, DefaultAnimation: 2 },
                $"Room 0:46 $3d:$02 state ${stage:x2} lost its source placement, graphics or progress gate.");
            if (!present)
            {
                FailIf(lady.CanTalkTo(_player) || _entities.BlocksLink(lady.Position),
                    $"Room 0:46 $3d:$02 remained interactive outside state $02 (state ${stage:x2}).");
                continue;
            }
            FailIf(!lady.Message.Contains("bad luck", StringComparison.Ordinal) ||
                lady.ObjectCollisionBounds.Size != new Vector2(12, 12),
                "Room 0:46 $3d:$02 lost source TX_1809 or solid $06/$06 radii.");
            ApproachLibraryActor(lady, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    PlainWords(_dialogue.CurrentMessage) != PlainWords(lady.Message),
                    $"Room 0:46 $3d:$02 TX_1809 was unreachable on conversation {repeat}.");
                int frame = lady.CurrentAnimationFrame;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(lady.CurrentAnimationFrame != frame,
                    "Room 0:46 $3d:$02 animated while TX_1809 was active.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            SetSharedNpcProgress(3);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!lady.Active || lady.TextId != 0x1809,
                "Room 0:46 $3d:$02 repeated its state-zero deletion gate during the visit.");
            LoadValidationRoom(0, 0x46);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3d).Active,
                "Room 0:46 $3d:$02 did not delete on state $03 re-entry.");
            SetSharedNpcProgress(2);
            LoadValidationRoom(0, 0x46);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3d).Active,
                "Room 0:46 $3d:$02 did not return on state $02 re-entry.");
        }
        GD.Print("Validated room 0:46 $3d:$02 across getGameProgress_1 states $00-$05, both game modes and update batching, with reachable repeat TX_1809 and state-zero lifetime.");
    }
}
