using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed partial class BlackTowerEndingEvent
{
    private int _pendingLinked;
    private bool _linkedWarpAfterObjects;
    internal void AdvanceAfterObjects()
    {
        if(!_linkedWarpAfterObjects)return;
        _linkedWarpAfterObjects=false;
        context.Transitions.BeginRequestedNativeWarp(context.Player);
    }
    private RoomOfRitesRitualDatabase? _linkedFlames;
    private Color[,]? _linkedBackground;
    private IReadOnlyDictionary<int,Color[]>? _linkedSprites;
    internal bool LinkedLeadInActive=>HasState&&Read(0xc2ef) is 0x11 or 0x12 or 0x20;
    private static readonly string[] PhysicalKeys=["attack","item","inventory","map","move_up","move_down","move_left","move_right"];
    private static bool AnyPhysicalKey()=>PhysicalKeys.Any(Input.IsPhysicalActionJustPressed);
    internal void BeginFlameOfSorrow()
    {HasState=true;_initialize=false;_resources.CaptureFullScreenFade();RequestLinkedCutscene(0x11);}
    private void RequestLinkedCutscene(int index){_pendingLinked=index;Write(0xcc04,index);}
    private bool SelectPendingLinkedCutscene()
    {
        if(_pendingLinked==0)return false;
        Write(0xc2ef,_pendingLinked);Write(0xcc04,0);Write(0xcc03,0);_pendingLinked=0;
        SkipsObjectUpdate=true;return true;
    }
    private void SpawnLinked(int id,int subid,Vector2 position)
    {
        if(!context.Entities.InteractionSlotAvailable)return;
        context.Entities.AddEntity(id==0xbc?new TwinrovaKidnapRoomEntity(context,Data,subid,position):
            new LinkedLeadInActorRoomEntity(context,Data,id,subid,position));
    }
    private void SpawnFlame(int subid)
    {
        if(!context.Entities.InteractionSlotAvailable)return;
        var data=(_linkedFlames??=new()).Flame(subid);
        context.Entities.AddEntity(new TwinrovaFlameRoomEntity(context.Rooms.SaveData,context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,subid,data));
    }
    private void SetLinkedRoomScreen(bool fullScreen)
    {
        // gfxRegisterStates $13/$04: SCY=$00, WINY=$c7. hideStatusBar
        // clears the window's tiles but does not move that window on-screen.
        context.Transitions.NativeRoomUsesFullScreen=fullScreen;
        if(fullScreen)context.Hud.Visible=false;
    }
    private void CaptureLinkedPalettes(int header)
    {
        Screens.ApplyPalette(header,context.Rooms.World.BackgroundPalettes,context.Entities);
        _linkedBackground=context.Rooms.World.BackgroundPalettes.Capture();
        _linkedSprites=Enumerable.Range(0,8).ToDictionary(index=>index,index=>
            context.Entities.NativeObjectPalettes?.TryGetValue(index,out var colors)==true?colors:OracleOamCompositor.GetStandardSpritePalette(index));
    }
    private void MaskedFade(bool black,bool fadeOut,int bg,int sprites,int delay=1,int speed=1)=>
        context.Transitions.BeginNativeMaskedFade(black?Colors.Black:Colors.White,fadeOut,bg,sprites,_linkedBackground!,_linkedSprites!,delay,speed);
    private void BlackPalettes(int bg,int sprites)=>FillLinkedPalettes(Colors.Black,bg,sprites);
    private void FillLinkedPalettes(Color color,int bg,int sprites)
    {
        var colors=context.Rooms.World.BackgroundPalettes.Capture();
        for(int palette=0;palette<8;palette++)if((bg&(1<<palette))!=0)for(int shade=0;shade<4;shade++)colors[palette,shade]=color;
        context.Rooms.World.BackgroundPalettes.Restore(colors);
        var objects=new Dictionary<int,Color[]>(_linkedSprites!);
        for(int palette=0;palette<8;palette++)if((sprites&(1<<palette))!=0)objects[palette]=[color,color,color,color];
        context.Entities.SetNativeObjectPalette(objects);
    }
    private bool LinkedDelay(bool text=false,bool palette=false)=>
        !(text&&context.DialogueOpen||palette&&context.Transitions.NativePaletteFadeActive)&&Decrement(0xcbb5);
    private void NextLinked(int delay=0)
    {Write(0xcbb3,Read(0xcbb3)+1);if(delay!=0)Write(0xcbb5,delay);}
    private void LinkedText(int id,int delay=30,int flags=0,int? position=null)
    {context.ShowDialogue(Data.Text(id),position,flags,id);NextLinked(delay);}
    private void LinkedWarp(int group,int room,int position,int arrival,int delay=1,bool beforeObjects=false)
    {
        HasState=false;_linkEnabled=false;_initialize=false;Simulated.SetEnabled(0);
        SetLinkedRoomScreen(false);
        context.Hud.ShowStatusBar();context.RoomView.ClearBackgroundFade();
        context.Transitions.NativeCameraFocus=null;context.Transitions.CutsceneCameraFrozen=false;context.Player.CancelCreditsLink();
        _resources.ReleaseFullScreenFade(restoreColor:false);
        _linkedBackground=null;_linkedSprites=null;
        Write(0xcc50,0);Write(AgesWramAddress.wMenuDisabled,0);Write(0xc2ef,1);
        var warp=new Warp(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,-1,0,0,
            group,room,position,0,arrival,NativeFadeDelay:delay);
        if(beforeObjects){SkipsObjectUpdate=true;context.Transitions.ApplyWarpWithFadeOut(context.Player,warp);}
        else {context.Transitions.RequestNativeFadeOutWarp(warp);_linkedWarpAfterObjects=true;}
    }
    private void AdvanceLinkedLeadIn()
    {
        if(Read(0xcc03)==0)
        {
            ClearTemporaryState();Write(0xcc03,1);
            if(Read(0xc2ef)!=0x20)
            {
                context.Entities.Clear();context.Player.CancelCreditsLink();context.Player.Visible=false;_linkEnabled=false;context.Transitions.CutsceneCameraFrozen=true;
                Write(AgesWramAddress.wDisabledObjects,0);Write(AgesWramAddress.wMenuDisabled,0x80);return;
            }
        }
        switch(Read(0xc2ef))
        {
            case 0x11:AdvanceFlameOfSorrow();break;
            case 0x12:AdvanceZeldaKidnapped();break;
            case 0x20:AdvanceFlameOfDespair();break;
        }
    }
    private void AdvanceFlameOfSorrow()
    {
        switch(Read(0xcbb3))
        {
            case 0:NextLinked(40);return;
            case 1:
                if(!LinkedDelay())return;
                Load(5,0xf1);CaptureLinkedPalettes(0xac);context.Hud.HideStatusBar();SetLinkedRoomScreen(true);
                context.Transitions.SetCutsceneCameraOrigin(new(40,0));Write(AgesWramAddress.wScrollMode,0);
                SpawnFlame(1);context.Sound.PlayNativeSound(SoundId.SndLightning);
                Write(0xcbb5,0);Write(0xcbb6,0);Write(0xcbba,255);NextLinked();goto case 2;
            case 2:
                if(!nativeFlash(5,0xcbb5))return;
                context.Transitions.CancelNativeWhiteFade(clearPresentation:true);NextLinked();return;
            case 3:
                SpawnFlame(0);context.Sound.PlayNativeSound(SoundId.SndCtrlStopMusic);BlackPalettes(0xfd,0xbf);NextLinked(4);return;
            case 4:if(LinkedDelay())LinkedText(0x281b,flags:4);return;
            case 5:
                if(!LinkedDelay(text:true))return;
                MaskedFade(true,false,0,0x10,speed:3);context.Sound.PlayNativeSound(0x72);NextLinked(30);return;
            case 6:
                if(!LinkedDelay(palette:true))return;
                MaskedFade(true,false,255,0xaf);SpawnLinked(0xb0,2,new(0x8e,0x4c));SpawnLinked(0xb0,3,new(0x62,0x4c));
                context.Sound.PlaySound(SoundId.MusDisaster);Write(0xcfc6,0);NextLinked(30);return;
            case 7:if(LinkedDelay(palette:true))LinkedText(0x2829);return;
            case 8:if(LinkedDelay(text:true))LinkedText(0x281c);return;
            case 9:if(LinkedDelay(text:true))LinkedText(0x281d);return;
            case 10:if(LinkedDelay(text:true))LinkedText(0x281e,60);return;
            case 11:
                if(!LinkedDelay(text:true))return;
                Screens.ApplyPalette(0x0f,context.Rooms.World.BackgroundPalettes,context.Entities);
                LinkedWarp(4,0xea,0x87,WarpDestinationTransition.UnknownC,4,beforeObjects:true);return;
            default:throw new NotSupportedException($"flameOfSorrowState1: substate${Read(0xcbb3):x2}.");
        }
    }
    private void KidnapWitches(bool departure)
    {
        Write(AgesWramAddress.wLoadedTreeGfxIndex,1);Write(AgesWramAddress.wInteractionIDToLoadExtraGfx,0xbc);
        SpawnLinked(0xbc,departure?2:0,new(departure?0x68:0x40,departure?0x50:0));
        SpawnLinked(0xbc,departure?3:1,new(departure?0x38:0x60,departure?0x50:0));
    }
    private void KidnapLightning(int index)
    {
        if(context.Entities.PartSlotAvailable)context.Entities.AddEntity(new LightningRoomEntity(context,new(index==0?0x38:0x68,index==0?0x58:0x48),signalCutscene:false));
    }
    private void AdvanceZeldaKidnapped()
    {
        switch(Read(0xcbb3))
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive)return;
                Load(1,0x49);SpawnLinked(0x3c,0x0f,new(0x48,0x48));SpawnLinked(0x31,8,new(0x58,0x48));SpawnLinked(0xad,9,new(0x50,0x38));
                SpawnLinked(0x3a,0x0e,new(0x38,0x48));SpawnLinked(0x43,7,new(0x78,0x28));
                CaptureLinkedPalettes(0xa2);
                context.Transitions.BeginNativeWhiteFade(false);NextLinked(60);return;
            case 1:if(LinkedDelay(palette:true)){Write(0xcfc0,Read(0xcfc0)|1);NextLinked(1);}return;
            case 2:if((Read(0xcfc0)&2)!=0&&LinkedDelay()){KidnapLightning(0);NextLinked(30);}return;
            case 3:if(LinkedDelay()){KidnapPuff(0);Write(0xcfc0,Read(0xcfc0)|4);NextLinked(30);}return;
            case 4:if(LinkedDelay()){KidnapLightning(1);NextLinked(30);}return;
            case 5:if(LinkedDelay()){KidnapPuff(1);Write(0xcfc0,Read(0xcfc0)|8);NextLinked(30);}return;
            case 6:if((Read(0xcfc0)&16)!=0)NextLinked(30);return;
            case 7:if(LinkedDelay()){Write(0xcfc0,Read(0xcfc0)|32);NextLinked(40);}return;
            case 8:if(LinkedDelay())LinkedText(0x281f,90);return;
            case 9:
                if(LinkedDelay(text:true)){Write(0xcfc0,Read(0xcfc0)|128);NextLinked(60);}
                else if(Read(0xcbb5)==60)Write(0xcfc0,Read(0xcfc0)|64);return;
            case 10:
                if(!LinkedDelay())return;
                KidnapWitches(false);context.Sound.PlaySound(SoundId.MusDisaster);Write(0xcfc0,0);Write(0xcfc6,0);NextLinked(30);return;
            case 11:if((Read(0xcfc0)&1)!=0&&LinkedDelay())LinkedText(0x2820);return;
            case 12:if(LinkedDelay(text:true))LinkedText(0x2821);return;
            case 13:if(LinkedDelay(text:true))LinkedText(0x2822);return;
            case 14:if(LinkedDelay(text:true)){Write(0xcfc0,Read(0xcfc0)&~1);NextLinked();}return;
            case 15:
                if((Read(0xcfc0)&1)==0)return;
                context.Sound.PlayNativeSound(SoundId.SndLightning);Write(0xcbb4,0);NextLinked();goto case 16;
            case 16:
                if(!AdvanceKidnapFlashes())return;
                context.Entities.ClearDynamicInteractions();Write(0xcfc0,Read(0xcfc0)&~1);Write(0xcfc6,0);NextLinked(4);return;
            case 17:
                if(!LinkedDelay())return;
                KidnapWitches(true);SpawnLinked(0x3a,0x0e,new(0x38,0x48));SpawnLinked(0x43,7,new(0x78,0x28));
                context.Entities.SetNativeObjectPalette(_linkedSprites);
                context.Transitions.BeginNativeWhiteFade(false,delay:4);NextLinked(30);return;
            case 18:if(LinkedDelay(palette:true))LinkedText(0x2823);return;
            case 19:if(LinkedDelay(text:true))LinkedText(0x2824);return;
            case 20:if(LinkedDelay(text:true)){context.Sound.PlayNativeSound(SoundId.SndBeam2);Write(0xcfc0,Read(0xcfc0)|1);NextLinked(90);}return;
            case 21:
                if(!LinkedDelay())return;
                Write(0xcbba,255);context.Sound.PlayNativeSound(SoundId.SndLightning);context.Sound.PlayNativeSound(SoundId.SndCtrlStopMusic);NextLinked();return;
            case 22:
                if(!nativeFlash(2,0xcbb5))return;
                context.Rooms.SaveData.SetGlobalFlag(AgesGlobalFlag.FlameOfDespairLit);Write(AgesWramAddress.wMenuDisabled,0);RequestLinkedCutscene(0x20);return;
            default:throw new NotSupportedException($"zeldaKidnappedState1: substate${Read(0xcbb3):x2}.");
        }
    }
    private void KidnapPuff(int index)
    {
        SpawnLinked(0x6b,0x16,new(index==0?0x38:0x68,index==0?0x58:0x48));
    }
    private bool AdvanceKidnapFlashes()
    {
        int state=Read(0xcbb4);
        if(state==0||state==3)
        {Write(0xcbb5,state==0?10:20);FillLinkedPalettes(Colors.White,255,255);Write(0xcbb4,state+1);return false;}
        if(state==5)return LinkedDelay(palette:true);
        if(!LinkedDelay())return false;
        Write(0xcbb5,state==4?30:10);context.Transitions.BeginNativeWhiteFade(true,speed:3);Write(0xcbb4,state+1);return false;
    }
}
