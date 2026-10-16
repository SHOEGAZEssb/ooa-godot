using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// CUTSCENE_BLACK_TOWER_ESCAPE_ATTEMPT $1f: bank1 calls this native machine,
// then updateStatusBar/updateAllObjects. The room reload retains actor slots.
internal sealed class BlackTowerEscapeAttemptEvent(RoomEventContext context,Action releasePossessionFade) : IRoomEvent
{
    private BlackTowerEscapeDatabase? _data;
    private BlackTowerEscapeDatabase Data=>_data??=new();
    private NayruRescueWarpDatabase? _companions;
    private readonly RoomEventResources _resources=new(context,new object());
    private readonly HashSet<IRoomEntity> _roots=[];
    internal int State {get;private set;}
    public bool HasState {get;private set;}
    public bool BlocksGameplay=>HasState;
    public bool OwnsGameLogic=>HasState;
    public bool MenusDisabled=>HasState;
    public bool AllScreenTransitionsDisabled=>HasState;
    public bool OwnsUpdatesOf(IRoomEvent other)=>other is BlackTowerPossessionEvent or PalacePossessionEvent;
    private byte Read(int address)=>context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>context.Entities.RuntimeState.SetWramByte(address,unchecked((byte)value));
    private bool Decrement(int address){Write(address,Read(address)-1);return Read(address)==0;}
    internal void Begin()
    {
        releasePossessionFade();_roots.Clear();HasState=true;State=0;_resources.CaptureFullScreenFade();
    }
    internal void AdvanceBeforeObjects()
    {
        if(!HasState)return;
        context.Transitions.AdvanceNativeWhiteFade();
        switch(State)
        {
            case 0:
                context.Sound.SetNativeActiveMusic(context.Sound.NativeRoomMusic);context.Sound.PlaySound(context.Sound.NativeRoomMusic);
                for(int address=0xcbb3;address<0xcbc3;address++)Write(address,0);
                int enemies=context.Entities.RoomEnemyCount;context.Entities.Clear();context.Entities.SetNativeRoomEnemyCount((byte)enemies);
                context.Player.EndCutsceneControl();context.Player.WarpTo(new(0x78,0x58),recordSafe:false);
                context.Player.Face(Vector2I.Down);context.Player.Visible=true;
                Write(WramAddress.wDisabledObjects,1);Write(WramAddress.wMenuDisabled,1);Write(0xcbb4,60);State=1;
                context.Transitions.ResetCamera();Write(WramAddress.wScrollMode,0);
                _companions??=new();
                foreach(var placement in Data.Placements)
                {
                    var actor=new NayruRescueActorRoomEntity(context,_companions,placement.Id,placement.SubId,placement.Position);
                    _roots.Add(actor);context.Entities.AddEntity(actor);
                }
                context.Transitions.BeginNativeWhiteFade(false,4);return;
            case 1:
                if(Read(0xcbb5)!=4)return;
                if(Decrement(0xcbb4)){Write(0xcbb4,16);State=2;context.Transitions.BeginNativeWhiteFade(true,4);return;}
                if(Read(0xcbb4)==1)ForceMovement(0x50);return;
            case 2:
                if(context.Transitions.NativePaletteFadeActive)return;
                context.Sound.PlaySound(SoundId.SndCtrlStopMusic);State=3;
                var room=context.Rooms.LoadCutsceneBackground(4,0xf3);
                context.Entities.RebindCutsceneBackground(room);context.RoomView.SetRoom(room.Texture);
                context.Sound.SelectRoomMusic(4,0xf3,context.Rooms.SaveData);
                context.Player.SetScriptedPosition(new(0x78+context.Player.PrecisePosition.X%1,0x78+context.Player.PrecisePosition.Y%1));
                context.Transitions.ResetCamera();context.Transitions.BeginNativeWhiteFade(false,4);return;
            case 3:
                Write(WramAddress.wScrollMode,0);
                context.Player.SetScriptedPosition(new(context.Player.PrecisePosition.X,0xf8+context.Player.PrecisePosition.Y%1));
                Write(0xcbb5,5);
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb4))return;
                ForceMovement(0x60);State=4;return;
            case 4:
                if(Read(0xcbb5)!=6)return;
                Rumble();int shake=context.Entities.ScreenShakeCounter;
                if(shake!=1)
                {
                    if(((shake-1)&0x1f)!=0)return;
                    int direction=CarriedObjectMotion.DirectionIndex(context.Player.FacingVector);
                    context.Player.Face(((direction>>1)^direction)%2==0?Direction(direction^2):Direction(direction^1));return;
                }
                if(!context.Entities.InteractionSlotAvailable)return;
                context.Entities.CreatePalaceExclamation(context.Player.Position-new Vector2(0,16),30);
                Write(0xcbb4,30);State=5;goto case 5;
            case 5:
                Rumble();if(!Decrement(0xcbb4))return;
                context.Player.Face(Vector2I.Down);ForceMovement(0x10);Write(0xcbb5,7);Write(0xcfde,0);
                if(!context.Entities.TryCreateFallingRock(0,1,Vector2.Zero))
                    throw new NotSupportedException("miscCutscenes.s:func_03_7cb7 state5 unchecked falling rock allocation overflow is not represented.");
                _roots.Add(context.Entities.EntityAdapters<FallingRockRoomEntity>().Single(rock=>rock.SubId==0));
                if(!context.Entities.InteractionSlotAvailable)throw new NotSupportedException("miscCutscenes.s:func_03_7cb7 state5 unchecked Wallmaster allocation overflow is not represented.");
                var hand=new VeranCutsceneWallmasterRoomEntity(context,Data,
                    new((int)context.Player.Position.X,((int)context.Player.Position.Y+16)&255));
                _roots.Add(hand);context.Entities.AddEntity(hand);
                State=6;return;
            case 6:
                Rumble();if(Read(0xcbb5)!=8)return;
                context.Sound.PlaySound(SoundId.SndCtrlStopMusic);context.Sound.SetNativeActiveMusic(0);
                HasState=false;_roots.Clear();_resources.ReleaseFullScreenFade();var warp=Data.Warp;
                context.Transitions.RequestNativeFadeOutWarp(new(context.Rooms.ActiveGroup,context.Rooms.CurrentRoom.Id,-1,0,0,
                    warp[0]&7,warp[1],warp[3],(warp[2]>>4)&7,warp[2]&15));return;
            default:throw new NotSupportedException($"miscCutscenes.s:func_03_7cb7 state ${State:x2}.");
        }
    }
    private void ForceMovement(int parameter)=>context.Player.RequestNativeForcedMovement(parameter,16,CarriedObjectMotion.DirectionIndex(context.Player.FacingVector));
    private void Rumble()
    {
        int shake=context.Entities.ScreenShakeCounter;
        if((shake&15)==0)context.Sound.PlaySound(SoundId.SndRumble);
        if(shake==0)context.Entities.BeginScreenShake(255);
    }
    private static Vector2I Direction(int direction)=>direction switch {0=>Vector2I.Up,1=>Vector2I.Right,2=>Vector2I.Down,3=>Vector2I.Left,_=>throw new ArgumentOutOfRangeException(nameof(direction))};
    public void UpdateFrame() {}
    public void Cancel()
    {
        if(!HasState)return;
        // State0 replaced the whole object bank. Clear its descendants only
        // while a retained root still belongs to the active object set; room
        // replacement has already retired these roots before cancellation.
        if(context.Entities.EntityAdapters<IRoomEntity>().Any(_roots.Contains))context.Entities.Clear();
        _roots.Clear();
        HasState=false;State=0;context.Player.Visible=true;context.Player.EndForcedRoomEntryMovement();
        context.Transitions.CancelNativeWhiteFade();_resources.ReleaseFullScreenFade();
        Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);Write(WramAddress.wScrollMode,0);
    }
}
