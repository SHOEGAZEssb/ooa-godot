using Godot;

namespace oracleofages;

// CUTSCENE_AMBI_PASSAGE_OPEN $1c; the button owns the initial fade request.
internal sealed class PalacePassageEvent(RoomEventContext context) : IRoomEvent
{
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly PalaceButtonDatabase _data = new();
    internal int State { get; private set; }
    private int Phase { get => context.Entities.RuntimeState.ReadWramByte(0xcbb3); set => context.Entities.RuntimeState.SetWramByte(0xcbb3, (byte)value); }
    internal int Counter { get => context.Entities.RuntimeState.ReadWramByte(0xcbb4); private set => context.Entities.RuntimeState.SetWramByte(0xcbb4, (byte)value); }
    public bool HasState { get; private set; }
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => HasState;
    public bool DisablesLink => HasState;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    internal void Begin()
    {
        HasState = true; State = 0;
        _resources.CaptureFullScreenFade(); context.Transitions.BeginNativeWhiteFade(true);
    }
    internal void AdvanceBeforeObjects()
    {
        if (!HasState) return;
        context.Transitions.AdvanceNativeWhiteFade();
        switch (State)
        {
            case 0:
                if (context.Transitions.NativePaletteFadeActive) return;
                for (int address = 0xcbb3; address < 0xcbbb; address++) context.Entities.RuntimeState.SetWramByte(address, 0);
                Counter = 60; State = 1;
                var room = context.Rooms.LoadCutsceneRoom(1, 0x27);
                context.Entities.LoadCutsceneRoom(1, room, includeTimePortals: false);
                context.RoomView.SetRoom(room.Texture); context.Transitions.ResetCamera();
                context.Player.WarpTo(Vector2.Zero, recordSafe: false); context.Player.Visible = false;
                context.Transitions.BeginNativeWhiteFade(false); return;
            case 1 when Phase == 0:
                if (context.Transitions.NativePaletteFadeActive) return;
                Counter = (Counter - 1) & 255;
                if (Counter != 0) return;
                Counter = 62;
                var button = _data.Subid(context.Entities.RuntimeState.ReadWramByte(0xcbbd));
                // Failed allocation retries after the original counter wraps.
                if (context.Entities.TryCreateSynchronizedBlock(button.GatePosition, button.Angle)) Phase = 1;
                return;
            case 1:
                Counter = (Counter - 1) & 255;
                if (Counter == 0) { Counter = 30; State = 2; context.Sound.PlaySound(SoundId.SndSolvePuzzle); }
                return;
            case 2:
                Counter = (Counter - 1) & 255;
                if (Counter != 0) return;
                int origin = context.Entities.RuntimeState.ReadWramByte(0xcbbb);
                int position = context.Entities.RuntimeState.ReadWramByte(0xcbbc);
                context.Rooms.SaveData.SetRoomFlag(1, origin, 0x80);
                context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0);
                context.Transitions.CancelNativeWhiteFade();
                _resources.ReleaseFullScreenFade(); HasState = false;
                context.Player.Visible = true;
                context.Transitions.ApplyWarpWithFadeOut(context.Player,
                    new Warp(1, 0x27, 0, 0, 0, 1, origin, position, 0, 0));
                return;
        }
    }
    public void UpdateFrame() { }
    public void Cancel()
    {
        if (!HasState) return;
        HasState = false; State = 0; context.Player.Visible = true;
        context.Transitions.CancelNativeWhiteFade(); _resources.ReleaseFullScreenFade();
        context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0);
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0);
    }
}
