using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTroySecret()
    {
        LoadValidationRoom(5,0xd8); StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        FailIf(cave.Actors.Single(a=>a.Actor.Record.Id==AgesInteractionId.Troy).Actor.Active,
            "Troy $ca:$00 remained after scriptend before GLOBALFLAG_FINISHEDGAME $14.");

        _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame);
        LoadValidationRoom(5,0xd8); StepGameplayUpdates(4,Vector2.Zero);
        cave=_roomEvents.Get<GoronCaveEvent>();
        var troy=cave.Actors.Single(a=>a.Actor.Record.Id==AgesInteractionId.Troy);
        void Talk()
        {
            ApproachGoronFromFloor(troy);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            for(int i=0;i<12&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains("Hoh hoh"),
                "Troy's $ca:$00 A-button prompt did not display source TX_2c06.");
        }
        void WaitFor(string text)
        {
            for(int i=0;i<90&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains(text),
                $"Troy did not display source text containing '{text}'; got '{_dialogue.CurrentMessage}'.");
        }

        Talk(); _dialogue.SubmitChoiceForValidation(1);
        WaitFor("promising"); _dialogue.Close(); StepGameplayUpdates(3,Vector2.Zero);
        FailIf(cave.BlocksGameplay||_saveData.HasGlobalFlag(GlobalFlag.BeganTroySecret),
            "Troy's TX_2c06 No branch retained input or began the secret.");

        int requests=0;
        cave.OpenSecretMenu=(index,complete)=>
        { FailIf(index!=2,"Troy asked for a secret other than TROY_SECRET $02."); requests++; complete(false); return true; };
        Talk(); _dialogue.SubmitChoiceForValidation(0);
        WaitFor("senile"); _dialogue.Close(); StepGameplayUpdates(3,Vector2.Zero);
        FailIf(requests!=1||cave.BlocksGameplay||_saveData.HasGlobalFlag(GlobalFlag.BeganTroySecret),
            "Invalid TROY_SECRET $02 advanced the quest or retained input.");

        Action<bool>? pending=null;
        cave.OpenSecretMenu=(index,complete)=>
        { FailIf(index!=2,"Troy changed his secret type after retry."); pending=complete; return true; };
        Talk(); _dialogue.SubmitChoiceForValidation(0);
        for(int i=0;i<90&&pending is null;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(pending is null||!cave.BlocksGameplay,
            "Troy did not hold script execution while the $02 secret menu was open.");
        LoadValidationRoom(2,0xed); pending!(true);
        StepGameplayUpdates(3,Vector2.Zero);
        FailIf(_saveData.HasGlobalFlag(GlobalFlag.BeganTroySecret)||
            _saveData.HasGlobalFlag(GlobalFlag.DoneTroySecret)||_player.CutsceneControlled,
            "Cancelled Troy secret menu accepted a stale completion callback.");
    }
}
