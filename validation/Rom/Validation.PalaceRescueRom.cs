using Godot;
using System.Linq;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceRescueRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0xce);
            _inventory.EquipA(0); _inventory.EquipB(0);
            _entities.Clear(); _entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,1);
            _entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,1);
            _player.WarpTo(Vector2.Zero,recordSafe:false); _entities.AddPalaceRescueActor();
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay(); rom[0xcc8a]=rom[0xcbca]=1;
            rom[0xd240]=1; rom[0xd241]=0x6e; rom[0xcc08]=0xd0;
            int update=0,generation=0,texts=0,hold=0,maxGuards=0;
            var publication = new InteractionMovementPublication(_entities, _runtimeState);
            int[] expected=[0x1708,0x2104,0x1709,0x5a0c,0x5a0d,0x2e0c,0x2e0d,0x2105,0x170a];
            void Compare()
            {
                rom.AdvanceWarpPalette();
                if(rom[0xc2ef]==0x0c)rom.AdvanceNayruRescueWarp();
                rom.UpdateGameplay(0,0,0xff,++update,
                    beforeParts: () => InteractionMovementPublication.Seed(rom),
                    afterInteractions: () => { if (_rooms.ActiveGroup == 5) publication.Compare(rom, "Palace rescue $6e"); });
                var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
                var actors=_entities.EntityAdapters<PalacePossessionRoomEntity>().ToArray();
                maxGuards=System.Math.Max(maxGuards,actors.Count(actor=>actor.SubId==4));
                foreach(var actor in actors)
                {
                    int address=0xd040+slots[actor]*256;
                    FailIf(rom[address]==0 || rom[address+1]!=0x6e || actor.SubId!=rom[address+2] || actor.State!=rom[address+4] ||
                        actor.PrecisePosition!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) || actor.ZFixed!=unchecked((short)rom.Word(address+14)),
                        $"Palace rescue INTERAC$6e:${actor.SubId:x2} slot${slots[actor]:x2} differs update={update}: port={actor.State}/{actor.PrecisePosition}/{actor.ZFixed}, speed/angle={SomariaPrivate<int>(actor,"_speed"):x2}/{SomariaPrivate<int>(actor,"_angle"):x2}; ROM={rom[address+4]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{unchecked((short)rom.Word(address+14))}, speed/angle/index={rom[address+0x10]:x2}/{rom[address+9]:x2}/{rom[address+3]:x2}.");
                }
                FailIf(_entities.RuntimeState.ReadWramByte(0xcfd0)!=rom[0xcfd0] || _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Palace rescue signal or Link movement differs update={update}: signal={_entities.RuntimeState.ReadWramByte(0xcfd0)}/{rom[0xcfd0]}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                if(generation!=rom.TextGeneration)
                {
                    FailIf(texts>=expected.Length || rom.Word(0xcba2)!=expected[texts] || !_dialogue.IsOpen,
                        $"Palace rescue text {texts} differs: ROM=${rom.Word(0xcba2):x4}.");
                    generation=rom.TextGeneration; texts++;hold=3;
                }
                if(hold>0 && --hold==0) { _dialogue.Close();rom[0xcba0]=0; }
                rom.SelectTriggeredCutscene();
            }
            for(int limit=0;limit<2200&&!_saveData.HasGlobalFlag(0x19);limit+=2)
                StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!_saveData.HasGlobalFlag(0x19) || texts!=expected.Length || maxGuards!=6 || rom[0xc2ef]!=0x0c,
                $"Palace rescue must finish its nine source messages and six guard lanes before triggering CUTSCENE$0c: texts={texts}, guards={maxGuards}, trigger=${rom[0xcc04]:x2}.");
            FailIf(publication.Publications < 200, "Palace rescue linear/circular movement publication was not exercised.");
            StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!_roomEvents.Get<NayruRescueWarpEvent>().HasState || _rooms.ActiveGroup!=0 || _rooms.CurrentRoom.Id!=0x38,
                "Palace rescue must transfer to CUTSCENE$0c and its Maku room on the following gameplay update.");
        }
    }
}
