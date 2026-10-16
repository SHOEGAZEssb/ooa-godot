using System;

namespace oracleofages;

internal sealed partial class BlackTowerEndingEvent
{
    private LinkedGameNpcDatabase? _transferSecrets;
    private void RestoreEndingInventory()
    {
        // endgameCutscenes.s:func_60f1 restores full health, rather than the
        // pre-credits health retained solely for a cancelled earlier sequence.
        context.Inventory.SetScriptedHealth(context.Inventory.MaxHealthQuarters);
        context.Inventory.SetScriptedEquippedItems(Read(0xcde3),Read(0xcde4));
        context.Inventory.SetActiveRingEnabled(true);_healthBeforeCredits=null;
    }
    private void AdvanceCreditsFinale()
    {
        // bank10.s:agesFunc_10_7298; invoked before updateGameKeysPressed, so
        // these action edges are the physical sample, before simulated input.
        switch(State)
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=1;RestoreEndingInventory();context.Entities.ClearDynamicInteractions();
                Profile("credits-end",0xa9);context.Transitions.ResetCamera();
                // The original overwrites the $f0 store at the SAME address
                // with A=0. decCbb3 therefore wraps and waits 256 updates.
                Write(0xcbb3,0);context.Sound.PlaySound(SoundId.SndCtrlMediumFadeOut);
                context.Transitions.BeginNativeWhiteFade(false,delay:4);return;
            case 1:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=2;return;
            case 2:
                if(!Decrement(0xcbb3))return;
                State=3;goto case 3;
            case 3:
                if(((context.Rooms.SaveData.ReadWramByte(WramAddress.wFileIsLinkedGame)+context.Rooms.SaveData.ReadWramByte(WramAddress.wFileIsHeroGame))&255)==2)return;
                if(!Input.IsActionJustPressed("attack")&&!Input.IsActionJustPressed("item")&&!Input.IsActionJustPressed("inventory"))return;
                State=4;context.Transitions.BeginNativeWhiteFade(true);return;
            case 4:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=5;var text=(_transferSecrets??=new()).GenerateGameTransferSecretText(context.Rooms.SaveData);Write(0xcbba,255);
                Profile("ending-secret",5);Screen.UploadSecretText(text);context.Entities.ClearDynamicInteractions();
                Write(0xcbb3,60);Screen.DrawFileDecorations(this);context.Transitions.BeginNativeWhiteFade(false);return;
            case 5:
                Screen.DrawFileDecorations(this);
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                Write(0xcbb3,60);State=6;return;
            case 6:
                Screen.DrawFileDecorations(this);if(!Decrement(0xcbb3))return;
                if(context.Entities.InteractionSlotAvailable)
                {context.Entities.AddEntity(new GameCompleteDialogRoomEntity(context));Write(0xcfde,0);}
                State=7;return;
            case 7:
                Screen.DrawFileDecorations(this);if(Read(0xcfde)==0)return;
                State=8;context.Sound.PlaySound(SoundId.SndCtrlFastFadeOut);context.Transitions.BeginNativeWhiteFade(true);return;
            case 8:
                Screen.DrawFileDecorations(this);if(context.Transitions.NativePaletteFadeActive)return;
                State=9;Profile("ending-continued",0xa7);context.Transitions.BeginNativeWhiteFade(false);return;
            case 9:
                Screen.SetOam("oamData_4fec",0x30,0x38);if(context.Transitions.NativePaletteFadeActive)return;
                Write(0xcbb3,180);State=10;return;
            case 10:
                Screen.SetOam("oamData_4fec",0x30,0x38);
                if(Read(0xcbb3)!=0){Decrement(0xcbb3);return;}
                if(!Input.IsActionJustPressed("attack"))return;
                State=11;context.Transitions.BeginNativeWhiteFade(true);return;
            case 11:
                Screen.SetOam("oamData_4fec",0x30,0x38);
                if(!context.Transitions.NativePaletteFadeActive)context.ResetGame();return;
            default:throw new NotSupportedException($"bank10.s: normal ending final screen substate ${State:x2} is not represented.");
        }
    }
}
