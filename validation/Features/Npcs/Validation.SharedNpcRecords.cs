using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void SetSharedNpcProgress(int stage)
    {
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, stage == 5);
        _saveData.SetGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame, stage == 4);
        _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, stage == 2);
        _saveData.WriteWramByte(WramAddress.wEssencesObtained,
            (byte)(stage == 0 ? 0 : stage == 3 ? 0x40 : 4));
        _saveData.CommitInventoryChange();
    }

    private void ValidateSharedTownNpcRecords()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        foreach (int stage in Enumerable.Range(0, 6))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            SetSharedNpcProgress(stage);
            foreach (var spec in new (int Room, int Sub, int State)[] { (0x46, 2, 1), (0x46, 5, 4), (0x67, 3, 2), (0xa7, 6, 5), (0x77, 3, -1) })
            {
                LoadValidationRoom(0, spec.Room);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                int id = spec.Room == 0x77 ? 0x44 : 0x41;
                var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == id && n.Record.SubId == spec.Sub);
                bool visible = spec.State < 0 ? stage >= 3 : stage == spec.State;
                FailIf(npc.Active != visible || npc.Visible != visible,
                    $"{linked}/{stage}: room 0:{spec.Room:x2} ${id:x2}:${spec.Sub:x2} visibility differs from getGameProgress_1.");
                if (!visible) continue;
                int expectedText = (id == 0x41 ? 0x2600 : 0x1610) + stage;
                FailIf(npc.TextId != expectedText || npc.Record.CanFace || id == 0x41 && npc.Record.Palette != 1,
                    $"Room 0:{spec.Room:x2} lost TX_{expectedText:x4}, fixed pose, or OAM palette $01.");
                ApproachLibraryActor(npc, batched);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != PlainWords(npc.Message.Replace("\\Link", _saveData.LinkName, StringComparison.Ordinal)),
                        $"Room 0:{spec.Room:x2} TX_{expectedText:x4} was unreachable on talk {repeat}.");
                    _dialogue.Close(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                }
                SetSharedNpcProgress((stage + 1) % 6);
                FailIf(!npc.Active || npc.TextId != expectedText,
                    "A state-zero-selected town NPC refreshed its existence/script during its lifetime.");
                SetSharedNpcProgress(stage);
            }
        }
        GD.Print("Validated six newly supported town records' progression, linked/unlinked selection, fixed palettes/poses, initialization lifetime and reachable repeat dialogue.");
    }

    private void ValidateForestNpcRecords()
    {
        int[] rooms = [0x82, 0x80, 0x71, 0x82, 0x82, 0x82, 0x72, 0x81];
        int[] subids = [5, 6, 7, 8, 9, 10, 12, 13];
        int[] texts = [0x110d, 0x1110, 0x1113, 0x110e, 0x1111, 0x1114, 0x1112, 0x1115];
        int[] palettes = [1, 2, 3, 1, 2, 3, 2, 3];
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (int phase in Enumerable.Range(0, 4))
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(GlobalFlag.WonFairyHidingGame, phase != 0);
            _saveData.SetGlobalFlag(GlobalFlag.ForestUnscrambled, phase != 0);
            _saveData.SetRoomFlag(0, 0x90, 0x40, phase >= 2);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, phase == 3);
            for (int i = 0; i < subids.Length; i++)
            {
                LoadValidationRoom(0, rooms[i]); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x49 && n.Record.SubId == subids[i]);
                bool expected = i < 3 ? phase == 1 : i < 6 ? phase == 2 : phase == 3;
                FailIf(npc.Active != expected || npc.Visible != expected,
                    $"Forest fairy $49:${subids[i]:x2} selected the wrong phase {phase}.");
                if (!expected) continue;
                FailIf(npc.TextId != texts[i] || npc.Record.Palette != palettes[i] || npc.Record.CanFace ||
                    npc.ScriptDrawOffset != new Vector2(0, -4) || npc.ObjectCollisionBounds.Size != new Vector2(8, 8) ||
                    npc.CurrentAnimationOpaquePixels == 0 || npc.Record.DefaultAnimation != (palettes[i] == 1 ? 0 : 1),
                    $"Forest fairy $49:${subids[i]:x2} lost source TX_{texts[i]:x4}/palette/Z/radius/animation.");
                ApproachLibraryActor(npc, batched);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != PlainWords(npc.Message.Replace("\\Link", _saveData.LinkName, StringComparison.Ordinal)),
                        $"Forest fairy $49:${subids[i]:x2} repeat dialogue {repeat} is unreachable.");
                    _dialogue.Close(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                }
                _saveData.SetGlobalFlag(GlobalFlag.ForestUnscrambled, false);
                FailIf(!npc.Active, "Forest fairy repeated a state-zero visibility gate while alive.");
                _saveData.SetGlobalFlag(GlobalFlag.ForestUnscrambled, true);
            }
        }
        GD.Print("Validated all eight forest-fairy records across four phases, both game modes, real geometry, repeat conversations and initialization lifetime.");
        var visibility = new NpcVisibilityRuleDatabase();
        var database = new NpcDatabase();
        foreach(int flags in Enumerable.Range(0,16))
        {
            var save=OracleSaveData.CreateStandardGame();var runtime=new OracleRuntimeState();
            bool won=(flags&1)!=0, forest=(flags&2)!=0, jabu=(flags&4)!=0, finished=(flags&8)!=0;
            save.SetGlobalFlag(GlobalFlag.WonFairyHidingGame,won);save.SetGlobalFlag(GlobalFlag.ForestUnscrambled,forest);
            save.SetGlobalFlag(GlobalFlag.FinishedGame,finished);save.SetRoomFlag(0,0x90,0x40,jabu);
            for(int i=0;i<subids.Length;i++)
            {
                var record=database.GetRoomNpcs(0,rooms[i]).Single(n=>n.Id==0x49&&n.SubId==subids[i]);
                bool expected=i<3?won&&forest&&!jabu:i<6?won&&forest&&jabu&&!finished:finished;
                FailIf(visibility.ShouldShow(record,save,runtime)!=expected,$"Forest fairy $49:${subids[i]:x2} lost independent native flag boundary ${flags:x2}.");
            }
        }
    }

    private void ValidatePostgameNpcRecords()
    {
        int[] ids = [0x36, 0x37, 0x3c, 0x39, 0x5d];
        string[] words = ["art of fighting", "I'm training everyday!", "Shhh! I can't hear", "Listen quietly", "wonderfully enchanting"];
        foreach (bool batched in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            LoadValidationRoom(0, 0x3a); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<NpcCharacter>().Any(n => ids.Contains(n.Record.Id) && n.Active),
                "Postgame audience existed before GLOBALFLAG_FINISHEDGAME.");
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            FailIf(_entities.Entities<NpcCharacter>().Any(n => ids.Contains(n.Record.Id) && n.Active),
                "Postgame audience respawned without re-entry after its state-zero deletion.");
            LoadValidationRoom(0, 0x3a); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_sound.NativeRoomMusic != 3 || _sound.NativeActiveMusic != 0xff,
                "Postgame Nayru did not initialize wActiveMusic2=$03/wActiveMusic=$ff.");
            for (int i = 0; i < ids.Length; i++)
            {
                var slot = _entities.EntityAdapters<SharedScriptNpcRoomEntity>().Single(s => s.Npc.Record.Id == ids[i]);
                var npc = slot.Npc;
                FailIf(!npc.Active || !npc.Visible || npc.CurrentAnimationOpaquePixels == 0,
                    $"Postgame ${ids[i]:x2} has no visible native actor.");
                ApproachLibraryActor(npc, batched);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                    if (i < 2)
                    {
                        int wait = i == 0 ? 10 : 20;
                        FailIf(_dialogue.IsOpen || slot.Script!.Counter != wait || !slot.Script.InputDisabled,
                            $"Postgame ${ids[i]:x2} failed its initial {wait}-update wait/input handoff.");
                        FailIf(!_interactions.FreezesNonInteractionObjects,
                            "disableinput did not publish the source $81 non-interaction freeze during the pre-text wait.");
                        StepGameplayUpdates(wait - 1, Vector2.Down, batched: batched);
                        FailIf(_dialogue.IsOpen || slot.Script.Counter != 1,
                            "Postgame pre-conversation wait finished one update early.");
                    }
                    for (int update = 0; !_dialogue.IsOpen && update < 40; update++)
                        StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                    FailIf(!_dialogue.IsOpen || !PlainWords(_dialogue.CurrentMessage).Contains(words[i], StringComparison.Ordinal),
                        $"Postgame ${ids[i]:x2} lost source dialogue or reachable repeat {repeat}: {_dialogue.CurrentMessage}.");
                    if (i == 2) FailIf((_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0 + 0x1e) & 4) == 0,
                        "Postgame boy did not OR the shared cfde signal with $04.");
                    _dialogue.Close(); StepGameplayUpdates(45, Vector2.Zero, batched: batched);
                    FailIf(slot.Script!.InputDisabled || _player.CutsceneControlled,
                        $"Postgame ${ids[i]:x2} retained input after its return waits.");
                }
            }
            FailIf(!_entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>().Any(),
                "Postgame Nayru emitted no singing music-note child.");
            var nayru = _entities.EntityAdapters<SharedScriptNpcRoomEntity>().Single(s => s.Npc.Record.Id == 0x36);
            ApproachLibraryActor(nayru.Npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            LoadValidationRoom(0, 0x3b);
            FailIf(nayru.Script!.InputDisabled || nayru.Script.HasState,
                "Leaving during Nayru's wait leaked script or player control.");
        }
        GD.Print("Validated all five postgame records, dialogue source content, exact waits, repeat/cancellation, shared boy signal and singing children in both stepping modes.");
    }

    private void ValidateAdlarAndRupeeGift()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int state in Enumerable.Range(0, 5))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, state == 4);
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru, state >= 2);
            _saveData.SetRoomFlag(4, 0xfc, 0x80, state >= 3);
            _saveData.SetRoomFlag(3, 0xfa, 0x40, state == 1);
            LoadValidationRoom(3, 0xfa); StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x29);
            ApproachLibraryActor(npc, batched);
            string[] words = ["palace advisor", "monster in human guise", "beast in human guise", "3716", "3713"];
            int[] ids = [0x3710, 0x3711, 0x3712, 0x3716, 0x3713];
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                int text = state == 0 && repeat == 1 ? 0x3711 : ids[state];
                var imported = new SharedNpcDatabase().Script(npc.Record).OfType<CutsceneShowTextCommand>().Single(c => c.TextId == text);
                FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != PlainWords(imported.Message.Replace("\\Link", _saveData.LinkName, StringComparison.Ordinal)) ||
                    state < 3 && repeat == 0 && !PlainWords(_dialogue.CurrentMessage).Contains(words[state], StringComparison.Ordinal),
                    $"Adlar state {state} repeat {repeat} lost TX_{text:x4}.");
                if (state == 0 && repeat == 0) FailIf(_saveData.HasRoomFlag(3, 0xfa, 0x40), "Adlar committed $40 before first text closed.");
                _dialogue.Close(); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                if (state == 0) FailIf(!_saveData.HasRoomFlag(3, 0xfa, 0x40), "Adlar failed to commit his first-meeting flag.");
            }
            if (state == 0)
            {
                LoadValidationRoom(3, 0xfa); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x29);
                ApproachLibraryActor(npc, batched);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!PlainWords(_dialogue.CurrentMessage).Contains("monster in human guise", StringComparison.Ordinal), "Adlar repeated his introduction after re-entry.");
                _dialogue.Close();
            }
        }
        foreach (bool batched in new[] { false, true })
        foreach (int wallet in new[] { 0, 850 })
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(3, 0xe9, 0x40, false);
            _inventory.LoseTreasure(TreasureId.Rupees);
            _inventory.AddRupees(wallet - _inventory.Rupees); _statusBar.SynchronizeRupees();
            LoadValidationRoom(3, 0xe9); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x2e);
            FailIf(npc.Position != new Vector2(0x28, 0x38), "Gift old man retained placed coordinates instead of native $28,$38.");
            ApproachLibraryActor(npc, batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != "Shh! Don't tell!" || _inventory.Rupees != wallet,
                "Gift old man lost TX_3318 or paid before closing text.");
            _dialogue.Close(); StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(_inventory.Rupees != Math.Min(999, wallet + 200) || !_inventory.HasTreasure(TreasureId.Rupees) || _saveData.HasRoomFlag(3, 0xe9, 0x40),
                "Gift old man failed to grant RUPEEVAL_200 or committed before wait/HUD catch-up.");
            var giftHost=_entities.EntityAdapters<SharedScriptNpcRoomEntity>().Single().Script!;
            FailIf(giftHost.Counter!=8,"Gift old man lost the original wait-8 initialization.");
            StepGameplayUpdates(7,Vector2.Zero,batched:batched);
            FailIf(giftHost.Counter!=1||_saveData.HasRoomFlag(3,0xe9,0x40),"Gift old man completed before the eighth wait update.");
            for (int update = 0; !_saveData.HasRoomFlag(3, 0xe9, 0x40) && update < 240; update++)
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(!_saveData.HasRoomFlag(3, 0xe9, 0x40) || _player.CutsceneControlled,
                "Gift old man failed to finish the rupee-display handoff.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (repeat == 1) { LoadValidationRoom(3, 0xe9); StepGameplayUpdates(3, Vector2.Zero, batched: batched); npc = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x2e); ApproachLibraryActor(npc, batched); }
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen || PlainWords(_dialogue.CurrentMessage) != "I have nothing more for you." || _inventory.Rupees != Math.Min(999, wallet + 200),
                    "Gift old man repeated his reward after completion/re-entry or lost TX_3319.");
                _dialogue.Close(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
        }
        GD.Print("Validated Adlar's five initialization branches, first-talk flag and re-entry; old man's 200-rupee gift, wallet cap, HUD handoff and repeats.");
    }

    private void ValidatePastNayruAndImpaRecords()
    {
        foreach (bool linked in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetLinkedGame(linked);
            _saveData.SetGlobalFlag(GlobalFlag.PreBlackTowerCutsceneDone);
            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit, false);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            LoadValidationRoom(1, 0x68); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var nayru = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x36);
            FailIf(!nayru.Active || nayru.TextId != 0x1d15 || !PlainWords(nayru.Message).Contains("may do something hasty", StringComparison.Ordinal),
                "Past Nayru lost @init0c's three gates or TX_1d15.");
            ApproachLibraryActor(nayru, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen || !PlainWords(_dialogue.CurrentMessage).Contains("may do something hasty", StringComparison.Ordinal), "Past Nayru's repeated dialogue is unreachable.");
                _dialogue.Close(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit);
            FailIf(!nayru.Active, "Past Nayru repeated her state-zero gate without re-entry.");
            LoadValidationRoom(1, 0x68);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x36).Active, "Past Nayru survived re-entry after the flame of despair was lit.");
            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit, false);
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru);
            _saveData.SetRoomFlag(0, 0x83, 0x80);
            _inventory.GiveTreasure(TreasureId.Harp, 1);
            _inventory.GiveTreasure(TreasureId.MakuSeed, 0);
            LoadValidationRoom(1, 0x47); StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            var impa = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x4f);
            FailIf(!impa.Active || impa.TextId != (linked ? 0x012e : 0x012b) ||
                !PlainWords(impa.Message).Contains(linked ? "everything would be fine" : "villagers", StringComparison.Ordinal),
                "Past Impa lost state $07 or the linked/unlinked TX_012e/TX_012b selection.");
            ApproachLibraryActor(impa, batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(!_dialogue.IsOpen || !PlainWords(_dialogue.CurrentMessage).Contains(linked ? "everything would be fine" : "villagers", StringComparison.Ordinal), "Past Impa's repeated conversation is unreachable.");
                _dialogue.Close(); StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            }
            _saveData.SetGlobalFlag(GlobalFlag.FlameOfDespairLit);
            FailIf(!impa.Active, "Past Impa repeated her state-zero visibility gate.");
            LoadValidationRoom(1, 0x47);
            FailIf(_entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0x4f).Active, "Past Impa did not delete on re-entry into state $08.");
        }
        GD.Print("Validated past Nayru and Impa visibility lifetimes, source dialogue, linked differences and reachable repeats.");
    }

    private void ValidateWaitingNayruAnimals()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int flags in Enumerable.Range(0, 16))
        {
            ReinitializeGameplayForValidation();
            bool intro=(flags&1)!=0, maku=(flags&2)!=0, saved=(flags&4)!=0, finished=(flags&8)!=0;
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone,intro);
            _saveData.SetGlobalFlag(GlobalFlag.RalphEnteredPortal);
            _saveData.SetGlobalFlag(GlobalFlag.MakuTreeSaved,maku);
            _saveData.SetGlobalFlag(GlobalFlag.SavedNayru,saved);
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame,finished);
            LoadValidationRoom(0,0x39); StepGameplayUpdates(4,Vector2.Zero,batched:batched);
            var animals=_entities.EntityAdapters<WaitingNayruAnimalRoomEntity>().ToArray();
            int count=!intro||finished?0:(maku?3:0)+(saved?1:0);
            FailIf(animals.Length!=count,$"Waiting animals flags ${flags:x2}: expected {count}, got {animals.Length}; parent deletion must not skip parseGivenObjectData.");
            foreach(var animal in animals)
            {
                var npc=animal.Npc;int id=npc.Record.Id;
                int text=id==0x5d?(saved?0x5713:0x5712):id==0x4b?(saved?0x5718:0x5717):npc.Record.Var03==0?0x5715:saved?0x571a:0x5719;
                Vector2 position=id==0x5d?new(0x68,0x18):id==0x4b?new(0x78,0x48):npc.Record.Var03==0?new(0x88,0x28):new(0x58,0x38);
                FailIf(!npc.Active||!npc.Visible||npc.TextId!=text||npc.Position!=position,
                    $"Waiting animal ${id:x2}/v${npc.Record.Var03:x2} lost source position or TX_{text:x4}.");
                if(!intro||finished)continue;
                ApproachLibraryActor(npc,batched);
                for(int repeat=0;repeat<2;repeat++)
                {
                    StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
                    FailIf(!_dialogue.IsOpen||PlainWords(_dialogue.CurrentMessage)!=PlainWords(npc.Message),
                        $"Waiting animal ${id:x2} TX_{text:x4} repeat {repeat} is unreachable.");
                    _dialogue.Close();StepGameplayUpdates(3,Vector2.Zero,batched:batched);
                }
                _saveData.SetGlobalFlag(GlobalFlag.SavedNayru,!saved);
                FailIf(!npc.Active||npc.TextId!=text,"Waiting animal repeated a native initialization choice while alive.");
                _saveData.SetGlobalFlag(GlobalFlag.SavedNayru,saved);
            }
        }
        GD.Print("Validated bear $5d:$02/v$00 and its ordered monkey/rabbit children across all 16 progression combinations, including child creation before parent deletion and reachable repeated dialogue.");
    }

    private void ValidateCheapScrubAndPatrol()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int level in new[]{1,2,3})
        foreach(int branch in new[]{0,1,2,3})
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Shield,level);
            if(branch!=3)_inventory.LoseTreasure(TreasureId.Shield);
            int price=new[]{10,20,40}[level-1];
            int wallet=branch==2?price-1:200;
            _inventory.AddRupees(wallet-_inventory.Rupees);
            LoadValidationRoom(1,0x70);StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            var scrub=_entities.EntityAdapters<BusinessScrubRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(scrub.Npc)<2||scrub.BushSlot is null||_entities.InteractionSlot(scrub.BushSlot.Node)<2||
                _entities.InteractionSlot(scrub.Npc)==_entities.InteractionSlot(scrub.BushSlot.Node),
                "$ce:$06 and its $ce:$80 child did not retain two independent native allocations.");
            FailIf(scrub.Offer.Price!=price||scrub.Offer.Parameter!=level||scrub.Offer.EffectiveSubId!=level+5,
                "$ce:$06 lost source $10/$20/$40 offers or initialized shield-level selection.");
            ApproachLibraryActor(scrub.Npc,batched);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains(price.ToString(),StringComparison.Ordinal),"Cheap scrub's prompt is unreachable.");
            _dialogue.SubmitChoiceForValidation(branch==1?1:0);StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            int expectedWallet=branch==0?wallet-price:wallet;
            FailIf(_inventory.Rupees!=expectedWallet||_inventory.HasTreasure(TreasureId.Shield)!=(branch is 0 or 3),
                "Cheap scrub's buy/decline/insufficient/already-owned branch changed the wrong inventory state.");
            _dialogue.Close();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen||_inventory.Rupees!=expectedWallet,"Cheap scrub failed a repeated purchase prompt.");
            LoadValidationRoom(1,0x71);
            FailIf(_roomEvents.Get<BusinessScrubEvent>().HasState,"Leaving cheap scrub's prompt leaked native purchase state.");
        }
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xf3);
            var npc=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x58&&n.Record.Var03==4);
            Vector2 start=npc.Position;
            FailIf(npc.TextId!=0x100d||!PlainWords(npc.Message).Contains("dangerous past this point",StringComparison.Ordinal),"$58:$03/v$04 did not choose its fixed TX_100d.");
            StepGameplayUpdates(96,Vector2.Zero,batched:batched);
            FailIf(npc.Position!=start+new Vector2(47,0),"$58:$03/v$04 did not perform 95 SPEED_80 movements before its $60 counter reached zero.");
            StepGameplayUpdates(20,Vector2.Zero,batched:batched);
            ApproachLibraryActor(npc,batched);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            for(int i=0;!_dialogue.IsOpen&&i<25;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(!_dialogue.IsOpen||!PlainWords(_dialogue.CurrentMessage).Contains("dangerous past this point",StringComparison.Ordinal),"Patrolling worker's conversation is unreachable.");
            Vector2 stopped=npc.Position;
            _dialogue.Close();StepGameplayUpdates(29,Vector2.Zero,batched:batched);
            FailIf(npc.Position!=stopped,"Worker resumed movement before its post-conversation wait 30.");
            StepGameplayUpdates(5,Vector2.Zero,batched:batched);
            FailIf(npc.Position==stopped,"Worker did not resume his source patrol after conversation.");
        }
        GD.Print("Validated cheap scrub shield levels and purchase outcomes/cancellation; worker fixed text, patrol counter, talk pause and resume in both stepping modes.");
    }

    private void ValidateAdvanceShopRecord()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool linked in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(linked);
            _saveData.WriteWramByte(WramAddress.wBoughtShopItems2,0);
            _inventory.AddRupees(999-_inventory.Rupees);
            LoadValidationRoom(3,0xfe);
            var shop=_roomEvents.Get<LynnaShopEvent>();
            var data=new LynnaShopDatabase(advance:true);
            var keeper=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x46);
            FailIf(keeper.Record.SubId!=2||keeper.Position!=new Vector2(0x28,0x48)||keeper.ObjectCollisionBounds.Size!=new Vector2(40,12),"Advance shopkeeper lost subid $02, source position or $06/$14 collision.");
            var stock=_entities.Entities<LynnaShopItem>();
            FailIf(!stock.Select(s=>s.Record.SubId).SequenceEqual(new[]{0x0e,0x0f,0x10})||
                stock.Any(s=>s.Record.Price!=100)||data.Item(0x0f).TreasureId!=0x2d||data.Item(0x0f).Parameter!=0x0e||data.Item(0x10).Parameter!=1,
                "Advance shop lost its ordered Gasha/GBA_TIME_RING/tier-1 ring stock and 100-rupee prices.");
            void Step(int count=1)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            void Walk(Vector2 target)
            {
                for(int axis=0;axis<2;axis++)
                for(int i=0;i<160&&Mathf.Abs(axis==0?_player.Position.X-target.X:_player.Position.Y-target.Y)>1;i++)
                {
                    Vector2 d=axis==0?(_player.Position.X>target.X?Vector2.Left:Vector2.Right):(_player.Position.Y>target.Y?Vector2.Up:Vector2.Down);
                    string action=d==Vector2.Left?"move_left":d==Vector2.Right?"move_right":d==Vector2.Up?"move_up":"move_down";
                    StepGameplayUpdates(1,d,[action],[action],batched);
                }
                FailIf(_collision.Collides(_player.Position),$"Advance shop approach crossed solid geometry at {_player.Position}.");
            }
            void Press(){StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);Step();}
            void Merchant(){Walk(new(80,72));Walk(new(68,72));_player.Face(Vector2I.Left);Press();}
            _player.WarpTo(new(80,104));Step(2);
            Merchant();
            FailIf(!_dialogue.IsOpen||!PlainWords(_dialogue.CurrentMessage).Contains("Advance",StringComparison.Ordinal),"Advance shop's TX_0e20 welcome is unreachable.");
            _dialogue.Close();Step(2);
            int ringsBefore=_inventory.UnappraisedRingCount;
            for(int i=0;i<3;i++)
            {
                Walk(new(80,88));Walk(new(68+i*32,58));_player.Face(Vector2I.Up);Press();
                FailIf(!stock[i].Held||!_player.IsCarryingObject,$"Advance item $47:${14+i:x2} is unreachable at its shelf.");
                if(i==0)
                {
                    Walk(new(80,72));
                    for(int update=0;!_dialogue.IsOpen&&update<140;update++)
                        StepGameplayUpdates(1,Vector2.Down,["move_down"],["move_down"],batched);
                    // scriptCmd_moveNpc counts the zero update without moving:
                    // $10 gives 15*2 down pixels; $18 gives 23*2 right pixels.
                    FailIf(!_dialogue.IsOpen||!PlainWords(_dialogue.CurrentMessage).Contains("Thief!",StringComparison.Ordinal)||
                        keeper.Position!=new Vector2(0x56,0x66)||!stock[0].Held||!_player.CutsceneControlled,
                        $"Advance theft lost source counter2 movement, text or held-stock/input ownership: {keeper.Position}.");
                    _dialogue.Close();
                    for(int update=0;_player.CutsceneControlled&&update<70;update++)Step();
                    Step(2);
                    FailIf(keeper.Position!=new Vector2(0x28,0x48)||!stock[0].Held||_player.CutsceneControlled||
                        keeper.CurrentScriptAnimationSource!=data.Animation(0x46,1),
                        "Advance theft failed its left/up return, angle $08, retained stock or enableallobjects handoff.");
                }
                Merchant();
                FailIf(!_dialogue.IsOpen||shop.Stage!=LynnaShopEventStage.PurchasePrompt,"Advance keeper did not price the held item.");
                if(i==0)
                {
                    _dialogue.SubmitChoiceForValidation(1);Step(2);
                    FailIf(stock[i].Held||_inventory.Rupees!=999||(_saveData.ReadWramByte(WramAddress.wBoughtShopItems2)&7)!=0,"Declining an Advance purchase changed its sold bit/wallet.");
                    Walk(new(80,88));Walk(new(68,58));_player.Face(Vector2I.Up);Press();Merchant();
                    _inventory.AddRupees(-999);_dialogue.SubmitChoiceForValidation(0);Step();
                    FailIf(!_dialogue.IsOpen||_inventory.Rupees!=0||(_saveData.ReadWramByte(WramAddress.wBoughtShopItems2)&7)!=0,
                        "Insufficient Advance funds charged the wallet or committed a sold bit.");
                    _dialogue.Close();Step(2);_inventory.AddRupees(999);
                    Walk(new(80,88));Walk(new(68,58));_player.Face(Vector2I.Up);Press();Merchant();
                }
                _dialogue.SubmitChoiceForValidation(0);Step();
                FailIf(_inventory.Rupees!=899-i*100||(_saveData.ReadWramByte(WramAddress.wBoughtShopItems2)&(1<<i))==0,
                    "Advance purchase failed its charge/sold-bit commit.");
                _dialogue.Close();Step(3);
            }
            FailIf(_inventory.UnappraisedRingCount!=ringsBefore+2,"Advance fixed GBA ring and random tier-1 ring were not both awarded.");
            FailIf(_inventory.UnappraisedRingAt(ringsBefore)!=0x4e,"Advance shop's fixed ring was not unappraised GBA_TIME_RING $0e (stored with bit $40).");
            LoadValidationRoom(3,0xfe);Step(2);
            FailIf(_entities.Entities<LynnaShopItem>().Count!=0,"Advance shop restocked sold items on re-entry.");
            _player.WarpTo(new(80,104));Merchant();
            FailIf(!_dialogue.IsOpen||!PlainWords(_dialogue.CurrentMessage).Contains("closing shop",StringComparison.Ordinal),"Advance shop did not select TX_0e26 after selling everything.");
        }
        GD.Print("Validated Advance shop welcome, three ordered items, reachable carrying/purchases, decline, sold flags, both ring rewards, linked differences and re-entry.");
    }

    private void ValidateRalphBeforeCurrentsRecord()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(1,0x20,0x40,false);
            _inventory.LoseTreasure(TreasureId.TuneOfCurrents);
            LoadValidationRoom(1,0x20);
            FailIf(_entities.Entities<RalphAfterChevalCharacter>().Single().Active,"Ralph $0b did not require a warp destination $24.");
            LoadValidationRoom(0,0x20);
            var origin=new Vector2(0x48,0x28);
            FailIf(_collision.Collides(origin),"Ralph $0b's time-warp source $24 is not open floor.");
            _player.WarpTo(origin);_transitions.ApplyHarpTimeWarp(_player,origin);
            for(int i=0;IsTransitioning&&i<600;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(IsTransitioning||_rooms.ActiveGroup!=1||_rooms.CurrentRoom.Id!=0x20,"Ralph $0b's actual harp time-warp failed to arrive in 1:20.");
            var roomEvent=_roomEvents.Get<RalphBeforeCurrentsEvent>();
            var ralph=_entities.Entities<RalphAfterChevalCharacter>().Single();
            FailIf(!ralph.Active||!roomEvent.HasState||!roomEvent.MenusDisabled,"Ralph $0b failed its native warp initialization/input mask.");
            for(int i=0;!_dialogue.IsOpen&&i<400;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            // counter2 reaches zero without moving: 47*2 + 31*1 + 31*.5
            // pixels left, then 47*1 right, retaining x=$4a.80.
            FailIf(!_dialogue.IsOpen||!PlainWords(_dialogue.CurrentMessage).Contains("old hermit",StringComparison.Ordinal)||ralph.Position!=new Vector2(0x4a,0x38),
                $"Ralph $0b lost TX_2a1a or its SPEED_200/100/80 horizontal route: {ralph.Position}.");
            var position=ralph.Position;StepGameplayUpdates(10,Vector2.Right,batched:batched);
            FailIf(ralph.Position!=position||_saveData.HasRoomFlag(1,0x20,0x40),"Ralph $0b advanced/completed while dialogue was open.");
            _dialogue.Close();
            for(int i=0;roomEvent.HasState&&i<180;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(roomEvent.HasState||ralph.Active||roomEvent.MenusDisabled||_player.CutsceneControlled||!_saveData.HasRoomFlag(1,0x20,0x40),"Ralph $0b failed its external ralphEndCutscene continuation, completion flag or input/menu cleanup.");
            LoadValidationRoom(1,0x20);
            FailIf(_entities.Entities<RalphAfterChevalCharacter>().Single().Active,"Completed Ralph $0b scene repeated after re-entry.");
        }
        GD.Print("Validated Ralph $37:$0b through actual harp time travel, its horizontal fixed-point route, dialogue freeze, external script continuation, flag, menus, input and re-entry.");
    }
}
