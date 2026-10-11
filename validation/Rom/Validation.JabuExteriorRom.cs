using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuExteriorOpeningRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(WramAddress.wEssencesObtained,0);
            _saveData.SetGlobalFlag(0x31,false);_saveData.SetRoomFlag(2,0x90,2,false);
            LoadValidationRoom(2,0x90);_player.WarpTo(new(0x50,0x68));
            SomariaRom? rom=null;
            int randomCalls=0;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                if(rom is null)return;
                if(rom[0xc2ef]==0x1d)rom.AdvanceJabuOpening();
                rom.AdvanceParts(_entities.FrameCounter);rom.AdvanceInteractions(_entities.FrameCounter);
                rom.AdvanceTileGraphics();
                rom.AdvanceScreenShake();
                rom.SelectTriggeredCutscene();
                var cutscene=_roomEvents.Get<JabuExteriorEvent>();
                var idle=_entities.EntityAdapters<JabuExteriorActorRoomEntity>().Single(actor=>actor.SubId==9);
                FailIf(_entities.InteractionSlot(idle)!=2 || idle.State!=rom[0xd244] ||
                    idle.Phase!=rom[0xd245] || idle.Counter!=rom[0xd246],
                    $"Jabu $dc:$09 slot/idle boundary differs at frame {_entities.FrameCounter}.");
                foreach(var spawner in _entities.EntityAdapters<JabuBubbleSpawnerRoomEntity>())
                    FailIf(_entities.InteractionSlot(spawner)!=3 || rom[0xd341]!=0x97,"Jabu $97:$01 did not reuse the deleted $dc:$0a slot.");
                FailIf(cutscene.Opening!=(rom[0xc2ef]==0x1d) || cutscene.Opening &&
                    (cutscene.OpeningState!=rom[0xcc03] || cutscene.OpeningPhase!=rom[0xcbb3] || cutscene.Counter!=rom[0xcbb4]),
                    $"Jabu opening ROM boundary differs: batch={batched} frame={_entities.FrameCounter}, port={cutscene.Opening}:{cutscene.OpeningState}/{cutscene.OpeningPhase}/{cutscene.Counter}, native=${rom[0xc2ef]:x2}:{rom[0xcc03]}/{rom[0xcbb3]}/{rom[0xcbb4]}, trigger=${rom[0xcc04]:x2}, slots=${rom[0xd240]:x2}/${rom[0xd340]:x2}.");
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=rom[0xcc02],
                    "Jabu opening native menu/object masks differ.");
                var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
                var bubbles=_entities.EntityAdapters<JabuBubbleRoomEntity>().OrderBy(bubble=>slots[bubble]).ToArray();
                int[] native=Enumerable.Range(0,16).Select(index=>0xd0c0+index*0x100).Where(slot=>rom[slot]!=0&&rom[slot+1]==0x16).ToArray();
                FailIf(bubbles.Length!=native.Length,"Jabu native bubble allocation/deletion differs.");
                for(int index=0;index<bubbles.Length;index++)
                    FailIf(SomariaPrivate<Vector2>(bubbles[index],"_position")!=new Vector2(rom.Word(native[index]+12)/256f,rom.Word(native[index]+10)/256f),
                        $"Jabu bubble {index} motion differs: port={bubbles[index].Position}, ROM={rom.Word(native[index]+12)/256f},{rom.Word(native[index]+10)/256f}.");
                var random=_random.CaptureState();
                FailIf(random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95],$"Jabu eye/bubble shared RNG differs at frame {_entities.FrameCounter} state={cutscene.OpeningState}/{cutscene.OpeningPhase}/{cutscene.Counter}: port={random.Rng1:x2}/{random.Rng2:x2}, native={rom[0xff94]:x2}/{rom[0xff95]:x2}, calls={random.Calls-randomCalls}/{rom.RandomCalls}.");
                foreach(int packed in new[]{0x22,0x23,0x24,0x25,0x26,0x27,0x32,0x33,0x34,0x35,0x36,0x37})
                    FailIf(_currentRoom.GetMetatile(new((packed&15)*16+8,(packed>>4)*16+8))!=rom[0xcf00+packed],
                        $"Jabu eye tile ${packed:x2} differs at frame {_entities.FrameCounter}: port=${_currentRoom.GetMetatile(new((packed&15)*16+8,(packed>>4)*16+8)):x2}, native=${rom[0xcf00+packed]:x2}.");
            });
            var exterior=_roomEvents.Get<JabuExteriorEvent>();
            rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0x50,0x68);
            randomCalls=_random.Calls;rom[0xc2ef]=1;
            rom[0xcc2f]=0x90;rom.LoadRoomTileset();
            rom[0xd240]=1;rom[0xd241]=0xdc;rom[0xd242]=9;
            rom[0xd340]=1;rom[0xd341]=0xdc;rom[0xd342]=0x0a;
            Step(3);FailIf(exterior.Opening,"Jabu opened without permission $31.");
            Step(240);
            _saveData.SetGlobalFlag(0x31);Step(3);FailIf(exterior.Opening,"Deleted $dc:$0a trigger revived without room re-entry.");
            LoadValidationRoom(2,0x90);_player.WarpTo(new(0x50,0x68));
            rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0x50,0x68);
            randomCalls=_random.Calls;
            rom[0xc2ef]=1;
            rom[0xcc2f]=0x90;rom.LoadRoomTileset();
            rom[0xd240]=1;rom[0xd241]=0xdc;rom[0xd242]=9;
            rom[0xd340]=1;rom[0xd341]=0xdc;rom[0xd342]=0x0a;
            Step(2);FailIf(!exterior.Opening||_saveData.HasRoomFlag(2,0x90,2),"Jabu trigger skipped its initialization/next-cutscene boundary.");
            Step(1);FailIf(exterior.Counter!=4||!_saveData.HasRoomFlag(2,0x90,2)||!exterior.DisablesLink,"Jabu state 0 lost counter $04, room bit $02 or input ownership.");
            Vector2 link=_player.Position;Step(124);
            FailIf(exterior.OpeningPhase!=3||exterior.Counter!=60||_entities.EntityAdapters<JabuBubbleRoomEntity>().Count()!=12||_player.Position!=link,
                "Jabu opening lost 4/60/60 timing, initial twelve bubbles or Link freeze.");
            Step(60);FailIf(exterior.OpeningState!=2||exterior.Counter!=60,"Jabu second mouth frame lost its 60-update boundary.");
            Step(59);FailIf(!exterior.Opening,"Jabu completed one update early.");
            Step(1);FailIf(exterior.Opening||exterior.DisablesLink||_entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0||
                _rooms.CurrentRoom.GetTerrainInfo(new(0x48,0x58)).Collision!=0,"Jabu completion did not release input and load open-mouth collisions.");
            rom=null;
            LoadValidationRoom(2,0x90);Step(3);FailIf(exterior.Opening,"Jabu opening repeated after completion.");
            _saveData.SetRoomFlag(2,0x90,2,false);LoadValidationRoom(2,0x90);Step(3);exterior.Cancel();
            FailIf(exterior.Opening||_entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0,"Jabu cancellation retained menu/object masks.");
            // Destination state zero runs during scrolling, then stays frozen.
            // Re-entry after opening must retire $0a before event binding.
            foreach(bool opened in new[]{false,true})
            {
                _saveData.SetGlobalFlag(0x31,false);_saveData.SetRoomFlag(2,0x90,2,opened);
                LoadValidationRoom(2,0x91);
                var destination=_rooms.GetRoom(2,0x90);
                var nativeScroll=new SomariaRom(_saveData,_random.CaptureState(),destination,0,0x98,0x68);
                nativeScroll[0xcd00]=8;
                nativeScroll[0xd240]=1;nativeScroll[0xd241]=0xdc;nativeScroll[0xd242]=9;
                nativeScroll[0xd340]=1;nativeScroll[0xd341]=0xdc;nativeScroll[0xd342]=0x0a;
                _entities.BeginScreenTransition(2,destination,new(-160,0),_player);
                nativeScroll.AdvanceInteractions(_entities.FrameCounter);
                var incoming=_entities.EntityAdapters<JabuExteriorActorRoomEntity>().ToArray();
                FailIf(incoming.Count(actor=>actor.SubId==0x0a)!=(nativeScroll[0xd340]!=0?1:0)||
                    incoming.Single(actor=>actor.SubId==9).State!=nativeScroll[0xd244],
                    "Jabu incoming state-zero deletion/init differs from native scroll admission.");
                var seed=_random.CaptureState();
                _entities.Update(8.0/60.0,_player);
                var after=_random.CaptureState();
                FailIf(incoming.Single(actor=>actor.SubId==9).State!=1||after.Rng1!=seed.Rng1||
                    after.Rng2!=seed.Rng2||after.Calls!=seed.Calls,
                    "Jabu destination animation advanced before scrolling completed.");
                _entities.FinishScreenTransition();
            }
        }
    }


}
