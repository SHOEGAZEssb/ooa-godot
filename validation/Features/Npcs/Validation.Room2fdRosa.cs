using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2fdRosa()
    {
        // rosa.s:$68:$01 loads rosa_subid01Script. Its linked check can
        // terminate the script, while the linked path runs generic TX_1c13.
        // The US group2MapfdObjectData places her at y=$28,x=$28.
        foreach (bool batched in new[] { false, true })
        foreach (byte essences in new byte[] { 0x00, 0x04, 0xff })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            if (_saveData.WriteWramByte(WramAddress.wEssencesObtained, essences))
                _saveData.CommitInventoryChange();
            LoadValidationRoom(2, 0xfd);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            NpcCharacter rosa = _entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Group: 2, Room: 0xfd, Id: InteractionId.Rosa, SubId: 0x01 });
            FailIf(!rosa.Active || !rosa.Visible ||
                rosa.Position != new Vector2(0x28, 0x28) || rosa.TextId != 0x1c13,
                $"Room 2:fd Rosa $68:$01 lost linked US placement or TX_1c13 at essences ${essences:x2}.");
            ApproachLibraryActor(rosa, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen ||
                    !_dialogue.CurrentMessage.Contains("Goron Dance", System.StringComparison.Ordinal) ||
                    !_dialogue.CurrentMessage.Contains("Subrosian", System.StringComparison.Ordinal) ||
                    !_dialogue.CurrentMessage.Contains("ways to go!", System.StringComparison.Ordinal),
                    $"Room 2:fd Rosa $68:$01 was unreachable or lost TX_1c13 on talk {repeat} with essences ${essences:x2}.");
                _dialogue.Close();
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen,
                "Room 2:fd Rosa failed to reopen before room-exit cancellation.");
            LoadValidationRoom(2, 0xfc);
            FailIf(_dialogue.IsOpen, "Leaving room 2:fd retained Rosa's dialogue.");
            LoadValidationRoom(2, 0xfd);
            FailIf(!_entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Group: 2, Room: 0xfd, Id: InteractionId.Rosa, SubId: 0x01 }).Active,
                "Linked Rosa $68:$01 disappeared after room 2:fd re-entry.");
        }
        foreach (byte essences in new byte[] { 0x00, 0x04, 0xff })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(false);
            if (_saveData.WriteWramByte(WramAddress.wEssencesObtained, essences))
                _saveData.CommitInventoryChange();
            LoadValidationRoom(2, 0xfd);
            NpcCharacter rosa = _entities.Entities<NpcCharacter>().Single(npc =>
                npc.Record is { Group: 2, Room: 0xfd, Id: InteractionId.Rosa, SubId: 0x01 });
            FailIf(rosa.Active || rosa.Visible,
                $"Unlinked Rosa $68:$01 survived stubScript with essences ${essences:x2}.");
        }
        GD.Print("Validated room 2:fd Rosa $68:$01 linked visibility, source text, real approach, repeated talks, cancellation and re-entry across essence states.");
    }
}
