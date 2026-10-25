using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNayruRescueWarpRom()
    {
        foreach(bool linked in new[]{false,true})
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(linked);_saveData.SetMakuTreeState(9);
            LoadValidationRoom(5,0xce);_inventory.EquipA(0);_inventory.EquipB(0);_entities.Clear();
            _player.WarpTo(Vector2.Zero,recordSafe:false);
            var owner=_roomEvents.Get<NayruRescueWarpEvent>();owner.Begin();
            var random=_random.CaptureState();
            var rom=new SomariaRom(_saveData,random,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc01]=(byte)(linked?1:0);rom[0xc2ef]=0x0c;rom[0xcc03]=0;
            var camera=rom.CreateMenuView();
            int update=0,generation=0,hold=0,texts=0,nativePressed=0,nativeHeld=0,nativeAngle=0xff;
            var actualTexts=new List<int>();
            // mainScripts.nayruScript02, ralphSubid04Script, makuTree_subid03Script,
            // scriptHelp.cloakedTwinrova/twinrova, and treasureObjectData $27:$00.
            int[] expected=linked
                ? [0x2106,0x0953,0x2e0e,0x2107,0x2109,0x040b,0x2c02,0x2c03,0x2c04,0x2c05,0x2c06,0x2c07,0x2e10,0x2108,0x0957,0x0955,0x0956]
                : [0x2106,0x0953,0x2e0e,0x2107,0x2109,0x040b,0x2c00,0x2c01,0x2e0f,0x2108,0x0954,0x0955,0x0956];
            void Compare()
            {
                rom.AdvanceWarpPalette();
                if(rom[0xc2ef]==0x0c)rom.AdvanceNayruRescueWarp();
                rom.UpdateGameplay(nativePressed,nativeHeld,nativeAngle,++update);nativePressed=0;
                camera.UpdateScreenShake();
                FailIf(RandomDiffers(random, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Nayru rescue shared RNG differs update={update}: port={_random.CaptureState().Calls-random.Calls}, ROM={rom.RandomCalls}.");
                FailIf(_dialogue.IsOpen!=(rom[0xcba0]!=0),$"Nayru rescue textbox timing differs update={update}: port={_dialogue.IsOpen}, ROM={rom[0xcba0]}.");
                FailIf(owner.State!=rom[0xcc03] || _rooms.ActiveGroup!=rom[0xcc2d] || _rooms.CurrentRoom.Id!=rom[0xcc30] ||
                    _entities.RuntimeState.ReadWramByte(0xcfd0)!=rom[0xcfd0],
                    $"Nayru rescue warp update={update},linked={linked}: state {owner.State}/{rom[0xcc03]}, room {_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}, signal {_entities.RuntimeState.ReadWramByte(0xcfd0)}/{rom[0xcfd0]}.");
                var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())
                {
                    int address=0xd040+slots[actor]*256;
                    FailIf(rom[address+1]!=actor.Id || rom[address+2]!=actor.SubId || rom[address+4]!=actor.State || rom[address+5]!=actor.Substate ||
                        actor.PrecisePosition!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) || actor.ZFixed!=unchecked((short)rom.Word(address+14)),
                        $"Nayru rescue actor ${actor.Id:x2}:${actor.SubId:x2} update={update} slot${slots[actor]:x2}: port {actor.State}/{actor.Substate}/{actor.PrecisePosition}/{actor.ZFixed}; ROM ${rom[address+1]:x2}:${rom[address+2]:x2} {rom[address+4]}/{rom[address+5]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{unchecked((short)rom.Word(address+14))}.");
                }
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),$"Nayru rescue Link differs update={update}: {_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, terrain={_terrain.GetTerrainInfo(_player.Position).Type}, native tile/type/collisions=${rom[0xcc9a]:x2}/${rom[0xcc9c]:x2}/{rom[0xcc33]}, state={rom[0xd004]}/{rom[0xd005]}.");
                if(generation!=rom.TextGeneration)
                {
                    int expectedId=texts<expected.Length?expected[texts]:(texts-expected.Length)%2==0?0x0955:0x0956;
                    FailIf(rom.Word(0xcba2)!=expectedId,$"Nayru rescue text {texts}: ROM=${rom.Word(0xcba2):x4}, expected=${expectedId:x4}.");
                    if(expectedId!=0x040b)FailIf(_dialogue.SourceTextId!=expectedId-0x0400,$"Nayru rescue port source text differs at message {texts}.");
                    generation=rom.TextGeneration;actualTexts.Add(rom.Word(0xcba2));texts++;hold=3;
                }
                if(hold>0&&--hold==0) {_dialogue.Close();rom[0xcba0]=0;}
            }
            for(int limit=0;limit<4200&&owner.HasState;limit+=2)StepGameplayUpdates(2,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(owner.HasState || !_saveData.HasGlobalFlag(0x11) || !_saveData.HasTreasure(TreasureId.TuneOfAges) || _saveData.MakuTreeState!=10 || texts!=(linked?17:13),
                $"Nayru rescue reward/handoff incomplete: state={owner.State},text={string.Join(',',actualTexts.Select(id=>id.ToString("x4")))}.");
            FailIf((rom[0xc6d2]&2)==0 || (rom[0xc69e]&0x80)==0 || rom[0xc6e8]!=10 ||
                _saveData.MakuMapTextPresent!=0x56 || _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0 ||
                _entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)!=0,
                "Nayru rescue must publish SavedNayru$11, TuneOfAges$27, Maku state10/map$56 and release controls.");
            nativeHeld=0x40;nativeAngle=0;
            StepGameplayUpdates(26,Vector2.Up,["move_up"],batched:batch,afterUpdate:Compare);
            nativeHeld=0;nativeAngle=0xff;
            var maku=_entities.EntityAdapters<NayruRescueActorRoomEntity>().Single(actor=>actor.Id==0x87);
            FailIf(!maku.Actor.CanTalkTo(_player),"Rescued Maku dialogue must be reachable from Link's arrival through the room's collision geometry.");
            for(int repeat=0;repeat<2;repeat++)
            {
                rom[0xd071+_entities.InteractionSlot(maku.Node)*256]=1;nativePressed=nativeHeld=1;
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);nativeHeld=0;
                StepGameplayUpdates(80,Vector2.Zero,batched:batch,afterUpdate:Compare);
            }
            FailIf(texts!=expected.Length+4 || _saveData.MakuTreeState!=10,
                "Maku must repeat its two-message source advice without repeating the rescue reward.");
        }
        ReinitializeGameplayForValidation();LoadValidationRoom(5,0xce);
        _roomEvents.Get<NayruRescueWarpEvent>().Begin();StepGameplayUpdates(10,Vector2.Zero);
        LoadValidationRoom(1,0x36);StepGameplayUpdates(2,Vector2.Zero);
        FailIf(_roomEvents.Get<NayruRescueWarpEvent>().HasState || _player.CutsceneControlled || !_player.Visible ||
            _entities.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)!=0 || _saveData.HasGlobalFlag(0x11),
            "Cancelled Nayru rescue must release its player/fade ownership without publishing completion.");
    }
}
