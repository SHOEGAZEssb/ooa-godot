using Godot;

namespace oracleofages;

internal sealed class PalacePossessionEvent(RoomEventContext context, System.Action beginMakuWarp) : IRoomEvent
{
    private readonly RoomEventResources _resources = new(context,new object());
    private bool _warpRequested;
    public bool HasState { get; private set; }
    public bool BlocksGameplay => false;
    internal bool PaletteActive => context.Transitions.NativePaletteFadeActive;
    internal void Fade(bool fadeOut,int delay)
    {
        HasState=true; _resources.CaptureFullScreenFade();
        context.Transitions.BeginNativeWhiteFade(fadeOut,delay);
    }
    internal void BeginRescue() {HasState=true;context.Transitions.ResetCamera();}
    internal void RequestWarp() { HasState=true; _warpRequested=true; }
    internal void ReleaseForNativeContinuation(bool restoreColor=true){HasState=false;_warpRequested=false;_resources.ReleaseFullScreenFade(restoreColor);}
    internal void AdvanceBeforeObjects()
    {
        if (!HasState) return;
        if (_warpRequested)
        {
            _warpRequested=false;
            // CUTSCENE_NAYRU_WARP_TO_MAKU_TREE takes ownership on the update
            // after INTERAC $6e:$00 writes its trigger.
            HasState=false;_resources.ReleaseFullScreenFade();beginMakuWarp();
            return;
        }
        context.Transitions.AdvanceNativeWhiteFade();
    }
    public void UpdateFrame() { }
    public void Cancel()
    {
        if (!HasState) return;
        HasState=false; _warpRequested=false;
        context.Transitions.CancelNativeWhiteFade(); _resources.ReleaseFullScreenFade();
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,0);
    }
}
