using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedFlameOfSorrowRom()=>VerifyLinkedLeadInRom(0x11);
    private void ValidateZeldaKidnappingRom()=>VerifyLinkedLeadInRom(0x12);
    private void ValidateFlameOfDespairRom()=>VerifyLinkedLeadInRom(0x20);
    private void ValidatePreRitesPalaceRom()=>VerifyLinkedLeadInRom(0x20,start:9,stop:13);
    private void ValidatePreRitesOracleWarpRom()=>VerifyLinkedLeadInRom(0x20,start:12,stop:23);
    private void ValidateLinkedLeadInCancellation()
    {
        foreach(var phase in new[]{(0x11,1),(0x11,60),(0x12,2),(0x12,180),(0x20,1),(0x20,45)})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);LoadValidationRoom(4,0xd4);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.BeginFlameOfSorrow();
            typeof(BlackTowerEndingEvent).GetField("_pendingLinked",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,phase.Item1);
            StepGameplayUpdates(phase.Item2,Vector2.Zero);
            owner.Cancel();owner.Cancel();StepGameplayUpdates(1,Vector2.Zero);
            FailIf(owner.HasState||_player.CutsceneControlled||_entities.PlayerMenusDisabled||_dialogue.IsOpen||
                _transitions.NativePaletteFadeActive||_transitions.CutsceneCameraFrozen||_transitions.NativeRoomUsesFullScreen||_runtimeState.ReadWramByte(0xcc04)!=0||
                _saveData.HasGlobalFlag(GlobalFlag.FlameOfDespairLit)||
                _entities.EntityAdapters<LinkedLeadInActorRoomEntity>().Any()||_entities.EntityAdapters<TwinrovaKidnapRoomEntity>().Any()||
                _entities.EntityAdapters<TwinrovaFlameRoomEntity>().Any()||_entities.EntityAdapters<LightningRoomEntity>().Any(),
                $"Cancel linked${phase.Item1:x2} update{phase.Item2} retained dialogue, fade, actors or control, or published completion.");
        }
    }
    private void VerifyLinkedLeadInRom(int cutscene,int start=0,int stop=9)
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);LoadValidationRoom(4,0xd4);_entities.Clear();
            if(cutscene==0x11)_saveData.SetRoomFlag(4,0xea,0x40);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x60,0x48));_transitions.ResetCamera();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.BeginFlameOfSorrow();
            typeof(BlackTowerEndingEvent).GetField("_pendingLinked",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,0);
            _runtimeState.SetWramByte(0xcc04,0);_runtimeState.SetWramByte(0xc2ef,(byte)cutscene);_runtimeState.SetWramByte(0xcc03,0);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x60,0x48)
                {CompanionDispatchEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom.InitializeLinkGameplay();rom[0xcd17]=0xd0;
            rom[0xcbe4]=(byte)_statusBar.DisplayedHealth;
            rom[0xcc01]=1;rom[0xc2ef]=(byte)cutscene;rom[0xcc03]=0;
            rom.LatchDisplayedObjectPalettes();
            if(start!=0)
            {
                foreach(var pair in new[]{(0xcc03,1),(0xcbc1,start),(0xcbb3,1)})
                {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
                Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            }
            int update=0,generation=0,hold=0;bool printingComplete=false;var sounds=_sound.AttachPlayRequestAudit();
            var checkedSprites=new System.Collections.Generic.HashSet<(int,int,int,int)>();
            // Isolate each cutscene and its immediate handoff. CUTSCENE $20's
            // other room/Link scenes are separate fixtures below.
            int limit=cutscene==0x12?2200:1400;
            while(update<limit&&owner.HasState&&(cutscene!=0x20||owner.Stage<stop)&&
                (cutscene!=0x12||_runtimeState.ReadWramByte(0xcc04)==0))
            {
                bool nextPage=cutscene==0x20&&owner.Stage==22&&_dialogue.IsPageComplete&&_dialogue.HasNextMessage;
                printingComplete=owner.Stage==22&&_dialogue.PrintingComplete;
                StepGameplayUpdates(cutscene==0x20&&owner.Stage>=stop-1||cutscene==0x11&&_runtimeState.ReadWramByte(0xcbb3)>=10||cutscene==0x12&&_runtimeState.ReadWramByte(0xcbb3)>=21?1:3,
                    Vector2.Zero,pressed:nextPage?["attack"]:[],batched:batched,afterUpdate:Compare);
            }
            _sound.SetRequestObserver(null);
            FailIf(update>=limit,$"Linked cutscene${cutscene:x2} stalled at {owner.Stage:x2}/{_runtimeState.ReadWramByte(0xcbb3):x2}.");
            if(cutscene==0x12)FailIf(!_saveData.HasGlobalFlag(GlobalFlag.FlameOfDespairLit)||rom[0xcc04]!=0x20,
                "Zelda kidnapping must publish GLOBALFLAG $3a and CUTSCENE $20 only after its final flash.");
            if(cutscene==0x11)
            {
                int wait=0;while(IsTransitioning&&wait++<260)StepGameplayUpdates(1,Vector2.Zero);
                FailIf(IsTransitioning||_rooms.ActiveGroup!=4||_currentRoom.Id!=0xea||!_saveData.HasRoomFlag(4,0xea,0x40),
                    "Flame of Sorrow must return to the opened Black Tower door $4:ea/$87 without replaying the seed.");
            }
            if(cutscene==0x20&&stop==23)
            {
                int wait=0;while(IsTransitioning&&wait++<260)StepGameplayUpdates(1,Vector2.Zero);
                FailIf(IsTransitioning||_rooms.ActiveGroup!=5||_currentRoom.Id!=0xf4||
                    _player.CutsceneControlled||_entities.PlayerMenusDisabled||_transitions.CutsceneCameraFrozen||_transitions.NativeRoomUsesFullScreen,
                    $"The oracle farewell must arrive in Room of Rites $5:f4/$57 with gameplay and camera ownership released: room{_rooms.ActiveGroup:x}:{_currentRoom.Id:x2},transition{IsTransitioning},control{_player.CutsceneControlled},menu{_entities.PlayerMenusDisabled},camera{_transitions.CutsceneCameraFrozen}.");
            }
            owner.Cancel();owner.Cancel();
            FailIf(owner.HasState||_entities.EntityAdapters<LinkedLeadInActorRoomEntity>().Any()||
                _entities.EntityAdapters<TwinrovaKidnapRoomEntity>().Any(),"Linked lead-in cancellation retained actor slots or event ownership.");
            void Compare()
            {
                if(cutscene==0x20&&rom[0xcbc1]==22)rom[0xcba0]=printingComplete?(byte)0x80:(byte)1;
                rom[0xcc00]=(byte)_entities.FrameCounter;rom.AdvanceWarpPalette();rom.AdvanceLinkedLeadInBody();
                if(cutscene!=0x11||rom[0xcc4b]==0)rom.UpdateGameplay(0,0,255,_entities.FrameCounter);
                if(cutscene==0x20&&rom[0xcc4b]!=0)rom.ApplyRequestedWarp();
                // bank1.func_4000 skips camera-position updates when the
                // native cutscene has disabled scrolling entirely.
                if(rom[WramAddress.wScrollMode]!=0)rom.AdvanceCameraPosition();
                rom.AdvanceScreenShake();rom.LatchDisplayedObjectPalettes();update++;
                if(owner.HasState&&(cutscene==0x11&&_runtimeState.ReadWramByte(0xcbb3)>=2||cutscene==0x20&&owner.Stage!=0))
                {
                    // The native window registers determine whether a status
                    // strip exists; hiding its tiles alone is insufficient.
                    bool fullScreen=rom[0xc488]>=144;
                    FailIf(_transitions.NativeRoomUsesFullScreen!=fullScreen||_hud.Visible==fullScreen,
                        $"Linked${cutscene:x2} update{update}: HUD aperture differs from native WINY=${rom[0xc488]:x2}.");
                    int fieldTop=fullScreen||!_presentationSettings.HudBottom?16:0;
                    int debugTop=fullScreen?0:fieldTop;
                    FailIf(_roomDebug.Position.Y!=debugTop,
                        $"Linked${cutscene:x2} update{update}: room display origin must be y={debugTop}, got{_roomDebug.Position.Y}.");
                    FailIf(_scene.RoomCamera.Position.Y!=rom[0xffaa]+72-fieldTop,
                        $"Linked${cutscene:x2} update{update}: camera presentation must render native y=${rom[0xffaa]:x2} at field top {fieldTop}.");
                }
                foreach(int address in new[]{0xcc03,0xcbb3,0xcbb4,0xcbb5,0xcbb6,0xcbc1,0xcbc2,0xcfc0,0xcfc6,0xcfd5,0xcfd6,WramAddress.wDisabledObjects,WramAddress.wMenuDisabled})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],
                        $"Linked${cutscene:x2} update{update},byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}, room{_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
                // This bounded native fixture omits the text thread. The port
                // still prints/pages through its normal UI; exclude its glyph sounds.
                FailIf(_rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||
                    RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||
                    !sounds.Requests.Where(sound=>sound!=SoundId.SndText&&sound!=SoundId.SndText2).SequenceEqual(rom.Sounds),
                    $"Linked${cutscene:x2} update{update}: room/RNG/audio differs, calls{_random.Calls-seed.Calls}/{rom.RandomCalls}, shake{_entities.ScreenShakeCounter}/{rom[0xcd18]}, {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                foreach(var actor in _entities.EntityAdapters<LinkedLeadInActorRoomEntity>())CheckActor(actor.Actor);
                foreach(var actor in _entities.EntityAdapters<TwinrovaKidnapRoomEntity>())CheckActor(actor.Actor);
                foreach(var actor in _entities.EntityAdapters<RoomOfRitesZeldaActor>())CheckActor((NpcCharacter)actor.Node);
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0)
                {
                    if(cutscene==0x20&&owner.Stage==22)rom[0xcba0]=0x80;
                    else {_dialogue.Close();rom[0xcba0]=0;}
                }
            }
            void CheckActor(NpcCharacter actor)
            {
                int at=0xd040+_entities.InteractionSlot(actor)*256;
                FailIf(rom[at]==0||actor.Record.Id!=rom[at+1]||actor.Record.SubId!=rom[at+2]||
                    actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.CurrentAnimationCounter!=rom[at+0x20]||
                    actor.CurrentAnimationParameter!=rom[at+0x21],
                    $"Linked${cutscene:x2} update{update} actor${actor.Record.Id:x2}:{actor.Record.SubId:x2},slot${at:x4}: position/animation differs ({actor.Position}/{rom[at+13]},{rom[at+11]}), counter{actor.CurrentAnimationCounter}/{rom[at+0x20]}.");
                int pointer=rom.Word(at+0x1e)&0x3fff;
                if(_transitions.NativeRoomUsesFullScreen)CompareFullscreenActorPositionRom(rom,actor,at,$"Linked${cutscene:x2} actor${actor.Record.Id:x2}:{actor.Record.SubId:x2}");
                if(rom[0xc4ab]==0&&rom[WramAddress.wLoadedTreeGfxIndex]==0&&checkedSprites.Add((actor.Record.Id,actor.Record.SubId,pointer,rom[0xcbc1])))
                {
                    var frames=SomariaPrivate<System.Collections.Generic.List<NpcCharacterAnimationFrame>>(actor,"_scriptAnimation");
                    var frame=frames[actor.CurrentAnimationFrame];
                    int cells=ValidationRom.LoadCleanUs().Span[0x14*0x4000+pointer];
                    CompareInteractionSpritePixelsRom(rom,frame.Texture,frame.Offset,at,cells,$"Linked${cutscene:x2} update{update} stage{rom[0xcbc1]} actor${actor.Record.Id:x2}:{actor.Record.SubId:x2}, sources{rom[0xffa9]:x2}",displayedPalette:true,displayFade:_warpFade.Color);
                }
            }
        }
    }
}
