using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidEssenceRom()
    {
        foreach (bool batch in new[] {false,true})
        {
            ReinitializeGameplayForValidation();
            for (int index=0;index<5;index++) _inventory.GiveTreasure(TreasureId.Essence,index);
            _saveData.SetMakuTreeState(5); _saveData.SetMakuMapTextPresent(0xb7); _saveData.SetMakuMapTextPast(0x5a);
            _saveData.SetRoomFlag(3,0x0f,0x80); // The source entrance was unlocked before this dungeon.
            LoadValidationRoom(5,0x37);
            var essence=_entities.Entities<DungeonEssence>().Single();
            var visual=new DungeonInteractionVisualDatabase().Visual("bereft-peak");
            FailIf(essence.EssenceIndex != 5 || essence.Position != new Vector2(120,40) || visual.TileBase != 0x0c ||
                visual.Palette != 0 || !essence.Message.StartsWith("\\pos(2)",StringComparison.Ordinal) ||
                !essence.Message.Contains("Bereft Peak",StringComparison.Ordinal) || !essence.Message.Contains("stalwart",StringComparison.Ordinal) ||
                essence.ExitWarp is not {DestinationGroup:3,DestinationRoom:0x0f,DestinationPosition:0x16,DestinationTransition:WarpDestinationTransition.SetRespawn},
                "Source INTERAC$7f's sixth OAM/TX_0013/exit row must select Bereft Peak and room$3:$0f/$16.");
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var actor in active.Where(actor => actor != essence).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(120,120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Essence approach must start on original south floor outside the pedestal.");
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,120) {HostilePartsEnabled=true};
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39]=0x0c;
            int s=0xd040+_entities.InteractionSlot(essence)*256;
            rom[s]=1; rom[s+1]=0x7f; rom[s+11]=40; rom[s+13]=120;
            var sounds=_sound.AttachPlayRequestAudit(); int update=0;
            var partSlots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            void Step(int count=1,bool approach=false) => StepGameplayUpdates(count,approach ? Vector2.Up : Vector2.Zero,
                batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,approach ? 0x40 : 0,approach ? 0 : 0xff,_entities.FrameCounter);
                rom.AdvanceTileGraphics();
                string context=$"Mermaid Essence batch={batch}, update{++update}";
                CompareSomariaMotionRom(rom,context);
                var random=_random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    context+": energy PARTs must retain the shared native RNG sequence.");
                var beads=_entities.Entities<BlueEnergyBeadRoomEntity>();
                FailIf(beads.Count != Enumerable.Range(0,16).Count(slot => rom[0xd0c0+slot*256] != 0 && rom[0xd0c1+slot*256] == 0x53),
                    context+": native energy allocation/deletion count differs.");
                foreach(var bead in beads)
                {
                    int part=0xd0c0+partSlots[bead]*256;
                    FailIf(rom[part+1] != 0x53 || bead.Index != rom[part+3] || bead.Delay != rom[part+7] ||
                        bead.Duration != rom[part+6] || bead.Initialized != (rom[part+4] != 0) ||
                        bead.Visible != ((rom[part+26]&0x80) != 0) ||
                        bead.PrecisePosition != new Vector2(rom.Word(part+12)/256f,rom.Word(part+10)/256f),
                        context+$": native PART$53 slot${part:x4} motion, visibility or delay differs.");
                }
                var precise=SomariaPrivate<Vector2>(essence,"_precisePosition");
                FailIf(precise != new Vector2(rom.Word(s+12)/256f,rom.Word(s+10)/256f) ||
                    (SomariaPrivate<int>(essence,"_zFixed")&0xffff) != rom.Word(s+14) ||
                    _inventory.Essences != rom[WramAddress.wEssencesObtained] || _saveData.GetRoomFlags(5,0x37) != rom[0xca37] ||
                    essence.EssenceIndex != rom[s+3] || essence.Visible != ((rom[s+26]&0x80) != 0),
                    context+$": source motion/collection/OAM differs: state={rom[s+4]}, XY={precise}/{rom.Word(s+12)/256f},{rom.Word(s+10)/256f}, Z={SomariaPrivate<int>(essence,"_zFixed")}/{unchecked((short)rom.Word(s+14))}, essences=${_inventory.Essences:x2}/${rom[WramAddress.wEssencesObtained]:x2}, flags=${_saveData.GetRoomFlags(5,0x37):x2}/${rom[0xca37]:x2}, visible={essence.Visible}/${rom[s+26]:x2}.");
                FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context+$": collection/script music or ordered cues differ: runtime=[{string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}], native=[{string.Join(',',rom.Sounds)}], state={rom[s+4]}.");
            });
            for (int walk=0;!_dialogue.IsOpen && walk<240;walk++) Step(approach:true);
            // Retail primary text bank uses the encoded high index $04.
            FailIf(!_dialogue.IsOpen || rom[s+4] != 5 || rom[0xcba2] != 0x13 || rom[0xcba3] != 4 ||
                _inventory.Essences != 0x3f || !_saveData.HasRoomFlag(5,0x37,OracleSaveData.RoomFlagItem),
                $"Actual floor approach must execute native essence state4, show TX_0013 and grant only bit$20: text={_dialogue.IsOpen}, state={rom[s+4]}, nativeText=${rom[0xcba1]:x2}:${rom[0xcba2]:x2}:${rom[0xcba3]:x2}, inventory=${_inventory.Essences:x2}, flags=${rom[0xca37]:x2}.");
            Step(8); FailIf(!essence.ReadyForDialogue || !_player.IsHoldingItemTwoHands || essence.SwirlActive,
                "The shared collection pose must wait for text completion.");
            _dialogue.Close(); rom[0xcba0]=0; Step(); Step();
            FailIf(essence.SwirlActive || _roomEvents.Get<DungeonEssenceEvent>().Counter != 0,
                "Native playsound ends the first script dispatch before creating energy or arming its wait.");
            Step();
            FailIf(!essence.SwirlActive || _roomEvents.Get<DungeonEssenceEvent>().Counter != 360 || rom[s+4] != 6,
                "The next eligible native script dispatch must begin the shared two180-update essence waits.");
            Step(360); Step(); Step(20); Step(); Step(20); Step(); Step(40); Step(); Step(28);
            FailIf(IsTransitioning || rom[0xcc4b] != 0,"Essence exit must wait through its last source delay update.");
            Step();
            FailIf(!IsTransitioning || rom[0xcc47] != 0x83 || rom[0xcc48] != 0x0f || rom[0xcc4a] != 0x16 ||
                rom[0xcc4b] != 0x83 || rom[0xcc49] != 1 || rom[0xcc35] != 0,
                "Native essence state7 must publish $83:$0f/$16, respawn-setting transition, delayed fade and music suppression.");

            // Native source warp bytes above establish the endpoint. Continue
            // through the shared runtime transition and placed Maku command
            // host; its common past-confetti implementation is reused.
            void RuntimeStep(int count=1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch);
            for (int wait=0;IsTransitioning && wait<180;wait++) RuntimeStep();
            var remote=_roomEvents.Get<RemoteMakuSixthEssenceEvent>();
            FailIf(IsTransitioning || _rooms.ActiveGroup != 3 || _currentRoom.Id != 0x0f ||
                _player.Position != new Vector2(104,24) || _saveData.RespawnGroup != 3 || _saveData.RespawnRoom != 0x0f ||
                !remote.HasState || remote.Record is not {SubId:1,Var03:8,EssenceMask:0x20,ConfettiKind:RemoteMakuConfettiKind.Past},
                "The actual essence exit must load room$3:$0f/$16 and admit source $8a:$01/v$08.");
            for (int wait=0;!_dialogue.IsOpen && wait<600;wait++) RuntimeStep();
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Queen Ambi",StringComparison.Ordinal) ||
                !_dialogue.CurrentMessage.Contains("save",StringComparison.Ordinal) || !_dialogue.CurrentMessage.Contains("Nayru",StringComparison.Ordinal) ||
                _saveData.MakuMapTextPast != 0xb8 || _saveData.MakuMapTextPresent != 0xb7 || !_hud.StatusBarHidden,
                "Source TX_05b8 must deliver the palace/Nayru handoff and update only past map text under the shared HUD lock.");
            _dialogue.Close();
            for (int wait=0;remote.HasState && wait<180;wait++) RuntimeStep();
            FailIf(remote.HasState || _player.CutsceneControlled || _hud.StatusBarHidden ||
                !_saveData.HasRoomFlag(3,0x0f,0x40) || _saveData.MakuTreeState != 6,
                "The placed Maku handoff must restore input/HUD and persist its room flag and source tree-state increment.");
            LoadValidationRoom(3,0x0f); RuntimeStep(4);
            FailIf(remote.HasState,"Completed Mermaid exit message must not replay on re-entry.");
            LoadValidationRoom(5,0x37); RuntimeStep(3);
            var collected=_entities.Entities<DungeonEssence>().Single();
            FailIf(!collected.Collected || collected.Visible || _entities.Entities<DungeonEssenceGlow>().Count != 0 ||
                _entities.Entities<DungeonEssencePedestal>().Count != 1,"Collected Mermaid Essence must retain only its pedestal.");
            _player.WarpTo(new(120,72)); StepGameplayUpdates(40,Vector2.Up,batched:batch);
            FailIf(_dialogue.IsOpen || _roomEvents.Get<DungeonEssenceEvent>().HasState || _inventory.Essences != 0x3f,
                "Repeating the real pedestal approach must not grant or replay Bereft Peak.");
        }
        GD.Print("Validated clean-US Mermaid Essence through actual floor approach, source motion/collection/text, shared timed script and exit bytes; actual respawn-setting exit, past Maku palace/Nayru handoff and collected re-entry in split/batched gameplay.");
    }
}
