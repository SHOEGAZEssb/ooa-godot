using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed partial class BlackTowerEndingEvent
{
    private void StaffRollCamera(int y)
    {
        Write(0xc486,y);Screen.SetScrollY(y);
        var origin=-context.Transitions.WorldToGameplayScreen(Vector2.Zero);
        context.Transitions.SetCutsceneCameraOrigin(new(origin.X,y));Screen.RefreshObjectOam();
    }
    private void WavingOam()
    {
        int scroll=Read(0xc486);var batches=new List<(string,int,int)>();
        // The cutscene dispatch precedes the object pass's frame increment.
        if(scroll<0x60&&((context.Entities.FrameCounter+1)&1)==0)batches.Add(("oamData_4ed8",-scroll,0));
        batches.Add(("oamData_4f21",0xc7-scroll,0x38));
        if(scroll>=0x60)batches.Add(("oamData_4f56",0xc7-scroll,0x38));
        Screen.SetOamBatches(batches.ToArray());
    }
    // bank10.s:agesFunc_10_70f6, linked substates $02..$08.
    private void AdvanceLinkedStaffRoll()
    {
        switch(State)
        {
            case 2:
                Screen.SetOam(null);if(!DecrementWord(0xcbb3))return;
                Write(0xcbb3,4);StaffRollCamera(Read(0xc486));Screen.ApplyLinkedRamHeader(1);
                Screens.ApplyPalette(0x0b,context.Rooms.World.BackgroundPalettes,context.Entities);
                for(int variant=2;variant>=0&&context.Entities.InteractionSlotAvailable;variant--)
                    context.Entities.AddEntity(new CreditsSceneActorRoomEntity(context,Data,Screen,0x4a,9,variant));
                State=3;return;
            case 3:
                Screen.SetOam(null);
                if(Read(0xc486)==0){Write(0xcbb3,120);State=4;return;}
                if(Decrement(0xcbb3)){Write(0xcbb3,4);StaffRollCamera(Read(0xc486)-1);}return;
            case 4:
                Screen.SetOam(null);if(!Decrement(0xcbb3))return;
                Write(0xcbba,255);State=5;return;
            case 5:
                Screen.SetOam(null);if(!flash(1))return;
                Screens.ApplyPalette(0x9f,context.Rooms.World.BackgroundPalettes,context.Entities);
                Screen.Load("credits-waving",retainGraphics:true);context.Entities.ClearDynamicInteractions();
                for(int subid=2;subid>=0&&context.Entities.InteractionSlotAvailable;subid--)
                    context.Entities.AddEntity(new CreditsSceneActorRoomEntity(context,Data,Screen,0xcf,subid));
                Write(0xc486,0);Screen.SetScrollY(0);Screen.SetScrollX(0);
                context.Transitions.BeginNativeWhiteFade(false,delay:4);State=6;Write(0xcbb3,240);WavingOam();return;
            case 6:
                WavingOam();if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                Write(0xcbb3,4);State=7;return;
            case 7:
                if(Read(0xc486)==0x98){Write(0xcbb3,240);State=8;}
                else if(Decrement(0xcbb3))
                {
                    Write(0xcbb3,4);StaffRollCamera(Read(0xc486)+1);
                    if(Read(0xc486)==0x60){context.Entities.ClearDynamicInteractions();Screen.ApplyLinkedRamHeader(0x2c);}
                }
                WavingOam();return;
            case 8:
                WavingOam();if(!Decrement(0xcbb3))return;
                ClearTemporaryState();Write(0xcbc1,3);context.Transitions.BeginNativeWhiteFade(true,delay:4);return;
            default:throw new System.NotSupportedException($"bank10.s:linked staff roll state${State:x2}.");
        }
    }
}
