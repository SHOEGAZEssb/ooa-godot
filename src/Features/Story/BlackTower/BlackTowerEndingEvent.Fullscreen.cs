using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class BlackTowerEndingEvent
{
    private EndingScreenDatabase? _screenData;
    private EndingScreenDatabase Screens=>_screenData??=new();
    private EndingScreen Screen=>context.InterfaceLayer.GetNode<EndingScreen>("EndingScreen");
    private bool _screenActive,_hudWasVisible;
    private Color[,]? _oldBackground;
    private IReadOnlyDictionary<int,Color[]>? _oldSprites;
    private Action? _restoreTerrain;
    internal int Stage=>Read(0xcbc1);
    internal bool CreditsPending=>HasState&&Read(0xc2ef)==0x0a;
    public DialogueScreenContext? DialogueScreen=>LinkedLeadInActive&&
        (Read(0xc2ef)==0x11||Read(0xc2ef)==0x20&&(Stage<9||Stage>=13))?
        context.DialogueScreens.ClearedLink(Read(0xc2ef)==0x20&&Stage>=13?4:0x13):
        _screenActive?context.DialogueScreens.ClearedLink(4):null;
    private void ClearTemporaryState(){for(int address=0xcbb3;address<0xcbc3;address++)Write(address,0);}
    private void BeginScreen()
    {
        _hudWasVisible=context.Hud.Visible;_oldBackground=context.Rooms.World.BackgroundPalettes.Capture();_oldSprites=context.Entities.NativeObjectPalettes;
        _screenActive=true;context.Hud.Visible=false;Screen.Bind(Screens,context.Rooms.World.BackgroundPalettes,context.Entities);
        context.Entities.ClearDynamicInteractions();context.Player.EndCutsceneControl();context.Player.WarpTo(Vector2.Zero,recordSafe:false);
        context.Player.Visible=false;_linkEnabled=false;
    }
    private void Profile(string profile,int palette)
    {Screens.ApplyPalette(palette,context.Rooms.World.BackgroundPalettes,context.Entities);Screen.Load(profile);}
    private void OracleOam()=>Screen.SetOam("oamData_4e37",0x30,0x38);
    private void TwinrovaOam(bool second)=>Screen.SetOam(second?"oamData_4d9e":"oamData_4d05",0x30,Read(0xcbb5));
    private void Text(int id)=>context.ShowDialogue(Screens.Text(id),textboxFlags:1,sourceTextId:id);
    private void AdvanceOracle()
    {
        switch(State)
        {
            case 0x11:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=0x12;Write(0xcbb3,60);Write(WramAddress.wTilesetAnimation,255);BeginScreen();Profile("oracle",0x9d);OracleOam();
                context.Transitions.BeginNativeWhiteFade(false);return;
            case 0x12:
                OracleOam();if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=0x13;Write(WramAddress.wMenuDisabled,1);Write(0xcbb3,60);Text(0x1312);return;
            case 0x13:
                OracleOam();if(context.DialogueOpen||!Decrement(0xcbb3))return;
                ClearTemporaryState();Write(0xcbc1,1);context.Transitions.BeginNativeWhiteFade(true);return;
        }
    }
    private void AdvanceTwinrova()
    {
        switch(State)
        {
            case 0:
                OracleOam();if(context.Transitions.NativePaletteFadeActive)return;
                State=1;Write(0xcbb3,60);Profile("twinrova",0x9e);Screen.SetOam(null);
                context.Sound.PlaySound(SoundId.MusDisaster);context.Transitions.BeginNativeWhiteFade(false);return;
            case 1:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=2;
                // @runStates loads DE=$cbc2. The helper and incCbc2 leave E
                // intact, so ld a,e installs $c2 here, not the preceding A=60.
                Write(0xcbb3,0xc2);Text(0x280b);return;
            case 2:
                if(context.DialogueOpen||!Decrement(0xcbb3))return;
                State=3;Write(0xcbb5,0xd0);TwinrovaOam(false);return;
            case 3:
                Write(0xcbb5,Read(0xcbb5)+1);if(Read(0xcbb5)!=0){TwinrovaOam(false);return;}
                Screen.SetOam(null);Screen.UseSecondMap();Write(0xcbb3,30);State=4;return;
            case 4:
                if(!Decrement(0xcbb3))return;
                State=5;Write(0xcbb5,0xd0);TwinrovaOam(true);return;
            case 5:
                // The source emits this OAM before decrementing cbb5.
                TwinrovaOam(true);Write(0xcbb5,Read(0xcbb5)-1);if(Read(0xcbb5)!=0xa0)return;
                Write(0xcbb3,30);Write(WramAddress.wOpenedMenuType,30);State=6;return;
            case 6:
                TwinrovaOam(true);if(!Decrement(0xcbb3))return;
                Write(0xcbb3,20);Text(0x280c);State=7;return;
            case 7:
                TwinrovaOam(true);if(context.DialogueOpen||!Decrement(0xcbb3))return;
                Write(WramAddress.wOpenedMenuType,0);Write(0xcbba,255);context.Sound.PlaySound(SoundId.SndLightning);State=8;return;
            case 8:
                TwinrovaOam(true);if(!flash(2))return;
                State=9;Write(0xcbb3,30);Profile("closeup",0x9c);Screen.SetOam(null);
                context.Sound.PlaySound(SoundId.SndLightning);context.Transitions.CancelNativeWhiteFade();context.Fade.Color=new(1,1,1,0);return;
            case 9:
                if(!Decrement(0xcbb3))return;
                Write(0xc2ef,0x0a);ClearTemporaryState();_restoreTerrain=context.Rooms.CurrentRoom.ClearNativeCutsceneTerrain();
                context.Transitions.ResetCamera();Write(0xcbb3,60);context.Transitions.BeginNativeBlackFade(true,delay:3);return;
            default:throw new NotSupportedException($"endgameCutscenes.s: CUTSCENE $09 stage1 state ${State:x2}.");
        }
    }
    private void ReleaseScreen()
    {
        if(!_screenActive)return;
        Screen.Release();context.Hud.Visible=_hudWasVisible;
        context.RoomView.SetRoom(context.Rooms.CurrentRoom.Texture);
        context.Rooms.World.BackgroundPalettes.Restore(_oldBackground!);context.Entities.SetNativeObjectPalette(_oldSprites);
        _restoreTerrain?.Invoke();_restoreTerrain=null;_oldBackground=null;_oldSprites=null;_screenActive=false;
    }
}
