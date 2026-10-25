using Godot;
using System;

namespace Oracles;

// CUTSCENE_BOOTED_FROM_PALACE $14 / miscCutscenes.s:func_03_6275.
internal sealed class PalaceBootEvent(RoomEventContext context) : IRoomEvent
{
    private readonly RoomEventResources _resources = new(context, new object());
    private readonly string _message = GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/palace_boot_text.tsv",
        new GeneratedTableSchema("palace guard warning", GeneratedTableKeySemantics.Unique,
            ["text-id", "text-base64", "source"], ["text-id"], headerRequired: true)).Rows[0].Base64Utf8(1);
    internal Action PrepareEntrance { get; set; } = null!;
    internal Action AdvanceEntrance { get; set; } = null!;
    internal int State { get; private set; }
    public bool HasState { get; private set; }
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => HasState;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    public bool OwnsUpdatesOf(IRoomEvent other) => other is DekuForestPalaceEvent;
    internal void Begin()
    {
        if (HasState) return;
        HasState = true; State = 0;
    }
    internal void AdvanceBeforeObjects()
    {
        if (!HasState) return;
        context.Transitions.AdvanceNativeWhiteFade();
        switch (State)
        {
            case 0:
                _resources.CaptureFullScreenFade();
                context.Transitions.BeginNativeWhiteFade(true); State++; break;
            case 1:
                if (context.Transitions.NativePaletteFadeActive) break;
                context.Player.ClearNativeItemParents(); context.InterruptBracelet(false);
                PrepareEntrance();
                // The original clears screen variables/WRAM1, then calls
                // func_131f. It preserves shared $cc8a instead of taking
                // disableLcdAndLoadRoom's broader shared-variable clear.
                var room = context.Rooms.LoadCutsceneBackground(1, 0x46);
                // clearScreenVariablesAndWramBank1 clears Link's whole page;
                // state$0f writes the visible throw position two updates later.
                context.Player.WarpTo(Vector2.Zero, recordSafe: false);
                context.RoomView.SetRoom(room.Texture); context.Transitions.ResetCamera();
                context.Entities.LoadNativeCutsceneRoomObjects(1, room);
                context.Sound.PlayRoomMusic(1, 0x46, context.Rooms.SaveData);
                context.Transitions.BeginNativeWhiteFade(false); State++; break;
            case 2:
                context.Player.RequestPalaceToss(); State++; break;
            case 3:
                if (context.Transitions.NativePaletteFadeActive || context.Player.PalaceTossSubstate != 2) break;
                context.ShowDialogue(_message, sourceTextId: 0x590a); State++; break;
            case 4:
                if (context.DialogueOpen) break;
                context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
                context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableScreenTransitions, 0);
                context.Sound.PlayRoomMusic(1, 0x46, context.Rooms.SaveData);
                HasState = false; _resources.ReleaseFullScreenFade(); break;
        }
    }
    public void UpdateFrame() => AdvanceEntrance();
    public void Cancel()
    {
        if (!HasState) return;
        HasState = false; State = 0;
        context.Player.CancelPalaceToss(); context.Transitions.CancelNativeWhiteFade();
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisableScreenTransitions, 0);
        _resources.ReleaseFullScreenFade();
    }
}
