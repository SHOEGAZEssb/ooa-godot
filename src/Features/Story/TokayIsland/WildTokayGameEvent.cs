using Godot;
using System;
using System.Linq;

namespace oracleofages;

// Room services for the native $48:$0d/$19 managers. Each manager and its
// $70/$48:$0c/$8c children advance in their own physical interaction slots.
internal sealed class WildTokayGameEvent : IRoomEvent
{
    internal RoomEventContext Context { get; }
    internal TokayInteractionDatabase Interactions { get; }
    internal WildTokayGameDatabase Database { get; }
    private readonly RoomEventResources _resources;
    private WildTokayManagerScriptHost? _manager;
    private WildTokayControllerRoomEntity? _controller;
    private bool _fadeOwned;
    internal Func<int,Action<bool>,bool>? OpenSecretMenu { get; private set; }
    internal void SetSecretMenuOpener(Func<int,Action<bool>,bool> opener)=>OpenSecretMenu=opener;
    internal int Counter=>_manager?.Counter??0;
    public bool HasState=>_manager is not null;
    public bool BlocksGameplay=>false;
    public bool ScreenTransitionsDisabled=>_controller is { Finished:false,Substate:>=3 and <=6 };
    public bool AllScreenTransitionsDisabled=>ScreenTransitionsDisabled;
    internal WildTokayGameEvent(RoomEventContext context,TokayInteractionDatabase interactions,WildTokayGameDatabase database)
    {Context=context;Interactions=interactions;Database=database;_resources=new(context,this);}
    internal void OnRoomLoaded(int group,OracleRoomData room)
    {
        Cancel();
        var slot=Context.Entities.EntityAdapters<TokayNpcRoomEntity>().SingleOrDefault(s=>s.Npc.Record.SubId is 0x0d or 0x19);
        if(slot is null)return;
        _manager=new(this,slot);
        slot.NativeUpdate=_manager.Advance;
    }
    internal bool TryInteractNpc(NpcCharacter npc)=>_manager?.QueueButton(npc)==true;
    public void UpdateFrame()
    {
        if(_fadeOwned&&!Context.Transitions.NativePaletteFadeActive&&_controller is {Substate:>=2})
        {_resources.ReleaseFullScreenFade();_fadeOwned=false;}
    }
    internal void AdvanceBeforeObjects(){if(_fadeOwned)Context.Transitions.AdvanceNativeWhiteFade();}
    internal void BeginFade(bool fadeOut)
    {
        _resources.CaptureFullScreenFade(Context.Hud.ZIndex+1);_fadeOwned=true;
        Context.Transitions.BeginNativeWhiteFade(fadeOut);
    }
    internal bool TryStartController(TokayNpcRoomEntity manager)
    {
        if(!Context.Entities.InteractionSlotAvailable)return false;
        _controller=new(this,manager.Npc.BaseRecord);Context.Entities.AddEntity(_controller);
        Context.Sound.PlaySound(SoundId.SndCtrlMediumFadeOut);BeginFade(true);return true;
    }
    public void Cancel()
    {
        _manager?.Cancel();_manager=null;
        _controller?.Cancel();_controller=null;
        if(_fadeOwned){Context.Transitions.CancelNativeWhiteFade();_resources.ReleaseFullScreenFade();_fadeOwned=false;}
    }
    internal static bool CanParticipantCatch(Vector2 actor,Vector2 meat)=>
        (((int)Mathf.Floor(meat.X)-(int)Mathf.Floor(actor.X)+10)&255)<=20&&
        (((int)Mathf.Floor(meat.Y)-(int)Mathf.Floor(actor.Y)+10)&255)<=20;
}
