using Godot;
using System;

namespace oracleofages;

internal sealed partial class RoomOfRitesBattleEvent
{
    private GanonDatabase? _ganonData;
    private bool _ganonHudHidden;
    internal GanonRoomEntity? CreateGanon()
    {
        var data=_ganonData??=new();
        var world=new GanonEnvironment(data,context.Entities,InitializeGanon,GanonFade,ClearGanonPalette,show=>
        {
            _ganonHudHidden=!show;
            if(show)context.Hud.ShowStatusBar();else context.Hud.HideStatusBar();
        },ReloadArena,(fadeOut,delay)=>
        {
            _resources.CaptureFullScreenFade();_ownsFade=true;
            context.Transitions.BeginNativeBlackFade(fadeOut,delay:delay);
        },()=>Fade(true,1,3),index=>
        {
            Write(0xcbe3,0xb1+index);
            context.Rooms.CurrentRoom.BackgroundPalettes.LoadSlots(7,data.ReversePalette(index));
        },()=>
        {
            var save=context.Rooms.SaveData;int group=context.Rooms.ActiveGroup;
            save.SetRoomFlag(group,context.Rooms.CurrentRoom.Id,0x80);
            save.SetRoomFlag(group,0xf1,0x80);save.SetRoomFlag(group,0xf5,0x80);
        });
        return (GanonRoomEntity?)context.Entities.TryAllocateEnemy(_=>new GanonRoomEntity(new(world)));
    }
    private void GanonFade(bool fadeOut,int delay,bool backgroundOnly)
    {
        if(!backgroundOnly){Fade(fadeOut,delay);return;}
        _resources.CaptureFullScreenFade();_ownsFade=true;
        context.Transitions.BeginNativeBackgroundBlackFade(delay);
    }
    private void ClearGanonPalette()
    {
        context.Transitions.CancelNativeWhiteFade(clearPresentation:true);
        _resources.ReleaseFullScreenFade(restoreColor:false);_ownsFade=false;
    }
    private void InitializeGanon(GanonCharacter actor)
    {
        Write(TwinrovaArenaDatabase.ModeAddress,3);
        var room=context.Rooms.LoadCutsceneBackground(5,0xf5);room.CopyLayoutToUnderlying();
        context.Entities.RebindCutsceneBackground(room);context.RoomView.SetRoom(room.Texture);
        context.Sound.SelectRoomMusic(5,0xf5,context.Rooms.SaveData);
        context.Player.WarpTo(new(0x78,0x88),recordSafe:false);context.Player.Face(Vector2I.Up);
        context.Player.Visible=false; // Ganon writes XYZ/enabled; Link's next state-$00 visit reveals him.
        context.Player.RequestNativeLinkInitialization();_linkCleared=false;context.Transitions.ResetCamera();
        context.Entities.LoadNativeObjectPaletteSlots(6,actor.World.Data.SpritePalette);
        room.BackgroundPalettes.LoadSlots(7,actor.World.Data.BackgroundPalette);
        Write(0xcbe3,0xb1);
    }
    private void AdvanceSacrifice()
    {
        switch(State)
        {
            case 4:case 5:case 6:
                Write(0xcbb4,Read(0xcbb4)-1);if(Read(0xcbb4)!=0)return;
                Write(0xcbb4,State==6?40:20);
                if(context.Entities.PartSlotAvailable)
                    context.Entities.AddEntity(new LightningRoomEntity(context,State switch {4=>new(0x78,0x18),5=>new(0xa8,0x48),_=>new(0x48,0x48)}));
                State++;return;
            case 7:
                Write(0xcbb4,Read(0xcbb4)-1);if(Read(0xcbb4)!=0)return;
                Write(0xcbb4,120);context.Sound.PlayNativeSound(SoundId.SndBossDead);State=8;return;
            case 8:
                context.Entities.BeginScreenShake(255);
                if(((context.Entities.FrameCounter+1)&7)==0)Fade(false,1,3);
                Write(0xcbb4,Read(0xcbb4)-1);if(Read(0xcbb4)!=0)return;
                Fade(true,4);context.Sound.PlayNativeSound(SoundId.SndFadeOut);State=9;return;
            case 9:
                context.Entities.BeginScreenShake(255);if(context.Transitions.NativePaletteFadeActive)return;
                RetireSceneActors();byte count=(byte)context.Entities.RoomEnemyCount;
                context.Entities.LoadCutsceneRoom(5,context.Rooms.CurrentRoom,includeTimePortals:false);
                context.Entities.SetNativeRoomEnemyCount(count);
                context.Player.ClearNativeItemParents();context.Player.WarpTo(Vector2.Zero,recordSafe:false);context.Player.Visible=false;_linkCleared=true;
                Cutscene=0;Write(WramAddress.wScrollMode,1);
                if(CreateGanon() is null)throw new InvalidOperationException("cutscene19_state9: unchecked ENEMY $04 allocation failed after clearWramBank1.");
                context.Sound.PlayNativeSound(SoundId.SndCtrlStopMusic);return;
            default:throw new NotSupportedException($"cutscene19_body: state${State:x2}.");
        }
    }
}
