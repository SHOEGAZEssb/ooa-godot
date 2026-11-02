
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void SetNpcProgressStage(bool linked, int stage)
    {
        _saveData.SetLinkedGame(linked);
        _saveData.WriteWramByte(AgesWramAddress.wEssencesObtained,
            (byte)(stage == 0 ? 0 : stage == 1 ? 0x02 : stage is 2 or 3 ? 0x08 : 0x40));
        _saveData.CommitInventoryChange();
        _saveData.SetGlobalFlag(AgesGlobalFlag.SavedNayru, stage == 3);
        _saveData.SetGlobalFlag(AgesGlobalFlag.SawTwinrovaBeforeEndgame, stage == 5);
        _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, stage == 7);
        if (stage == 6)
            _saveData.SetRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80);
    }

    private void RepeatNpcConversation(NpcCharacter actor, bool batched, string source)
    {
        ApproachLibraryActor(actor, batched);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != PlainWords(actor.Message),
                $"{source} was unreachable on conversation {repeat}.");
            int frame = actor.CurrentAnimationFrame;
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(actor.CurrentAnimationFrame != frame, $"{source} animated during active text.");
            _dialogue.Close();
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
        }
    }
}
