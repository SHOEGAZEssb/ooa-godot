using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronHints()
    {
        foreach(int room in new[]{0xfd,0xff})
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(2,room); StepGameplayUpdates(4,Vector2.Zero);
            var cave=_roomEvents.Get<GoronCaveEvent>();
            var hint=cave.Actors.Single(a=>a.Actor.Record.SubId==0x10);
            bool past=(_rooms.CurrentRoom.TilesetFlags&0x80)!=0;
            int[] treasures=[0x5b,0x5e,0x5c,0x5d,0x45,0x59,0x44];
            foreach(int treasure in treasures.Prepend(-1))
            {
                if(treasure>=0) _inventory.GiveTreasure(treasure,0);
                ApproachGoronFromFloor(hint); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
                for(int i=0;i<8&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
                string message=_dialogue.CurrentMessage;
                FailIf(!message.StartsWith("I can see your",StringComparison.Ordinal)||!message.Contains(past?"Oh, my!!!":"Amazing!",StringComparison.Ordinal)||message.Contains("\\call",StringComparison.Ordinal),
                    $"Goron hint 2:{room:x2} did not resolve its clairvoyance preamble: {message}.");
                _dialogue.Close(); StepGameplayUpdates(35,Vector2.Zero);
            }
        }
        ReinitializeGameplayForValidation();
        LoadValidationRoom(2,0xf6); StepGameplayUpdates(4,Vector2.Zero);
        FailIf(_entities.Entities<NpcCharacter>().Any(n=>n.Record is {Id:InteractionId.Goron,SubId:0x0f}&&n.Active),
            "Biggoron secret NPC appeared in an unlinked game.");
        _saveData.SetLinkedGame(true);
        LoadValidationRoom(2,0xf6); StepGameplayUpdates(4,Vector2.Zero);
        var goron=_entities.Entities<NpcCharacter>().Single(n=>n.Record is {Id:InteractionId.Goron,SubId:0x0f});
        ApproachGoronActorFromFloor(goron); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(130,0);
        FailIf(!_saveData.HasGlobalFlag(GlobalFlag.BeganBiggoronSecret)||_saveData.ReadWramByte(WramAddress.wShortSecretIndex)!=0x28,
            "Biggoron secret did not write GLOBALFLAG $58 and short-secret index $28.");
        ApproachGoronActorFromFloor(goron); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<8&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage.Contains("\\secret1",StringComparison.Ordinal),
            "Biggoron secret could not be repeated with resolved symbols.");
        AdvanceGoronDialogue(80);
    }
}
