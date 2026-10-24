using Godot;
using System;

namespace oracleofages;

internal sealed partial class BlackTowerEndingEvent
{
    private MakuConfettiDatabase? _ritesConfetti;
    private FallingBoulderDatabase? _ritesBoulders;
    internal bool RitesCollapseActive=>HasState&&Read(0xc2ef)==0x0f;
    internal void BeginRoomOfRitesCollapse()
    {
        HasState=true;_initialize=true;_linkEnabled=true;Write(0xc2ef,0x0f);
        _resources.CaptureFullScreenFade();
    }
    private void RitesRumble()
    {
        context.Entities.BeginScreenShake(4);
        if(((context.Entities.FrameCounter+1)&15)==0)context.Sound.PlayNativeSound(SoundId.SndRumble2);
    }
    private void RitesBoulders()
    {
        if(context.Entities.PartSlotAvailable)
            context.Entities.AddEntity(new RoomOfRitesBoulderRoomEntity(context.Entities,_ritesBoulders??=new()));
    }
    private void AdvanceRoomOfRitesCollapse()
    {
        if(Stage==1){AdvanceRoomOfRitesReunion();return;}
        if(Stage!=0)throw new NotSupportedException($"endgameCutsceneHandler_0f stage${Stage:x2},substate${State:x2}.");
        switch(State)
        {
            case 0:
                State=1;context.Inventory.UnequipRing();
                if(!context.Entities.InteractionSlotAvailable)throw new NotSupportedException("endgameCutsceneHandler_0f: unchecked Link exclamation allocation has no free INTERACTION slot.");
                if(context.Entities.InteractionSlotAvailable)
                    context.Entities.Spawn(new ExclamationMarkSpawn(context.Player.Position+new Vector2(-16,-8),Duration:40));
                if(!context.Entities.InteractionSlotAvailable)throw new NotSupportedException("endgameCutsceneHandler_0f: unchecked Zelda exclamation allocation aliases an occupied INTERACTION slot.");
                if(context.Entities.InteractionSlotAvailable)
                    context.Entities.Spawn(new ExclamationMarkSpawn(new(0x78,0x30),Duration:40));
                Write(0xcbb3,10);return;
            case 1:
                if(!Decrement(0xcbb3))return;
                State=2;Write(0xcbb3,30);context.Sound.PlayNativeSound(SoundId.SndCtrlStopMusic);return;
            case 2:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=3;Write(0xcbb3,150);RitesBoulders();return;
            case 3:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=4;Write(0xcbb3,60);context.Sound.PlayNativeSound(SoundId.SndCtrlStopSfx);
                context.ShowDialogue(Data.Text(0x3d0e),sourceTextId:0x3d0e);return;
            case 4:
                if(context.DialogueOpen||!Decrement(0xcbb3))return;
                State=5;Write(0xcbb3,60);context.Sound.PlayNativeSound(SoundId.MusDisaster);RitesBoulders();return;
            case 5:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=6;Write(0xcbb3,90);return;
            case 6:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=7;Write(0xcbb3,60);context.Sound.PlayNativeSound(SoundId.SndCtrlStopSfx);return;
            case 7:
                if(!Decrement(0xcbb3))return;
                State=8;Write(0xcbb3,60);context.ShowDialogue(Data.Text(0x3d0f),sourceTextId:0x3d0f);return;
            case 8:
                if(context.DialogueOpen||!Decrement(0xcbb3))return;
                State=9;Write(0xcbb3,0x68);Write(0xcbb4,1);
                if(context.Entities.InteractionSlotAvailable)
                    context.Entities.AddEntity(new MakuConfettiRoomEntity(context.Entities,_ritesConfetti??=new()));
                return;
            case 9:
                if(!DecrementWord(0xcbb3))return;
                State=10;Write(0xcbb3,60);context.ShowDialogue(Data.Text(0x0563),sourceTextId:0x0563);return;
            case 10:
                if(context.DialogueOpen||!Decrement(0xcbb3))return;
                State=11;Write(0xcbb3,30);return;
            case 11:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=12;Write(0xcbb3,140);RitesBoulders();context.Entities.CreateEnergySwirl(new(0x78,0x44),outgoing:true);return;
            case 12:
                RitesRumble();if(!Decrement(0xcbb3))return;
                State=13;Write(0xcbb3,60);RitesBoulders();return;
            case 13:
                RitesRumble();if(!Decrement(0xcbb3))return;
                Write(0xcbc1,1);State=0;context.Transitions.BeginNativeWhiteFade(true,delay:3);return;
            default:throw new NotSupportedException($"endgameCutsceneHandler_0f stage$00 substate${State:x2}.");
        }
    }
}
