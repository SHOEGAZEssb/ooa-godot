
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoom165Woman()
    {
        // femaleVillager.s:@initSubid04 uses miscMan2.s:@data1's single
        // subid-$04 state ($01) and script-table entry TX_1501 in state zero.
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 8))
        {
            if (stage == 6 && !linked)
                continue;
            ReinitializeGameplayForValidation();
            SetNpcProgressStage(linked, stage);
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var woman = _entities.Entities<NpcCharacter>().Single(n =>
                n.Record.Id == AgesInteractionId.FemaleVillager && n.Record.SubId == 4);
            bool present = stage == 1;
            FailIf(AgesNpcStoryState.GetGameProgress2(_saveData) != stage ||
                woman.Active != present || woman.Visible != present ||
                woman.Position != new Vector2(0x28, 0x38) ||
                woman.Record is not { Palette: 2, DefaultAnimation: 2 },
                $"Room 1:65 $3b:$04 state ${stage:x2} lost source placement, pose or @data1 gate.");
            if (!present)
            {
                FailIf(woman.CanTalkTo(_player) || _entities.BlocksLink(woman.Position),
                    $"Room 1:65 $3b:$04 remained interactive outside state $01 (state ${stage:x2}).");
                continue;
            }
            FailIf(woman.TextId != 0x1501 ||
                !woman.Message.Contains("My husband was", StringComparison.Ordinal) ||
                woman.ObjectCollisionBounds.Size != new Vector2(12, 12),
                "Room 1:65 $3b:$04 lost source TX_1501 or solid $06/$06 radii.");
            RepeatNpcConversation(woman, batched, "Room 1:65 $3b:$04 TX_1501");
            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0x08);
            _saveData.CommitInventoryChange();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!woman.Active || woman.TextId != 0x1501,
                "Room 1:65 $3b:$04 repeated its state-zero deletion gate during the visit.");
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3b).Active,
                "Room 1:65 $3b:$04 did not delete on state $02 re-entry.");
            _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained, 0x02);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(1, 0x65);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x3b).Active,
                "Room 1:65 $3b:$04 did not return on state $01 re-entry.");
        }
        GD.Print("Validated room 1:65 $3b:$04's sole progress state $01, TX_1501, room geometry, repeated dialogue and re-entry.");
    }
}
