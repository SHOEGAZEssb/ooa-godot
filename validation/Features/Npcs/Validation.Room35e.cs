using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom35eSubrosian()
    {
        // mainData.s:group3Map5eObjectData contains only $4e:$03 at Y/X=$38/$50.
        NpcRecord record = new NpcDatabase().GetRoomNpcs(3, 0x5e).Single();
        LinkedGameNpcDatabaseRecord secret = new LinkedGameNpcDatabase().Get(3, 0x5e, 0x4e, 3);
        var landing = new TimeWarpLandingDatabase();
        FailIf(record is not { Id: 0x4e, SubId: 3, Var03: 0, Y: 0x38, X: 0x50,
                TextId: 0x4d0a, CanFace: true, DefaultAnimation: 2,
                Implementation: NpcImplementationClassification.OrdinaryGeneric } ||
            secret is not { SecretIndex: 2, ShortSecretIndex: 0x22, BeganFlag: 0x52,
                HasExtraText: true, OfferTextId: 0x4d0a, RefusalTextId: 0x4d0b,
                ExplanationTextId: 0x4d0c, SecretTextId: 0x4d0d, FinalTextId: 0x4d0e } ||
            !PlainWords(secret.OfferMessage).Contains("A stranger has spoken to me!", StringComparison.Ordinal) ||
            !secret.ExplanationMessage.Contains("\\col(3)Subrosian\nVolcanoes\\col(0)", StringComparison.Ordinal) ||
            secret.SecretMessage != "Here goes!\\stop\n\\secret1\\stop\nYou got that?\n  \\opt()Yes \\opt()No",
            "3:5e $4e:$03 lost source placement, facing, TX_4d0a-TX_4d0e controls, or secret $02 metadata.");

        // scriptHelper.s:linkedNpc_checkShouldSpawn entry $02 is @always.
        // Neither the D1 bit nor any other essence is required in a linked file.
        foreach (bool linked in new[] { false, true })
        foreach (byte essences in new byte[] { 0, 1, 2, 0xff })
        {
            _saveData.SetLinkedGame(linked);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, essences);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(3, 0x5e);
            StepGameplayUpdates(2, Vector2.Zero);
            NpcCharacter actor = _entities.Entities<NpcCharacter>().Single();
            FailIf(actor.Active != linked || actor.Visible != linked ||
                _entities.TimeWarpPositionOccupied(landing, 0x35) != linked,
                $"3:5e $4e:$03 spawn must require only linked state: linked={linked}, essences=${essences:x2}.");
        }

        var traces = new List<string>[2];
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetLinkedGame(true);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 0);
            _saveData.SetGlobalFlag(0x52, false);
            _saveData.WriteWramByte(WramAddress.wUnappraisedRingsEnd, 0x34);
            _saveData.WriteWramByte(0xc601, 0x12);
            _saveData.WriteWramByte(WramAddress.wShortSecretIndex, 0);
            _saveData.CommitInventoryChange();
            LoadValidationRoom(3, 0x5e);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single();
            FailIf(!npc.Active || !npc.Visible || npc.Position != new Vector2(0x50, 0x38) ||
                npc.CurrentAnimationOpaquePixels == 0,
                "3:5e linked $4e:$03 must publish its original Subrosian pose.");

            var trace = traces[batched ? 1 : 0] = new List<string>();
            void Step(int updates, Vector2 movement, string[]? held = null, string[]? pressed = null) =>
                StepGameplayUpdates(updates, movement, held, pressed, batched, () => trace.Add(
                    $"{_player.Position}|{_player.CutsceneControlled}|{_dialogue.IsOpen}|" +
                    $"{_dialogue.ChoiceActive}|{_dialogue.SelectedChoice}|{_dialogue.CurrentMessage}|" +
                    $"{npc.CurrentAnimationFrame}|{npc.Visible}|{_saveData.HasGlobalFlag(0x52)}|" +
                    $"{_saveData.ReadWramByte(WramAddress.wShortSecretIndex):x2}"));
            void Approach()
            {
                // The house's lower floor is open. Walk up through its actual
                // geometry until object collision stops Link below the NPC.
                _player.WarpTo(new Vector2(0x50, 0x68));
                FailIf(_currentRoom.IsSolid(_player.Position) || _entities.BlocksLink(_player.Position),
                    "3:5e approach starts inside solid geometry.");
                Step(48, Vector2.Up, ["move_up"]);
                FailIf(_entities.FindTalkTarget(_player) != npc || _entities.BlocksLink(_player.Position) ||
                    _currentRoom.IsSolid(_player.Position),
                    $"3:5e $4e:$03 is unreachable through the house floor; Link={_player.Position}.");
            }
            void Talk()
            {
                Step(1, Vector2.Zero, ["attack"], ["attack"]);
                FailIf(!_dialogue.IsOpen || !_dialogue.ChoiceActive || !_player.CutsceneControlled,
                    "3:5e A-button interaction must open a choice and apply disableinput.");
            }
            void Choice(int choice, int updates = 21)
            {
                _dialogue.SubmitChoiceForValidation(choice);
                Step(updates, Vector2.Zero);
            }
            void FinishText()
            {
                _dialogue.Close();
                Step(2, Vector2.Zero);
                FailIf(_player.CutsceneControlled || _interactions.NpcScriptsForValidation.BlocksGameplay,
                    "3:5e enableinput failed to release Link after text.");
            }

            Approach();
            Talk();
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("A stranger has spoken to me!", StringComparison.Ordinal),
                "3:5e first conversation must offer TX_4d0a.");
            Vector2 linkBefore = _player.Position;
            int frameBefore = npc.CurrentAnimationFrame;
            Step(32, Vector2.Down, ["move_down"]);
            FailIf(_player.Position != linkBefore || npc.CurrentAnimationFrame != frameBefore ||
                _saveData.HasGlobalFlag(0x52),
                "3:5e active text must freeze Link, ordinary NPC animation, and secret generation.");
            _dialogue.SubmitChoiceForValidation(1);
            Step(20, Vector2.Zero);
            FailIf(_dialogue.IsOpen || !_player.CutsceneControlled || _saveData.HasGlobalFlag(0x52),
                "3:5e offer choice wait completed before its 20-update boundary.");
            Step(1, Vector2.Zero);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Well, I won't make you." ||
                _dialogue.ChoiceActive || _saveData.HasGlobalFlag(0x52),
                "3:5e refusing the offer must show TX_4d0b without setting flag $52.");
            FinishText();

            Talk();
            LoadValidationRoom(3, 0x5f);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || _saveData.HasGlobalFlag(0x52) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "3:5e cancelling before generation must release input without writing flag $52 / short index $22.");
            LoadValidationRoom(3, 0x5e);
            npc = _entities.Entities<NpcCharacter>().Single();
            Approach();
            Talk();
            Choice(0, 22);
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("My brother lives in the cave near the three peaks", StringComparison.Ordinal) ||
                !_dialogue.ChoiceActive || _saveData.HasGlobalFlag(0x52),
                "3:5e accepting the offer must show TX_4d0c before generating the secret.");
            Choice(1);
            FailIf(!_dialogue.ChoiceActive || _dialogue.SelectedChoice != 1 ||
                _saveData.HasGlobalFlag(0x52) || _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "3:5e Once more must repeat TX_4d0c without writing flag $52 / short index $22.");
            _dialogue.SubmitChoiceForValidation(0);
            Step(20, Vector2.Zero);
            FailIf(_dialogue.IsOpen || _saveData.HasGlobalFlag(0x52) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0,
                "3:5e secret generation ran before TX_4d0c's wait 20 completed.");
            Step(1, Vector2.Zero);
            string secretMessage = _dialogue.CurrentMessage;
            // bank3.s bit packing/checksum/XOR and bank0.s US symbols,
            // independently calculated for GameID $1234 + short index $22:
            // symbol values $03,$35,$27,$00,$14 -> G4qB!.
            FailIf(!_saveData.HasGlobalFlag(0x52) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x22 ||
                !secretMessage.Contains("Here goes!\nG4qB!\nYou got that?", StringComparison.Ordinal) ||
                !_dialogue.ChoiceActive,
                $"3:5e secret must set flag $52, short index $22, and show source-derived G4qB! in TX_4d0d: " +
                $"flag={_saveData.HasGlobalFlag(0x52)}, index=${_saveData.ReadWramByte(WramAddress.wShortSecretIndex):x2}, " +
                $"choice={_dialogue.ChoiceActive}, text={secretMessage}.");
            Choice(1);
            FailIf(_dialogue.CurrentMessage != secretMessage || _dialogue.SelectedChoice != 1,
                "3:5e No must repeat the same TX_4d0d secret.");
            Choice(0);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Say hi to my brother!" || _dialogue.ChoiceActive,
                "3:5e acknowledging the secret must show TX_4d0e.");
            FinishText();
            Step(4, Vector2.Down, ["move_down"]);
            FailIf(_player.Position == linkBefore,
                "3:5e Link cannot move after completing the secret conversation.");
            Step(4, Vector2.Up, ["move_up"]);
            Talk();
            Step(2, Vector2.Zero);
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("My brother lives", StringComparison.Ordinal),
                "3:5e completed extra-text conversation must resume at TX_4d0c on the next A press.");

            // Cancelling after generation preserves the authoritative save flag.
            LoadValidationRoom(3, 0x5f);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || _interactions.NpcScriptsForValidation.BlocksGameplay ||
                !_saveData.HasGlobalFlag(0x52),
                "3:5e room-exit cancellation must release input and retain flag $52.");
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? restored) ||
                restored is null || !restored.HasGlobalFlag(0x52) || !restored.IsLinkedGame,
                "3:5e linked state and flag $52 did not survive save serialization.");
            LoadValidationRoom(3, 0x5e);
            npc = _entities.Entities<NpcCharacter>().Single();
            Approach();
            Talk();
            FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("A stranger has spoken to me!", StringComparison.Ordinal),
                "3:5e room re-entry must restart at TX_4d0a even with flag $52 set.");
            LoadValidationRoom(3, 0x5f);
            _dialogue.Close();
            FailIf(_player.CutsceneControlled || !_saveData.HasGlobalFlag(0x52),
                "3:5e cancellation during the offer retained input or cleared flag $52.");
        }
        FailIf(!traces[0].SequenceEqual(traces[1]),
            "3:5e Subrosian dialogue/input/animation/save trace differs between individual and batched gameplay updates.");
        GD.Print("Validated 3:5e $4e:$03 linked-only spawn without essence gates, collision approach, " +
            "TX_4d0a-TX_4d0e choices, secret $22 / flag $52, repeat, cancellation, re-entry, and split/batched gameplay.");
    }
}
