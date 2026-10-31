using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// CUTSCENE_FAIRIES_HIDE owns the main-thread state. Placed $6c controllers,
// $49 actors and $84 sparkles retain their own native slots and dispatch.
internal sealed class FairiesWoodsEvent : RoomCutsceneCommandHost, IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly FairiesWoodsDatabase _database;
    private readonly FairiesWoodsEventRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly List<FairyHidingMinigameRoomEntity> _controllers = new();
    private readonly List<ForestFairyFlight> _flights = new();
    private readonly List<NpcCharacter> _discovered = new();
    private NativeScriptInputControl? _inputControl;
    private bool _ownsPlayerVisibility, _originalPlayerVisible;

    internal FairiesWoodsEvent(RoomEventContext context)
    {
        _context = context;
        _database = context.Entities.FairiesWoodsData;
        _record = _database.Event;
        _runtime = context.Entities.RuntimeState;
    }

    public override RoomEventContext Context => _context;
    public bool HasState => NativeActive || _runtime.ReadField("wCutsceneTrigger") == 0x13 || _controllers.Any(controller => !controller.Finished) ||
        _flights.Any(flight => flight.Active) || _discovered.Any(IsLive);
    public bool RequiresRoomEventUpdate => false;
    public bool BlocksGameplay => _inputControl?.LinkDisabled == true ||
        _controllers.Any(controller => !controller.Finished && controller.BlocksGameplay);
    internal bool NativeActive => _runtime.ReadWramByte(NativeCutsceneRequest.IndexAddress) == 0x13;
    private int NativeState
    {
        get => _runtime.ReadField("wCutsceneState");
        set => _runtime.SetField("wCutsceneState", unchecked((byte)value));
    }
    internal int CompletionCounter
    {
        get => _runtime.ReadField("wTmpcbb6");
        private set => _runtime.SetField("wTmpcbb6", unchecked((byte)value));
    }
    public bool OwnsGameLogic => NativeActive;
    public bool SuppressesStatusBar => NativeActive;
    public bool SuppressesNormalMenuDispatch => NativeActive;
    public bool DisablesLink => NativeActive && _context.Rooms.CurrentRoom.Id != _record.StartRoom;
    internal FairiesWoodsStage Stage => NativeActive ? NativeStage :
        _controllers.LastOrDefault(controller => !controller.Finished)?.Stage ??
        (Active != 0 && _context.Rooms.CurrentRoom.Id == _record.StartRoom
            ? FairiesWoodsStage.SearchRoom : FairiesWoodsStage.Inactive);
    internal int FoundFairies => Found;
    internal int SignalValue => Signal;
    internal int HiddenCounter => _controllers.LastOrDefault(controller => !controller.Finished)?.Counter ?? 0;
    private FairiesWoodsStage NativeStage => Found == 7 ? NativeState switch
    {
        0 => FairiesWoodsStage.CompletionShowInitial,
        1 => FairiesWoodsStage.CompletionWaitInitial,
        2 => _context.Transitions.NativePaletteFadeActive ? FairiesWoodsStage.CompletionFastFade1 : FairiesWoodsStage.CompletionHold1,
        3 => _context.Transitions.NativePaletteFadeActive ? FairiesWoodsStage.CompletionFastFade2 : FairiesWoodsStage.CompletionHold2,
        4 => FairiesWoodsStage.CompletionSlowFade,
        5 => FairiesWoodsStage.CompletionFinalize,
        _ => throw new NotSupportedException($"miscCutscenes.s: fairy completion state ${NativeState:x2}.")
    } : NativeState switch
    {
        0 => FairiesWoodsStage.HidingBegin,
        1 => FairiesWoodsStage.HidingFadeOut,
        2 or 5 or 8 => FairiesWoodsStage.HidingFadeIn,
        3 or 6 or 9 => FairiesWoodsStage.HidingFlight,
        4 or 7 or 10 => FairiesWoodsStage.HidingRoomFadeOut,
        11 => FairiesWoodsStage.HidingReturnFadeIn,
        _ => throw new NotSupportedException($"miscCutscenes.s: fairy hiding state ${NativeState:x2}.")
    };
    internal IReadOnlyList<ForestFairyFlight> Flights
    {
        get { _flights.RemoveAll(flight => !flight.Active); return _flights; }
    }
    internal FairiesWoodsDatabase Database => _database;
    private static bool IsLive(NpcCharacter actor) => GodotObject.IsInstanceValid(actor) && actor.Active;

    internal void RegisterController(FairyHidingMinigameRoomEntity controller)
    {
        if (!_controllers.Contains(controller)) _controllers.Add(controller);
    }

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        if (group == _record.Group && room.Id == _record.ResetRoom &&
            !_context.Rooms.SaveData.HasGlobalFlag(_record.CompletionFlag))
            for (int offset = 0; offset < 0x10; offset++)
                _runtime.SetWramByte(_record.ActiveAddress + offset, 0);
    }

    public void ReleaseOutgoingActors(int group, OracleRoomData room)
    {
        if (HasState && _context.Entities.ScreenTransitionActive)
            Cancel(deactivateDiscoveredActors: false);
    }

    internal void RequestNativeCutscene(NativeScriptInputControl input)
    {
        // The departing interaction and main-thread handler share one writer.
        _inputControl = input;
        _runtime.SetField("wCutsceneTrigger", 0x13);
    }

    internal void AdvanceBeforeObjects()
    {
        if (!NativeActive) return;
        _context.Transitions.AdvanceNativeWhiteFade();
        if (Found == 7) { AdvanceCompletion(); return; }
        switch (NativeState)
        {
            case 0:
                _runtime.SetField("wTmpcbb3", (byte)Mathf.FloorToInt(_context.Player.Position.Y));
                _runtime.SetField("wTmpcbb4", (byte)Mathf.FloorToInt(_context.Player.Position.X));
                Vector2I facing = _context.Player.FacingVector;
                _runtime.SetField("wTmpcbb5", (byte)(facing == Vector2I.Up ? 0 : facing == Vector2I.Right ? 1 : facing == Vector2I.Down ? 2 : 3));
                CapturePlayerVisibility();
                EventResources.CaptureFullScreenFade(zIndex: ScreenDrawPriority.DialogueBackdropZIndex);
                _context.Transitions.BeginNativeWhiteFade(true);
                NativeState++;
                break;
            case 1:
            case 4:
            case 7:
                if (_context.Transitions.NativePaletteFadeActive) break;
                Signal = 0;
                LoadCutsceneRoom(_database.HidingRooms[(NativeState - 1) / 3].Room);
                NativeState++;
                _context.Transitions.BeginNativeWhiteFade(false);
                break;
            case 2:
            case 5:
            case 8:
                if (_context.Transitions.NativePaletteFadeActive) break;
                if (SpawnFlight(_database.HidingRooms[(NativeState - 2) / 3].Preset) is not null) NativeState++;
                break;
            case 3:
            case 6:
            case 9:
                if (Signal != 0)
                {
                    Signal = 0;
                    _context.Transitions.BeginNativeWhiteFade(true);
                    NativeState++;
                    SetFadeAlpha(0.0f);
                }
                break;

            case 10:
                if (_context.Transitions.NativePaletteFadeActive) break;
                Signal = 0;
                LoadCutsceneRoom(_record.StartRoom);
                NativeState++;
                _context.Player.SetScriptedPosition(new(_runtime.ReadField("wTmpcbb4"), _runtime.ReadField("wTmpcbb3")));
                _context.Player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(_runtime.ReadField("wTmpcbb5") * 8));
                RestorePlayerVisibility();
                _context.Player.RequestNativeLinkInitialization();
                _context.Transitions.BeginNativeWhiteFade(false);
                break;
            case 11:
                if (_context.Transitions.NativePaletteFadeActive) break;
                ReleaseNativeControl();
                ShowText(0x1104);
                break;
            default:
                throw new NotSupportedException($"miscCutscenes.s: fairy hiding state ${NativeState:x2}.");
        }
    }

    private void AdvanceCompletion()
    {
        switch (NativeState)
        {
            case 0:
                if (_context.Transitions.PaletteFadeActive) break;
                EventResources.CaptureFullScreenFade(zIndex: ScreenDrawPriority.DialogueBackdropZIndex);
                SetFadeAlpha(0.0f);
                ShowText(0x110a);
                NativeState++;
                break;

            case 1:
                if (!_context.Entities.TextActiveSource())
                    BeginCompletionFastFade();
                break;

            case 2:
                if (_context.Transitions.NativePaletteFadeActive) break;
                if (--CompletionCounter == 0) BeginCompletionFastFade();
                break;
            case 3:
                if (_context.Transitions.NativePaletteFadeActive) break;
                if (--CompletionCounter == 0)
                {
                    NativeState++;
                    Active = 0;
                    _context.Sound.PlaySound(_record.MysterySound);
                    _context.Transitions.BeginNativeWhiteFade(false, _record.DelayedFadeRefill, _record.NormalFadeSpeed);
                }
                break;

            case 4:
                if (!_context.Transitions.NativePaletteFadeActive)
                {
                    ShowText(0x110b);
                    NativeState++;
                }
                break;

            case 5:
                FinishCompletion();
                break;
            default:
                throw new NotSupportedException($"miscCutscenes.s: fairy completion state ${NativeState:x2}.");
        }
    }

    public void UpdateFrame() =>
        throw new InvalidOperationException("CUTSCENE $13 must run before objects; $6c/$49 use their native interaction slots.");

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_discovered.Contains(npc) || !IsLive(npc) || npc.Record.TextId == 0) return false;
        return _context.Entities.EntityAdapters<DiscoveredForestFairyRoomEntity>().FirstOrDefault(actor =>
            ReferenceEquals(actor.Actor, npc))?.TryQueueAButton() == true;
    }

    public void Cancel() => Cancel(deactivateDiscoveredActors: true);
    internal void Cancel(bool deactivateDiscoveredActors)
    {
        if (NativeActive)
        {
            _context.Transitions.CancelNativeWhiteFade();
            _runtime.SetWramByte(NativeCutsceneRequest.IndexAddress, 1);
        }
        if (_runtime.ReadField("wCutsceneTrigger") == 0x13) _runtime.SetField("wCutsceneTrigger", 0);
        foreach (var controller in _controllers) controller.Cancel();
        _controllers.Clear();
        foreach (var flight in _flights) flight.Deactivate();
        _flights.Clear();
        if (deactivateDiscoveredActors)
            foreach (var actor in _discovered) if (IsLive(actor)) actor.SetActive(false);
        _discovered.Clear();
        _inputControl?.Cancel(); _inputControl = null;
        RestorePlayerVisibility();
        EventResources.ReleaseFullScreenFade();
    }

    internal bool TrySpawnDiscoveredFairy(int index)
    {
        var entity = _context.Entities.TryAllocateInteraction(() =>
            new DiscoveredForestFairyRoomEntity(_context, _database, index));
        if (entity is null) return false;
        _discovered.Add(entity.Actor);
        return true;
    }

    private ForestFairyFlight? SpawnFlight(int preset) => TrySpawnFlight(preset);
    internal ForestFairyFlight? TrySpawnFlight(int preset)
    {
        var flight = ForestFairyFlight.TryCreate(_context, _database, preset, 0,
            $"FairiesWoods_00_{preset:x2}");
        if (flight is not null) _flights.Add(flight);
        return flight;
    }

    internal void ShowSourceText(int textId) => ShowText(textId);

    private void LoadCutsceneRoom(int room)
    {
        foreach (ForestFairyFlight flight in _flights)
            flight.Deactivate();
        _flights.Clear();
        _context.Rooms.ClearScreenVariables();
        OracleRoomData loaded = _context.Rooms.LoadCutsceneBackground(
            _record.Group, room);
        _runtime.SetField("wScrollMode", 1);
        _context.Player.WarpTo(Vector2.Zero, recordSafe: false);
        _context.Player.Visible = false;
        _context.RoomView.SetRoom(loaded.Texture);
        _context.Entities.LoadCutsceneRoom(
            _record.Group, loaded, includeTimePortals: false);
        _context.Transitions.ResetCamera();
        SetFadeAlpha(1.0f);
    }


    private void BeginCompletionFastFade()
    {
        NativeState++;
        CompletionCounter = _record.CompletionHold;
        _context.Sound.PlaySound(_record.MysterySound);
        _context.Transitions.BeginNativeWhiteFade(false, speed: _record.FastFadeSpeed);
    }

    private void FinishCompletion()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.CompletionFlag);
        _context.Rooms.SaveData.SetGlobalFlag(_record.UnscrambledFlag);
        ReleaseNativeControl();
    }

    private void ReleaseNativeControl()
    {
        (_inputControl ??= new(_runtime)).SetMenuDisabled(0);
        _inputControl.SetDisabledObjects(0);
        _runtime.SetWramByte(NativeCutsceneRequest.IndexAddress, 1);
        EventResources.ReleaseFullScreenFade();
    }


    private void SetFadeAlpha(float alpha)
    {
        EventResources.CaptureFullScreenFade(zIndex: ScreenDrawPriority.DialogueBackdropZIndex);
        _context.Fade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1));
    }

    private void CapturePlayerVisibility()
    {
        if (_ownsPlayerVisibility)
            return;
        _ownsPlayerVisibility = true;
        _originalPlayerVisible = _context.Player.Visible;
    }

    private void RestorePlayerVisibility()
    {
        if (!_ownsPlayerVisibility)
            return;
        _context.Player.Visible = _originalPlayerVisible;
        _ownsPlayerVisibility = false;
    }


    private void ShowText(int textId)
    {
        FairiesWoodsTextRecord text = _database.Text(textId);
        if (textId == 0x110c)
            _context.ShowChoiceDialogue(text.Message);
        else
            _context.ShowDialogue(text.Message);
    }

    private byte Active
    {
        get => _runtime.ReadWramByte(_record.ActiveAddress);
        set => _runtime.SetWramByte(_record.ActiveAddress, value);
    }

    private byte Found
    {
        get => _runtime.ReadWramByte(_record.FoundAddress);
        set => _runtime.SetWramByte(_record.FoundAddress, value);
    }

    private byte Signal
    {
        get => _runtime.ReadWramByte(_record.SignalAddress);
        set => _runtime.SetWramByte(_record.SignalAddress, value);
    }


}

internal enum FairiesWoodsStage
{
    Inactive,
    ControllerInitialize,
    StartPending,
    IntroScript,
    SearchRoom,
    HiddenWatch,
    HiddenSpawn,
    RevealScript,
    AwaitLastFairyWarp,
    ExitScript,
    CompletionPending,
    HidingBegin,
    HidingFadeOut,
    HidingFadeIn,
    HidingFlight,
    HidingRoomFadeOut,
    HidingReturnFadeIn,
    CompletionShowInitial,
    CompletionWaitInitial,
    CompletionFastFade1,
    CompletionHold1,
    CompletionFastFade2,
    CompletionHold2,
    CompletionSlowFade,
    CompletionFinalize
}
