using System.Linq;

namespace Oracles;

internal sealed partial class RoomOfRitesZeldaEvent
{
    private readonly RoomEventResources _resources = new(context, new object());
    internal bool WarpActive { get; private set; }
    internal int WarpState { get; private set; }
    private int _waveAmplitude;
    private RoomOfRitesZeldaActor? _warpZelda;
    private byte Read(int address) => context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    private bool Decrement(int address) { Write(address, Read(address) - 1); return Read(address) == 0; }
    private void BeginApproach() { WarpActive = true; WarpState = 0; }
    internal void AdvanceBeforeObjects()
    {
        SkipsObjectUpdate = false;
        if (!WarpActive) return;
        SkipsObjectUpdate = true;
        context.Transitions.AdvanceNativeWhiteFade();
        switch (WarpState)
        {
            case 0:
                _resources.CaptureFullScreenFade();
                _warpZelda = _actors.SingleOrDefault(actor => !actor.Finished) ??
                    throw new System.InvalidOperationException("bank1.s:cutscene17 state$00 found no live Zelda $ad; native scan would write outside the interaction pool at $e01a.");
                Write(0xcbb5, 0xd0 + context.Entities.NativeInteractionSlot(_warpZelda));
                Write(0xcc04, 0); Write(0xcbb7, 0); _waveAmplitude = 0;
                WarpState = 1; context.Sound.PlayNativeSound(0xb9); return;
            case 1:
                Wave(); Write(0xcbb7, Read(0xcbb7) + 1);
                if (Read(0xcbb7) != 0) { _waveAmplitude = Read(0xcbb7); return; }
                WarpState = 2; Write(0xcbb3, 30); return;
            case 2:
                Interactions(); Wave(); Write(0xcbb4, (Read(0xcbb4) + 1) & 3);
                if (Read(0xcbb4) != 0) return;
                _warpZelda!.Visible = !_warpZelda.Visible;
                if (!Decrement(0xcbb3)) return;
                _warpZelda.Visible = false;
                Write(0xcbb4, 20); Write(0xcbb3, 5); WarpState = 3;
                goto case 3;
            case 3:
                // Native state2 falls through here, running interactions twice.
                Interactions(); Wave();
                if (!Decrement(0xcbb4)) return;
                Write(0xcbb4, 20); context.Transitions.BeginNativeWhiteFade(true);
                if (Decrement(0xcbb3)) WarpState = 4;
                return;
            case 4:
                Wave(); if (context.Transitions.NativePaletteFadeActive) return;
                Write(0xcc50, 0); Write(AgesWramAddress.wMenuDisabled, 0); Write(AgesWramAddress.wScrollMode, 1);
                WarpActive = false;
                context.RoomView.ClearHorizontalWave();
                _resources.ReleaseFullScreenFade(restoreColor: false);
                context.Sound.PlayNativeSound(0xf1); context.Sound.PlayNativeSound(0xb4);
                context.Transitions.ApplyWarpAfterNativeFadeOut(context.Player, new Warp(
                    context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id, -1, 0, 0,
                    5, 0xf5, 0x77, 0, WarpDestinationTransition.Fall));
                return;
        }
    }
    private void Wave() => context.RoomView.SetHorizontalWave(_waveAmplitude,
        (context.Entities.FrameCounter + 1) & 127, step: 2, normalEvenSamples: true,
        cameraOrigin: -context.Transitions.WorldToGameplayScreen(Godot.Vector2.Zero),
        background: context.Rooms.CurrentRoom.UnderwaterBackgroundTexture);
    private void Interactions() => context.Entities.UpdateNativeInteractionPass(context.Player,
        (context.Entities.FrameCounter + 1) & 255);
    private void CancelWarp()
    {
        if (WarpActive)
        {
            context.Transitions.CancelNativeWhiteFade();
            context.RoomView.ClearHorizontalWave();
            if (Read(AgesWramAddress.wMenuDisabled) == 0x17) Write(AgesWramAddress.wMenuDisabled, 0);
            if (Read(0xcc04) == 0x17) Write(0xcc04, 0);
        }
        _resources.ReleaseFullScreenFade();
        WarpActive = false; SkipsObjectUpdate = false; WarpState = 0; _warpZelda = null;
    }
}
