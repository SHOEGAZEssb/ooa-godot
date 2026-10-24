using Godot;
using System;
using System.Linq;

namespace oracleofages;

internal sealed partial class BlackTowerEndingEvent
{
    private void DespairText(int id,int counter,int flags=8,int? position=3)
    {Write(0xcbc1,Stage+1);Write(0xcbb3,counter);context.ShowDialogue(Data.Text(id),position,flags,id);}
    private bool DespairDelay(bool text=false,bool palette=false)=>
        !(text&&context.DialogueOpen||palette&&context.Transitions.NativePaletteFadeActive)&&Decrement(0xcbb3);
    private void DespairShake()
    {
        // @func_5995 consumes two ordered draws for SCY/SCX, without
        // changing hCameraY/X or object world coordinates.
        var table=Data.Table("shake");
        int y=unchecked((sbyte)table[context.Entities.NextRandomValue()&7]);
        int x=unchecked((sbyte)table[context.Entities.NextRandomValue()&7]);
        context.RoomView.SetNativeScrollOffset(new(x,y));
    }
    private void AdvanceFlameOfDespair()
    {
        switch(Stage)
        {
            case 0:
                Write(0xcfde,0x0b);var room=Data.RoomSet(0x0b);Load(room.Group,room.Room);
                foreach(var actor in Data.Placements(room.Scene))
                {
                    if(actor.Id==0xa9)SpawnFlame(actor.SubId);
                    else if(actor.Id==0xad)
                    {
                        if(context.Entities.InteractionSlotAvailable)
                        {
                            var record=Data.ActorRecord(0xad,0) with {Group=room.Group,Room=room.Room,X=(int)actor.Position.X,Y=(int)actor.Position.Y};
                            context.Entities.AddEntity(new RoomOfRitesZeldaActor(context,record));
                        }
                    }
                    else SpawnLinked(actor.Id,actor.SubId,actor.Position);
                }
                context.Hud.HideStatusBar();CaptureLinkedPalettes(0xac);context.Transitions.CancelNativeWhiteFade(clearPresentation:true);BlackPalettes(0xfd,255);
                SetLinkedRoomScreen(true);
                context.Transitions.SetCutsceneCameraOrigin(new(40,0));context.Transitions.CutsceneCameraFrozen=true;Write(WramAddress.wScrollMode,0);Write(0xcbb3,30);Write(0xcbc1,1);return;
            case 1:if(DespairDelay())DespairText(0x2825,40,4,null);return;
            case 2:if(DespairDelay(text:true)){Write(0xcbc1,3);Write(0xcbb3,32);Write(0xcbb4,0);}return;
            case 3:
                if(!DespairDelay(palette:true))return;
                Write(0xcbb3,32);int index=Read(0xcbb4);Write(0xcbb4,index+1);
                int mask=Data.Table("flame-masks")[index];if(index<3)context.Sound.PlayNativeSound(0x72);
                if(mask!=0){MaskedFade(true,false,0,mask,speed:3);return;}
                MaskedFade(true,false,255,1);Write(0xcbb3,60);context.Sound.PlayNativeSound(0x1d);Write(0xcbc1,4);return;
            case 4:if(DespairDelay(palette:true))DespairText(0x2826,40);return;
            case 5:if(DespairDelay(text:true))DespairText(0x2827,40);return;
            case 6:if(DespairDelay(text:true))DespairText(0x2828,60);return;
            case 7:if(DespairDelay(text:true)){Write(0xcbc1,8);Write(0xcbb3,180);}return;
            case 8:
                DespairShake();if(((context.Entities.FrameCounter+1)&15)==0)context.Sound.PlayNativeSound(SoundId.SndRumble2);
                if(!DespairDelay())return;
                context.Sound.PlayNativeSound(SoundId.SndCtrlStopSfx);context.Sound.PlayNativeSound(SoundId.SndCtrlMediumFadeOut);
                Write(0xcbc1,9);context.Transitions.BeginNativeWhiteFade(true,delay:4);return;
            case 9:
                DespairShake();if(context.Transitions.NativePaletteFadeActive)return;
                Write(0xcbc1,10);Write(0xcfde,0x0c);var palace=Data.RoomSet(0x0c);Load(palace.Group,palace.Room);
                foreach(var actor in Data.Placements(palace.Scene))
                {
                    if(actor.Id==0x40)context.Entities.AddEntity(new BlackTowerEndingActor(context,Data,actor.Id,actor.SubId,actor.Position));
                    else SpawnLinked(actor.Id,actor.SubId,actor.Position);
                }
                _linkEnabled=true;context.Transitions.CutsceneCameraFrozen=false;context.Player.WarpTo(new(0x60,0x48),recordSafe:false);context.Player.Face(Vector2I.Up);context.Player.Visible=true;
                context.Player.RequestNativeLinkInitialization();Write(WramAddress.wDisabledObjects,0x81);Write(WramAddress.wMenuDisabled,0x81);
                for(int address=0xcfc0;address<0xcfe0;address++)Write(address,0);
                SetLinkedRoomScreen(false);context.Hud.ShowStatusBar();context.Sound.PlayNativeSound(SoundId.SndCtrlStopSfx);context.Sound.PlayNativeSound(SoundId.SndCtrlStopMusic);
                context.Transitions.BeginNativeWhiteFade(false);return;
            case 10:
                if(Read(0xcfd0)!=1)return;
                Write(0xcbc1,11);context.Inventory.GiveTreasure(TreasureId.HeartRefill,0x40);
                context.Player.BeginRitesReaction(this,context.Player.Position,()=>
                {
                    if(context.Entities.InteractionSlotAvailable)context.Entities.Spawn(new ExclamationMarkSpawn(context.Player.Position+new Vector2(4,-8),Duration:255,Record:Data.ActorRecord(0x9f,1)));
                });
                Write(0xcbb3,90);context.Sound.PlayNativeSound(0x4a);return;
            case 11:
                if(!DespairDelay())return;
                Write(0xcbc1,12);Write(0xcbb3,180);context.Entities.CreateEnergySwirl(new(0x60,0x48),outgoing:true);return;
            case 12:if(DespairDelay()){Write(0xcbc1,13);Write(0xcbb3,60);context.Transitions.BeginNativeWhiteFade(true);}return;
            case 13:
                if(!DespairDelay(palette:true))return;
                Write(0xcbc1,14);context.Entities.Clear();context.Player.CancelCreditsLink();context.Player.Visible=false;_linkEnabled=false;
                // bank0.clearScreenVariables clears the $30-byte screen
                // block, independently of the retained object/menu masks.
                for(int at=0xcd00;at<0xcd30;at++)Write(at,0);
                Write(0xcd28,255);Write(0xcd2a,255);Write(0xcd2b,255);
                context.Hud.HideStatusBar();context.RoomView.SetBackgroundFade(Colors.White,1);context.Transitions.CutsceneCameraFrozen=true;context.Transitions.SetCutsceneCameraOrigin(new(0,16));
                SetLinkedRoomScreen(true);
                CaptureLinkedPalettes(0x0f);SpawnLinked(0x36,0x12,Vector2.Zero);SpawnLinked(0xaa,2,Vector2.Zero);
                FillLinkedPalettes(Colors.White,0,255);Write(WramAddress.wOpenedMenuType,2);Write(0xcbb3,30);context.Transitions.CancelNativeWhiteFade(clearPresentation:true);
                context.ShowDialogue(Data.Text(0x1d1a),sourceTextId:0x1d1a);return;
            case 14:
                if(!DespairDelay(text:true))return;
                Write(0xcbc1,15);MaskedFade(false,false,0,4);Write(0xcbb3,60);return;
            case 15:if(DespairDelay(palette:true))DespairText(0x1d1b,30,0,null);return;
            case 16:
                if(!DespairDelay(text:true))return;
                Write(0xcbc1,17);MaskedFade(false,false,0,0x12);Write(0xcbb3,60);return;
            case 17:if(DespairDelay(palette:true))DespairText(0x1d1c,30,0,null);return;
            case 18:if(DespairDelay(text:true)){Write(0xcbc1,19);Write(0xcbb3,60);}return;
            case 19:
                if(!DespairDelay())return;
                Write(0xcbc1,20);Write(0xcbb3,240);context.Entities.CreateEnergySwirl(new(0x50,0x48),outgoing:true);return;
            case 20:
                if(!DespairDelay())return;
                Write(0xcbb3,90);context.Transitions.BeginNativeWhiteFade(true);Write(0xcbc1,21);return;
            case 21:
                if(!DespairDelay(palette:true))return;
                Write(0xcbc1,22);context.Entities.ClearDynamicInteractions();
                foreach(var part in context.Entities.EntityAdapters<BlueEnergyBeadRoomEntity>().ToArray())context.Entities.RetireEntity(part);
                Write(0xcbb3,60);context.ShowDialogue(Data.Text(0x1d1d),textboxFlags:2,sourceTextId:0x1d1d);return;
            case 22:
                if(!context.DialogueOpen||!context.TextPrintingComplete||!DespairDelay())return;
                Write(WramAddress.wOpenedMenuType,0);context.Sound.PlayNativeSound(SoundId.SndCtrlFastFadeOut);
                context.CloseDialogue();LinkedWarp(5,0xf4,0x57,WarpDestinationTransition.Invisible);return;
            default:throw new NotSupportedException($"endgameCutsceneHandler_20 state${Stage:x2}.");
        }
    }
}
