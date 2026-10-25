using Godot;
using System;

namespace Oracles;

internal sealed partial class BlackTowerEndingEvent
{
    private void BeginReunionLink(Vector2 position)
    {
        _linkEnabled=true;
        context.Player.BeginCreditsReunion(this,position,()=>
        {
            for(int subid=1;subid>=0;subid--)
            {
                if(!context.Entities.InteractionSlotAvailable)break;
                context.Entities.AddEntity(new KissHeartRoomEntity(Data,subid,context.Player.Position));
            }
        });
    }
    private void LoadReunion()
    {
        var room=Data.RoomSet(0x11);Load(room.Group,room.Room);
        foreach(var actor in Data.Placements(room.Scene))
        {
            switch(actor.Id)
            {
                case 0xa8:context.Entities.AddEntity(new CreditsLinkLoaderRoomEntity(context.Player,this,actor.Position,BeginReunionLink));break;
                case 0xaa:context.Entities.AddEntity(new DinRoomEntity(context,Data,actor.Position));break;
                case 0x36:case 0x87:context.Entities.AddEntity(new NayruRescueActorRoomEntity(context,_companions??=new(),actor.Id,actor.SubId,actor.Position));break;
                case 0x31:context.Entities.AddEntity(new ImpaCreditsRoomEntity(context,Data,actor.Position,()=>flash(1),actor.SubId));break;
                case 0xad:context.Entities.AddEntity(new ZeldaEndingRoomEntity(context,Data,actor.SubId,actor.Position));break;
                default:throw new NotSupportedException($"endgameCutsceneHandler_0f room set$11: INTERAC${actor.Id:x2}:${actor.SubId:x2}.");
            }
        }
        context.Transitions.BeginNativeWhiteFade(false);
    }
    private void ReunionOam()=>Screen.SetOam("oamData_4f73",0x30,0x38);
    private void AdvanceRoomOfRitesReunion()
    {
        switch(State)
        {
            case 0:
                RitesRumble();if(context.Transitions.NativePaletteFadeActive)return;
                State=1;Write(0xcfde,0x11);LoadReunion();
                context.Sound.PlayNativeSound(SoundId.SndCtrlStopSfx);context.Sound.PlayNativeSound(SoundId.SndCtrlFastFadeOut);Write(0xcbb3,60);return;
            case 1:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=2;Write(0xcbb3,60);context.Entities.CreateEnergySwirl(new(0x50,0x48),100);return;
            case 2:
                if(!Decrement(0xcbb3))return;
                State=3;Write(0xcbb3,0);Write(0xcbba,255);return;
            case 3:
                if(!flash(1))return;
                State=4;Write(0xcbb3,60);Write(0xcfc0,1);context.Transitions.BeginNativeWhiteFade(false,delay:3);return;
            case 4:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=5;Write(0xcbb3,60);context.Sound.PlayNativeSound(SoundId.MusCredits1);return;
            case 5:
                if(!Decrement(0xcbb3))return;
                State=6;Write(0xcbb3,30);return;
            case 6:
                if(!Decrement(0xcbb3))return;
                State=7;Write(0xcbb3,60);Write(0xcfc0,2);return;
            case 7:
                if(Read(0xcfc0)!=9)return;
                State=8;context.Transitions.BeginNativeWhiteFade(true,delay:3);return;
            case 8:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=9;context.Player.CancelCreditsLink();BeginScreen();Profile("reunion-makutree",0xc9);
                Write(0xcbb3,240);ReunionOam();context.Transitions.BeginNativeWhiteFade(false,delay:3);return;
            case 9:
                ReunionOam();if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=10;Write(0xcbb3,16);context.Transitions.BeginNativeBlackFade(true,delay:3);return;
            case 10:
                ReunionOam();if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                Write(0xc2ef,0x0a);ClearTemporaryState();_restoreTerrain=context.Rooms.CurrentRoom.ClearNativeCutsceneTerrain();
                context.Transitions.ResetCamera();Write(0xcbb3,60);context.Sound.PlayNativeSound(SoundId.SndCtrlMediumFadeOut);return;
            default:throw new NotSupportedException($"endgameCutsceneHandler_0f stage$01 substate${State:x2}.");
        }
    }
}
