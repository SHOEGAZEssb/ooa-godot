using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOceanZoraDialogue()
    {
        var npcs = new NpcDatabase();
        var dialogue = new NpcDialogueRuleDatabase();
        var visibility = new NpcVisibilityRuleDatabase();
        var save = OracleSaveData.CreateStandardGame();
        // Independent literals from zora.s, including its unreachable state 3.
        int[] palace = [0x3410,0x3413,0x3415,0x3417,0x341a,0x3420,0x3424,0x3425,0x3427,0x3428];
        int[] cleaned = [0x3412,0x3414,0x3416,0x3419,0x341b,0x3422,0x3424,0x3426,0x3427,0x3429];
        foreach (var npc in npcs.AllRecords.Where(n => n.Id == 0xab && n.SubId < 10))
        {
            FailIf(npc.Implementation != NpcImplementationClassification.OrdinaryGeneric || !npc.CanFace ||
                !dialogue.TryResolve(npc, save, out var initial) || initial.TextId != palace[npc.SubId],
                $"Zora ${npc.SubId:x2} lost its uncured palace text.");
            FailIf(visibility.ShouldShow(npc, save, AgesGameContent.CreateRuntimeState()) != (npc.SubId < 6),
                $"zora.s:@state0 deletion gate differs for ${npc.SubId:x2}.");
        }
        save.SetGlobalFlag(0x27); save.SetGlobalFlag(0x30); save.WriteWramByte(AgesWramAddress.wEssencesObtained,0x7f);
        foreach (var npc in npcs.AllRecords.Where(n => n.Id == 0xab && n.SubId < 10))
            FailIf(!dialogue.TryResolve(npc, save, out var text) || text.TextId != cleaned[npc.SubId],
                $"Zora ${npc.SubId:x2} must retain the clean-US carry bug after D7.");
        int[] ocean = [0x3448,0x344a,0x344c,0x3446,0x3441,0x3443,0x3445,0x344d,0x344e];
        foreach (var npc in npcs.AllRecords.Where(n => n.Id == 0xab && n.SubId is >= 0x13 and <= 0x1b))
            FailIf(!dialogue.TryResolve(npc,save,out var text) || text.TextId != ocean[npc.SubId-0x13],
                $"Ocean Zora ${npc.SubId:x2} lost its pollution text table.");

        _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
        _inventory.GiveTreasure(TreasureId.Flippers, 0);
        _saveData.SetGlobalFlag(0x30);
        LoadValidationRoom(3,0xb1);
        var zora = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xab);
        _player.WarpTo(new(0x28,0x38));
        for (int tap=0;tap<8 && !zora.CanTalkTo(_player);tap++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
            StepGameplayUpdates(1,Vector2.Right,["move_right"],["move_right"]);
            StepGameplayUpdates(6,Vector2.Right,["move_right"],batched:true);
        }
        FailIf(!zora.CanTalkTo(_player) || _rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0,
            $"Room 3:b1 Zora unreachable from ocean floor: {_player.Position}.");
        for (int repeat=0; repeat<2; repeat++)
        {
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("King Zora"), "Ocean Zora did not talk through A-button routing.");
            _dialogue.Close(); StepGameplayUpdates(3,Vector2.Zero);
        }
        _saveData.SetGlobalFlag(0x30,false);
        FailIf(zora.TextId!=0x3443,"zora.s must select pollution text at room entry, without reselecting it during a conversation.");
        LoadValidationRoom(3,0xb1);
        FailIf(_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xab).TextId!=0x3442,"Zora pollution text was not reselected on re-entry.");
    }

    private void ValidateKingZoraQuestChoiceHandoff()
    {
        // scripts.s: TX_3408 falls through into TX_3409. Both prompts use
        // showtextnonexitable, publishing $80 before the script replaces text.
        foreach (bool batched in new[] { false, true })
        foreach (bool alreadyRefused in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.KingZoraCured);
            _saveData.SetRoomFlag(5, 0xab, 0x40, alreadyRefused);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _dialogue.MessageSpeed = 4;
            LoadValidationRoom(5, 0xab);
            var king = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x9c);
            var story = _roomEvents.Get<KingZoraEvent>();
            void Step(int count = 1, string? button = null) =>
                StepGameplayUpdates(count, Vector2.Zero, button is null ? [] : [button],
                    button is null ? [] : [button], batched);
            void PumpUntil(System.Func<bool> done)
            {
                for (int update = 0; !done() && update < 4000; update++)
                    Step(button: update % 2 == 0 ? "attack" : null);
                FailIf(!done(), "King Zora $5:ab text did not reach its input-driven boundary.");
            }
            void OpenQuest()
            {
                Step(button: "attack"); Step();
                FailIf(!_dialogue.ChoiceActive || !_dialogue.CurrentMessage.Contains("Will you accept\nthis quest?") ||
                    !_dialogue.CurrentMessage.Contains("Yes No"), "TX_3408/TX_3409 lost the source quest and options.");
                PumpUntil(() => _dialogue.ChoiceCursorVisible);
            }
            _player.WarpTo(new(0x78, 0x68));
            for (int tap = 0; tap < 15 && !king.CanTalkTo(_player); tap++)
            {
                Step();
                StepGameplayUpdates(8, Vector2.Up, ["move_up"], ["move_up"], batched);
            }
            FailIf(!king.CanTalkTo(_player) || _currentRoom.IsSolid(_player.Position) ||
                _player.Position.DistanceTo(king.Position) < 16,
                $"King Zora quest approach failed through $5:ab collision: {_player.Position}.");
            Vector2 position = _player.Position;
            OpenQuest();
            int command = story.CurrentCommandIndex;
            Step(4);
            FailIf(story.CurrentCommandIndex != command || story.BlocksGameplay ||
                _player.Position != position, "King Zora advanced before a completed answer.");
            Step(button: "move_right");
            FailIf(_dialogue.SelectedChoice != 1, "King Zora quest cursor cannot select No.");
            Step();
            int confirmationUpdates = 0;
            StepGameplayUpdates(2, Vector2.Down, ["attack"], ["attack"], batched, () =>
            {
                if (++confirmationUpdates == 1)
                    FailIf(!_dialogue.IsOpen || !_dialogue.PrintingComplete || story.CurrentCommandIndex != command,
                        "No must publish $80 after the interaction pass, retaining its panel for one update.");
                FailIf(_player.Position != position, "No confirmation leaked movement through active text.");
            });
            if (!alreadyRefused)
            {
                // scriptCmd_orRoomFlags clears carry and yields after $40.
                FailIf(!_dialogue.PrintingComplete || !_saveData.HasRoomFlag(5, 0xab, 0x40),
                    "First refusal must retain its $80 panel through the room-flag write update.");
                Step();
            }
            FailIf(!_dialogue.CurrentMessage.StartsWith("Is that so...") || !_dialogue.IsOpen ||
                !_saveData.HasRoomFlag(5, 0xab, 0x40) || _inventory.HasTreasure(TreasureId.LibraryKey),
                $"King Zora No handoff must replace the retained prompt with TX_340a and set room bit $40: message={_dialogue.CurrentMessage}, open={_dialogue.IsOpen}, command={story.CurrentCommandIndex}, ready={_dialogue.PrintingComplete}, flag={_saveData.HasRoomFlag(5, 0xab, 0x40)}, blocked={story.ScriptExecutionBlocked}.");
            PumpUntil(() => !_dialogue.IsOpen); Step(3);
            FailIf(_player.CutsceneControlled, "Refusing King Zora retained the player lock.");

            // Repeat without reloading: the short prompt must accept Yes.
            OpenQuest();
            confirmationUpdates = 0;
            StepGameplayUpdates(2, Vector2.Down, ["attack"], ["attack"], batched, () =>
            {
                if (++confirmationUpdates == 1)
                    FailIf(!_dialogue.IsOpen || !_dialogue.PrintingComplete || _player.CutsceneControlled,
                        "Yes must publish its result before King Zora acquires the input lock.");
                FailIf(_player.Position != position, "Yes confirmation leaked movement through active text.");
            });
            FailIf(!_dialogue.CurrentMessage.StartsWith("Very well!") || !_player.CutsceneControlled ||
                _inventory.HasTreasure(TreasureId.LibraryKey), "Yes did not enter TX_340b before granting Library Key $46.");
            PumpUntil(() => !_dialogue.IsOpen && !story.BlocksGameplay);
            Step(3);
            FailIf(!_inventory.HasTreasure(TreasureId.LibraryKey) || _player.CutsceneControlled ||
                _dialogue.BlocksPlayerInput, "King Zora quest did not grant $46 and release text/input.");
            Step(button: "attack"); Step();
            FailIf(_dialogue.ChoiceActive || !_dialogue.CurrentMessage.Contains("Eyeglass"),
                "King Zora repeated the quest instead of TX_340c after granting the key.");
            PumpUntil(() => !_dialogue.IsOpen); Step(3);
            StepGameplayUpdates(4, Vector2.Down, ["move_down"], ["move_down"], batched);
            FailIf(_player.Position.Y <= position.Y, "Link cannot move after King Zora quest completion.");

            // Room cancellation while the choice's $80 result is pending.
            _inventory.LoseTreasure(TreasureId.LibraryKey);
            LoadValidationRoom(5, 0xab);
            _player.WarpTo(position);
            StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"], batched);
            OpenQuest(); Step(button: "attack");
            _dialogue.Close(); LoadValidationRoom(0, 0x11); Step(4);
            FailIf(story.HasState || _player.CutsceneControlled || _inventory.HasTreasure(TreasureId.LibraryKey),
                "Canceling a pending King Zora answer must retire the script without granting $46.");
        }
        GD.Print("Validated King Zora $5:ab input-driven Yes/No handoff, refusal/repeat, Library Key, release and cancellation with split/batched updates.");
    }

    private void ValidateKingZoraOceanStory()
    {
        _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
        _inventory.GiveTreasure(TreasureId.Flippers,0);
        _saveData.SetGlobalFlag(0x27,false); _saveData.SetGlobalFlag(0x30,false);
        _inventory.LoseTreasure(TreasureId.Potion);
        LoadValidationRoom(5,0xad);
        var king = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x9c);
        void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:true);
        void Approach()
        {
            _player.WarpTo(new(0x78,0x68));
            for(int tap=0;tap<15 && !king.CanTalkTo(_player);tap++)
            {
                StepGameplayUpdates(1,Vector2.Zero);
                StepGameplayUpdates(1,Vector2.Up,["move_up"],["move_up"]);
                StepGameplayUpdates(7,Vector2.Up,["move_up"],batched:true);
            }
            FailIf(!king.CanTalkTo(_player) || _rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0 ||
                _player.Position.DistanceTo(king.Position) < 16, $"King Zora unreachable through palace collision: Link {_player.Position}, king {king.Position}, active {king.Active}, button {king.ScriptButtonSensitive}.");
        }
        void Talk() { StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]); Step(1); }
        Approach(); Talk();
        FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("pain"), "Uncured King Zora lost TX_3400.");
        _dialogue.Close(); Step(3);
        _inventory.GiveTreasure(TreasureId.Potion,0);
        LoadValidationRoom(5,0xad); king=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x9c);
        Approach(); Talk();
        FailIf(!_dialogue.ChoiceActive, "King Zora potion offer lost its choice.");
        _dialogue.SubmitChoiceForValidation(1); Step(3);
        FailIf(_saveData.HasGlobalFlag(0x27) || !_inventory.HasTreasure(TreasureId.Potion), "Refusing King Zora consumed potion.");
        Talk(); _dialogue.SubmitChoiceForValidation(0); Step(10);
        FailIf(_inventory.HasTreasure(TreasureId.Potion) || !_player.CutsceneControlled, "Potion handoff did not consume $2f and hold Link.");
        for (int i=0;i<240 && !_saveData.HasGlobalFlag(0x27);i++) { if (_dialogue.IsOpen) _dialogue.Close(); Step(1); }
        FailIf(!_saveData.HasGlobalFlag(0x27) || _player.CutsceneControlled, "King Zora cure failed to persist $27 or release Link.");
        Step(3); Talk(); FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Filthy water"), "King Zora repeat talk did not select TX_3405.");
        _dialogue.Close(); Step(3);
        _inventory.LoseTreasure(TreasureId.LibraryKey); _saveData.SetRoomFlag(5,0xab,0x40,false);
        LoadValidationRoom(5,0xab); king=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x9c);
        Approach(); Talk(); FailIf(!_dialogue.ChoiceActive, "Present King Zora lost the library task choice.");
        _dialogue.SubmitChoiceForValidation(0);
        for (int i=0;i<180 && !_inventory.HasTreasure(TreasureId.LibraryKey);i++) { if (_dialogue.IsOpen) _dialogue.Close(); Step(1); }
        FailIf(!_inventory.HasTreasure(TreasureId.LibraryKey), "King Zora did not grant Library Key $46.");
        for (int i=0;i<180 && _player.CutsceneControlled;i++) { if (_dialogue.IsOpen) _dialogue.Close(); Step(1); }
        _saveData.SetGlobalFlag(0x30); _saveData.SetGlobalFlag(0x31,false);
        LoadValidationRoom(5,0xab); king=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x9c);
        Approach(); Talk(); _dialogue.Close(); Step(3);
        FailIf(!_saveData.HasGlobalFlag(0x31), "Clean-water King Zora failed to grant Jabu permission $31.");
        Talk(); _dialogue.Close(); Step(3);
        _roomEvents.Get<KingZoraEvent>().Cancel();
        FailIf(_player.CutsceneControlled, "King Zora cancellation retained input.");
    }



    private void ValidateKingZoraSwordSecret()
    {
        foreach(int startingSword in new[]{1,2})
        {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.Flippers,0);_inventory.GiveTreasure(TreasureId.MermaidSuit,0);
        _inventory.GiveTreasure(TreasureId.Sword,startingSword);
        _inventory.GiveTreasure(TreasureId.Essence,6);
        _saveData.SetGlobalFlag(0x27);_saveData.SetGlobalFlag(0x30);_saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame);
        _saveData.SetGlobalFlag(0x64,false);_saveData.SetGlobalFlag(0x6e,false);
        LoadValidationRoom(5,0xab);
        var king=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x9c);
        var story=_roomEvents.Get<KingZoraEvent>();
        void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:true);
        _player.WarpTo(new(0x78,0x68));
        for(int i=0;i<15&&!king.CanTalkTo(_player);i++)
        {Step(1);StepGameplayUpdates(1,Vector2.Up,["move_up"],["move_up"]);StepGameplayUpdates(7,Vector2.Up,["move_up"],batched:true);}
        FailIf(!king.CanTalkTo(_player)||_rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision!=0,"Post-game King Zora is unreachable.");
        int requests=0;
        story.OpenSecretMenu=(index,done)=>{FailIf(index!=0,"King Zora requested the wrong secret.");requests++;done(true);return true;};
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);Step(1);
        FailIf(!_dialogue.ChoiceActive,"King Zora secret offer lost its choice.");_dialogue.SubmitChoiceForValidation(0);
        for(int i=0;i<850&&!_saveData.HasGlobalFlag(0x6e);i++){if(_dialogue.IsOpen)_dialogue.Close();Step(1);}
        FailIf(requests!=1||!_saveData.HasGlobalFlag(0x64)||!_saveData.HasGlobalFlag(0x6e)||_inventory.SwordLevel!=startingSword+1,
            $"King Zora secret did not upgrade level {startingSword} and persist BEGAN $64/DONE $6e.");
        for(int i=0;i<80&&_player.CutsceneControlled;i++){if(_dialogue.IsOpen)_dialogue.Close();Step(1);}
        Step(3);StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);Step(1);
        FailIf(!_dialogue.IsOpen||requests!=1||!_dialogue.CurrentMessage.Contains("secret"),"King Zora repeat secret failed its completed branch.");
        _dialogue.Close();Step(3);story.Cancel();FailIf(_player.CutsceneControlled,"King Zora secret cancellation retained Link.");
        }
    }
}
