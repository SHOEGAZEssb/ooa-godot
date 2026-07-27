using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_FAIRY_HIDING_MINIGAME $6c, its three $49 forest fairies, and
/// CUTSCENE_FAIRIES_HIDE. Transient cfd0-cfdf state deliberately survives
/// ordinary forest room changes and is reset only by room $0:$93 or quitting.
/// </summary>
internal sealed class FairiesWoodsEvent :
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private static readonly HashSet<int> ForestRooms =
        [0x70, 0x71, 0x72, 0x80, 0x81, 0x82, 0x90, 0x91, 0x92];

    private readonly RoomEventContext _context;
    private readonly FairiesWoodsDatabase _database = new();
    private readonly FairiesWoodsEventRecord _record;
    private readonly OracleRuntimeState _runtime;
    private readonly CutsceneCommandRunner _runner;
    private readonly List<ForestFairyFlight> _flights = new();
    private readonly Dictionary<NpcCharacter, int> _discovered = new();
    private FairiesWoodsStage _stage;
    private FairyScriptKind _scriptKind;
    private FairiesWoodsSparkleLayer? _sparkles;
    private FairiesWoodsHiddenSpotRecord _hiddenSpot;
    private byte _hiddenOriginalTile;
    private int _hiddenCounter;
    private int _fadeCounter;
    private int _hideRoomIndex;
    private int _completionCounter;
    private int _forcedLeftCounter;
    private bool _inputLocked;
    private bool _screenTransitionsDisabled;
    private Vector2 _savedLinkPosition;
    private Vector2I _savedLinkFacing;
    private Vector2 _originalFadePosition;
    private Vector2 _originalFadeSize;
    private int _originalFadeZ;
    private Color _originalFadeColor;
    private bool _ownsFade;
    private bool _ownsPlayerVisibility;
    private bool _originalPlayerVisible;

    internal FairiesWoodsEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Event;
        _runtime = context.Entities.RuntimeState;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState =>
        _stage != FairiesWoodsStage.Inactive ||
        _forcedLeftCounter != 0 ||
        (_sparkles?.Count ?? 0) != 0;
    public bool BlocksGameplay => _inputLocked || _forcedLeftCounter != 0;
    internal bool ScreenTransitionsDisabled => _screenTransitionsDisabled;
    internal FairiesWoodsStage Stage => _stage;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int FoundFairies => Found;
    internal int SignalValue => Signal;
    internal int HiddenCounter => _hiddenCounter;
    internal IReadOnlyList<ForestFairyFlight> Flights => _flights;
    internal FairiesWoodsDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group &&
        (room.Id == _record.StartRoom ||
         room.Id == _record.ExitRoom ||
         _database.TryHiddenSpot(room.Id, out _));

    /// <summary>
    /// Applies roomCode0 and the state-zero deletion gates of forest fairy
    /// variants before the controller selects a room-entry event.
    /// </summary>
    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        if (group != _record.Group)
            return;
        if (room.Id == _record.ResetRoom && !Completed)
            ClearTransientState();
        if (!ForestRooms.Contains(room.Id))
            return;
        // Placed $49:$05-$10 actors belong to the later Jabu, companion, and
        // linked-game phases. Keep every ordinary variant retired until its
        // native state-0 predicate and loaded-text table are supported.
        for (int subId = 0; subId <= 0x10; subId++)
            _context.DeactivateNpcs(0x49, subId);
    }

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (Completed)
            return;
        EnsureSparkleLayer();

        if (room.Id == _record.StartRoom)
        {
            StartMainRoom();
            return;
        }
        if (Active == 0)
            return;
        if (_database.TryHiddenSpot(room.Id, out _hiddenSpot))
        {
            int bit = 1 << (_hiddenSpot.FairyIndex - 3);
            if ((Found & bit) != 0)
                return;
            Signal = 0;
            _hiddenOriginalTile = room.GetMetatile(
                PointForPackedPosition(_hiddenSpot.PackedPosition));
            _hiddenCounter = _record.HiddenDelay;
            _stage = FairiesWoodsStage.HiddenWatch;
            return;
        }
        if (room.Id == _record.ExitRoom)
        {
            _scriptKind = FairyScriptKind.Exit;
            _runner.Start(_database.ExitCommands);
            _stage = FairiesWoodsStage.ExitScript;
        }
    }

    public void UpdateFrame()
    {
        _sparkles?.UpdateFrame();
        UpdateForcedLeft();

        switch (_stage)
        {
            case FairiesWoodsStage.StartPending:
                if (LinkIsVulnerable())
                {
                    Vector2I entryDirection =
                        _context.Transitions.ScrollDirection;
                    if (entryDirection == Vector2I.Up ||
                        entryDirection == Vector2I.Right ||
                        entryDirection == Vector2I.Down ||
                        entryDirection == Vector2I.Left)
                    {
                        _context.Player.Face(entryDirection);
                    }
                    LockInput();
                    Active = 1;
                    _scriptKind = FairyScriptKind.Intro;
                    _runner.Start(_database.IntroCommands);
                    _stage = FairiesWoodsStage.IntroScript;
                    _runner.AdvanceFrame();
                }
                break;

            case FairiesWoodsStage.IntroScript:
            case FairiesWoodsStage.RevealScript:
            case FairiesWoodsStage.ExitScript:
                _runner.AdvanceFrame();
                break;

            case FairiesWoodsStage.HiddenWatch:
                UpdateHiddenSpot();
                break;

            case FairiesWoodsStage.HiddenSpawn:
                SpawnRevealedFairy();
                break;

            case FairiesWoodsStage.HidingFadeOut:
                UpdateHidingFadeOut();
                break;

            case FairiesWoodsStage.HidingFadeIn:
                UpdateHidingFadeIn();
                break;

            case FairiesWoodsStage.HidingFlight:
                if (Signal != 0)
                {
                    Signal = 0;
                    _fadeCounter = 0;
                    _stage = FairiesWoodsStage.HidingRoomFadeOut;
                    SetFadeAlpha(0.0f);
                }
                break;

            case FairiesWoodsStage.HidingRoomFadeOut:
                UpdateHidingRoomFadeOut();
                break;

            case FairiesWoodsStage.HidingReturnFadeIn:
                UpdateHidingReturnFadeIn();
                break;

            case FairiesWoodsStage.CompletionShowInitial:
                OwnFullScreenFade();
                SetFadeAlpha(0.0f);
                ShowText(0x110a);
                _stage = FairiesWoodsStage.CompletionWaitInitial;
                break;

            case FairiesWoodsStage.CompletionWaitInitial:
                if (!_context.DialogueOpen)
                    BeginCompletionFastFade(FairiesWoodsStage.CompletionFastFade1);
                break;

            case FairiesWoodsStage.CompletionFastFade1:
                if (UpdateFadeIn(
                        _record.FastFadeSpeed,
                        refill: 1,
                        _record.FastFadeIn))
                {
                    _completionCounter = _record.CompletionHold;
                    _stage = FairiesWoodsStage.CompletionHold1;
                }
                break;

            case FairiesWoodsStage.CompletionHold1:
                if (--_completionCounter == 0)
                    BeginCompletionFastFade(FairiesWoodsStage.CompletionFastFade2);
                break;

            case FairiesWoodsStage.CompletionFastFade2:
                if (UpdateFadeIn(
                        _record.FastFadeSpeed,
                        refill: 1,
                        _record.FastFadeIn))
                {
                    _completionCounter = _record.CompletionHold;
                    _stage = FairiesWoodsStage.CompletionHold2;
                }
                break;

            case FairiesWoodsStage.CompletionHold2:
                if (--_completionCounter == 0)
                {
                    Active = 0;
                    _context.Sound.PlaySound(_record.MysterySound);
                    _fadeCounter = 0;
                    SetFadeAlpha(1.0f);
                    _stage = FairiesWoodsStage.CompletionSlowFade;
                }
                break;

            case FairiesWoodsStage.CompletionSlowFade:
                if (UpdateFadeIn(
                        _record.NormalFadeSpeed,
                        _record.DelayedFadeRefill,
                        _record.DelayedFadeIn))
                {
                    ShowText(0x110b);
                    _stage = FairiesWoodsStage.CompletionFinalize;
                }
                break;

            case FairiesWoodsStage.CompletionFinalize:
                FinishCompletion();
                break;
        }

        UpdateFlights();
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_discovered.ContainsKey(npc))
            return false;
        int count = CountBits(Found);
        if (count == 1)
            ShowText(0x1108);
        else if (count == 2)
            ShowText(0x1109);
        return true;
    }

    public void Cancel() => Cancel(deactivateDiscoveredActors: true);

    internal void Cancel(bool deactivateDiscoveredActors)
    {
        _runner.Clear();
        _scriptKind = FairyScriptKind.None;
        foreach (ForestFairyFlight flight in _flights)
            flight.Deactivate();
        _flights.Clear();
        foreach (NpcCharacter actor in _discovered.Keys)
        {
            if (deactivateDiscoveredActors &&
                GodotObject.IsInstanceValid(actor))
            {
                actor.SetActive(false);
            }
        }
        _discovered.Clear();
        if (_inputLocked || _forcedLeftCounter != 0)
            _context.Player.EndCutsceneControl();
        _inputLocked = false;
        _screenTransitionsDisabled = false;
        _forcedLeftCounter = 0;
        _stage = FairiesWoodsStage.Inactive;
        RestorePlayerVisibility();
        RestoreFade();
        if (_sparkles is not null)
        {
            _sparkles.QueueFree();
            _sparkles = null;
        }
    }

    private void StartMainRoom()
    {
        if (!_context.Rooms.SaveData.HasTreasure(_record.EssenceTreasure))
            return;
        if (Active == 0)
        {
            _stage = FairiesWoodsStage.StartPending;
            return;
        }
        if (Found == 7)
        {
            BeginCompletion();
            return;
        }

        SpawnDiscoveredFairies();
        _stage = FairiesWoodsStage.SearchRoom;
    }

    private void SpawnDiscoveredFairies()
    {
        int found = Found;
        int count = CountBits(found);
        for (int index = 2; index >= 0; index--)
        {
            if ((found & (1 << index)) == 0)
                continue;
            FairiesWoodsDiscoveredRecord discovered =
                _database.DiscoveredFairies[index];
            FairiesWoodsTextRecord text = count == 1
                ? _database.Text(0x1108)
                : count == 2
                    ? _database.Text(0x1109)
                    : new FairiesWoodsTextRecord(0, string.Empty);
            NpcCharacter actor = SpawnFairyNpc(
                subId: 1,
                var03: index,
                discovered.Y,
                discovered.X,
                text.TextId,
                text.Message,
                talkable: true,
                solid: true);
            actor.SetBasePalette(discovered.Palette);
            actor.SetScriptAnimation(discovered.Animation);
            actor.SetScriptDrawOffset(new Vector2(0, -4));
            actor.SetCollisionRadii(4, 4);
            _discovered.Add(actor, index);
        }
    }

    private void UpdateHiddenSpot()
    {
        byte tile = _context.Rooms.CurrentRoom.GetMetatile(
            PointForPackedPosition(_hiddenSpot.PackedPosition));
        if (tile == _hiddenOriginalTile)
            return;
        _hiddenCounter = unchecked((byte)(_hiddenCounter - 1));
        if (_hiddenCounter != 0 || !LinkIsVulnerable())
            return;
        LockInput();
        _screenTransitionsDisabled = true;
        _stage = FairiesWoodsStage.HiddenSpawn;
    }

    private void SpawnRevealedFairy()
    {
        Signal = 0;
        ForestFairyFlight flight = SpawnFlight(_hiddenSpot.FairyIndex);
        SpawnPuff(flight.Actor.Position);
        _scriptKind = FairyScriptKind.Reveal;
        _runner.Start(_database.RevealCommands);
        _stage = FairiesWoodsStage.RevealScript;
    }

    private void FinishReveal()
    {
        int bit = 1 << (_hiddenSpot.FairyIndex - 3);
        Found = (byte)(Found | bit);
        _screenTransitionsDisabled = false;
        if (Found != 7)
        {
            UnlockInput();
            _stage = FairiesWoodsStage.Inactive;
            return;
        }

        _stage = FairiesWoodsStage.AwaitLastFairyWarp;
        Warp warp = new(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            -1,
            0,
            0,
            _record.Group,
            _record.StartRoom,
            0x64,
            0,
            3);
        _context.Transitions.ApplyWarp(_context.Player, warp);
    }

    private void BeginHidingCutscene()
    {
        _savedLinkPosition = _context.Player.Position;
        _savedLinkFacing = _context.Player.FacingVector;
        LockInput();
        CapturePlayerVisibility();
        OwnFullScreenFade();
        _fadeCounter = 0;
        _hideRoomIndex = 0;
        _stage = FairiesWoodsStage.HidingFadeOut;
        SetFadeAlpha(0.0f);
    }

    private void UpdateHidingFadeOut()
    {
        if (!UpdateFadeOut(
                _record.NormalFadeSpeed,
                _record.NormalFadeOut))
            return;
        _context.Player.Visible = false;
        LoadHidingRoom(0);
        _fadeCounter = 0;
        _stage = FairiesWoodsStage.HidingFadeIn;
    }

    private void UpdateHidingFadeIn()
    {
        if (!UpdateFadeIn(
                _record.NormalFadeSpeed,
                refill: 1,
                _record.NormalFadeIn))
            return;
        SpawnFlight(_database.HidingRooms[_hideRoomIndex].Preset);
        _stage = FairiesWoodsStage.HidingFlight;
    }

    private void UpdateHidingRoomFadeOut()
    {
        if (!UpdateFadeOut(
                _record.NormalFadeSpeed,
                _record.NormalFadeOut))
            return;
        _hideRoomIndex++;
        if (_hideRoomIndex < _database.HidingRooms.Count)
        {
            LoadHidingRoom(_hideRoomIndex);
            _fadeCounter = 0;
            _stage = FairiesWoodsStage.HidingFadeIn;
            return;
        }

        LoadCutsceneRoom(_record.StartRoom);
        _context.Player.SetScriptedPosition(_savedLinkPosition);
        _context.Player.Face(_savedLinkFacing);
        RestorePlayerVisibility();
        _fadeCounter = 0;
        _stage = FairiesWoodsStage.HidingReturnFadeIn;
    }

    private void UpdateHidingReturnFadeIn()
    {
        if (!UpdateFadeIn(
                _record.NormalFadeSpeed,
                refill: 1,
                _record.NormalFadeIn))
            return;
        UnlockInput();
        RestoreFade();
        ShowText(0x1104);
        _stage = FairiesWoodsStage.SearchRoom;
    }

    private void LoadHidingRoom(int index)
    {
        if (index < 0 || index >= _database.HidingRooms.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        Signal = 0;
        LoadCutsceneRoom(_database.HidingRooms[index].Room);
    }

    private void LoadCutsceneRoom(int room)
    {
        foreach (ForestFairyFlight flight in _flights)
            flight.Deactivate();
        _flights.Clear();
        _discovered.Clear();
        _sparkles?.Clear();
        OracleRoomData loaded = _context.Rooms.LoadCutsceneRoom(
            _record.Group, room);
        _context.RoomView.SetRoom(loaded.Texture);
        _context.Entities.LoadCutsceneRoom(
            _record.Group, loaded, includeTimePortals: false);
        _context.Transitions.UpdateCamera();
        SetFadeAlpha(1.0f);
    }

    private void BeginCompletion()
    {
        LockInput();
        _context.Player.Face(Vector2I.Up);
        _stage = FairiesWoodsStage.CompletionShowInitial;
    }

    private void BeginCompletionFastFade(FairiesWoodsStage stage)
    {
        _completionCounter = _record.CompletionHold;
        _context.Sound.PlaySound(_record.MysterySound);
        _fadeCounter = 0;
        SetFadeAlpha(1.0f);
        _stage = stage;
    }

    private void FinishCompletion()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.CompletionFlag);
        _context.Rooms.SaveData.SetGlobalFlag(_record.UnscrambledFlag);
        UnlockInput();
        RestoreFade();
        _stage = FairiesWoodsStage.Inactive;
    }

    private void UpdateFlights()
    {
        for (int index = 0; index < _flights.Count; index++)
        {
            ForestFairyFlight flight = _flights[index];
            flight.UpdateFrame(_context.Entities.FrameCounter);
            if (!flight.Active)
                _flights.RemoveAt(index--);
        }
    }

    private ForestFairyFlight SpawnFlight(int preset)
    {
        FairiesWoodsMovementRecord movement = _database.Movements[preset];
        NpcCharacter actor = SpawnFairyNpc(
            subId: 0,
            var03: preset,
            movement.InitialY,
            movement.InitialX,
            0,
            string.Empty,
            talkable: false,
            solid: false);
        actor.SetAnimationRate(0.0f);
        actor.SetBlocksLink(false);
        actor.SetCollisionRadii(4, 4);
        var flight = new ForestFairyFlight(
            _database,
            _runtime,
            _context.Sound,
            EnsureSparkleLayer(),
            actor,
            preset,
            SpawnPuff);
        _flights.Add(flight);
        return flight;
    }

    private void SpawnPuff(Vector2 position)
    {
        PuzzlePuffEffect puff = _context.Entities.Spawn<PuzzlePuffEffect>(
            new PuzzlePuffSpawn(position, _record.PuffSound));
        // The outgoing $49 slot precedes its newly allocated $05 puff slot, so
        // the puff receives state 0 in this same original interaction pass.
        puff.UpdateFrame();
    }

    private NpcCharacter SpawnFairyNpc(
        int subId,
        int var03,
        int y,
        int x,
        int textId,
        string message,
        bool talkable,
        bool solid)
    {
        var record = new NpcRecord(
            _record.Group,
            _context.Rooms.CurrentRoom.Id,
            0x49,
            subId,
            y,
            x,
            var03,
            textId,
            _record.FairySprite,
            _record.FairyTileBase,
            0,
            0,
            false,
            _record.Animation0,
            _record.Animation0,
            _record.Animation0,
            _record.Animation0,
            message,
            NpcImplementationClassification.EventOwned);
        return _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                record,
                $"FairiesWoods_{subId:x2}_{var03:x2}",
                Talkable: talkable,
                Solid: solid));
    }

    private FairiesWoodsSparkleLayer EnsureSparkleLayer()
    {
        if (_sparkles is not null &&
            GodotObject.IsInstanceValid(_sparkles))
        {
            return _sparkles;
        }
        _sparkles = new FairiesWoodsSparkleLayer
        {
            Name = "FairiesWoodsSparkles"
        };
        _sparkles.Initialize(_record);
        _context.RoomView.GetParent().AddChild(_sparkles);
        return _sparkles;
    }

    private void UpdateForcedLeft()
    {
        if (_forcedLeftCounter == 0)
            return;
        _context.Player.AdvanceCutsceneInput(Vector2I.Left);
        _forcedLeftCounter--;
        if (_forcedLeftCounter == 0 && !_inputLocked)
            _context.Player.EndCutsceneControl();
    }

    private bool ExitCollision()
    {
        Vector2 delta = _context.Player.Position -
            new Vector2(_record.ExitX, _record.ExitY);
        return Mathf.Abs(delta.Y) <
                _record.ExitRadiusY + NpcCharacter.LinkCollisionRadius &&
            Mathf.Abs(delta.X) <
                _record.ExitRadiusX + NpcCharacter.LinkCollisionRadius;
    }

    private bool LinkIsVulnerable() =>
        _context.Player.InvincibilityFrames <= 0.0f &&
        _context.Player.KnockbackFrames <= 0.0f &&
        !_context.Player.IsDying &&
        !_context.Player.IsDrowning &&
        !_context.Player.IsFallingInHole &&
        !_context.DialogueOpen &&
        !_context.Player.CutsceneControlled;

    private void LockInput()
    {
        if (_inputLocked)
            return;
        _context.Player.BeginCutsceneControl();
        _inputLocked = true;
    }

    private void UnlockInput()
    {
        if (!_inputLocked)
            return;
        _context.Player.EndCutsceneControl();
        _inputLocked = false;
    }

    private bool UpdateFadeOut(int speed, int expectedUpdates)
    {
        _fadeCounter++;
        int offset = _fadeCounter * speed;
        bool complete = offset >= 0x20;
        SetFadeAlpha(complete ? 1.0f : offset / 31.0f);
        if (complete != (_fadeCounter >= expectedUpdates))
        {
            throw new InvalidOperationException(
                "Imported Fairies' Woods fade-out duration is inconsistent.");
        }
        return complete;
    }

    private bool UpdateFadeIn(int speed, int refill, int expectedUpdates)
    {
        _fadeCounter++;
        int paletteUpdates = 1 + ((_fadeCounter - 1) / refill);
        int offset = 0x20 - paletteUpdates * speed;
        bool complete = offset < 0;
        SetFadeAlpha(complete ? 0.0f : offset / 31.0f);
        if (complete != (_fadeCounter >= expectedUpdates))
        {
            throw new InvalidOperationException(
                "Imported Fairies' Woods fade-in duration is inconsistent.");
        }
        return complete;
    }

    private void OwnFullScreenFade()
    {
        if (_ownsFade)
            return;
        _ownsFade = true;
        _originalFadePosition = _context.Fade.Position;
        _originalFadeSize = _context.Fade.Size;
        _originalFadeZ = _context.Fade.ZIndex;
        _originalFadeColor = _context.Fade.Color;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = 48;
    }

    private void SetFadeAlpha(float alpha)
    {
        OwnFullScreenFade();
        _context.Fade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0, 1));
    }

    private void RestoreFade()
    {
        if (!_ownsFade)
            return;
        _context.Fade.Position = _originalFadePosition;
        _context.Fade.Size = _originalFadeSize;
        _context.Fade.ZIndex = _originalFadeZ;
        _context.Fade.Color = _originalFadeColor;
        _ownsFade = false;
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

    private void ClearTransientState()
    {
        for (int address = _record.ActiveAddress;
             address < _record.ActiveAddress + 0x10;
             address++)
        {
            _runtime.SetWramByte(address, 0);
        }
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

    private bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(_record.CompletionFlag);

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static Vector2 PointForPackedPosition(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);

    RoomEventContext ICutsceneCommandHost.Context => _context;

    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "FairyExit";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
            UnlockInput();
        else
            LockInput();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"write wDisabledObjects=${value:x2}");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    int ICutsceneCommandHost.ReadMemory(string binding) => ReadMemory(binding);

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "Fairies' Woods exit choice has no completed result.");
        }
        return choice == value;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        FairiesWoodsTextRecord imported = _database.Text(textId);
        if (message != imported.Message)
        {
            throw new InvalidOperationException(
                $"Fairies' Woods TX_{textId:x4} command payload diverged.");
        }
        ShowText(textId);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' animation ${animation:x2}");

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' movement animation ${angle:x2}");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (actor != "FairyExit" ||
            radiusY != _record.ExitRadiusY ||
            radiusX != _record.ExitRadiusX)
        {
            throw Unsupported(
                $"set actor '{actor}' collision ${radiusY:x2}/${radiusX:x2}");
        }
    }

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        if (actor != "FairyExit")
            throw Unsupported($"make actor '{actor}' button sensitive");
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        throw Unsupported($"move actor '{actor}' at ${speed:x2}/${angle:x2}");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set actor '{actor}' z={zFixed}");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        throw Unsupported($"set actor '{actor}' visible={visible}");

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding != "FairySignal")
            throw Unsupported($"write '{binding}'=${value:x2}");
        Signal = (byte)value;
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw Unsupported($"or room flag ${flag:x2}");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        if (handler.StartsWith("SpawnForestFairy:", StringComparison.Ordinal))
        {
            int preset = int.Parse(handler.AsSpan(handler.IndexOf(':') + 1));
            SpawnFlight(preset);
            return;
        }
        switch (handler)
        {
            case "ShowFairyFoundText":
                ShowText(0x1105 + CountBits(Found));
                break;
            case "MoveLinkBackLeft":
                if (!_context.Player.CutsceneControlled)
                    _context.Player.BeginCutsceneControl();
                _context.Player.Face(Vector2I.Left);
                _forcedLeftCounter = 8;
                break;
            default:
                throw Unsupported($"run native handler '{handler}'");
        }
    }

    bool ICutsceneCommandHost.UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload)
    {
        if (handler != "WaitForExitCollision" ||
            actor is not { Value: "FairyExit" } ||
            frames != 1 ||
            !string.IsNullOrEmpty(payload))
        {
            throw Unsupported($"update native handler '{handler}'");
        }
        _ = commandUpdate;
        return ExitCollision();
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        FairyScriptKind completed = _scriptKind;
        _scriptKind = FairyScriptKind.None;
        switch (completed)
        {
            case FairyScriptKind.Intro:
                BeginHidingCutscene();
                break;
            case FairyScriptKind.Reveal:
                FinishReveal();
                break;
            case FairyScriptKind.Exit:
                ClearTransientState();
                _stage = FairiesWoodsStage.Inactive;
                break;
            default:
                throw Unsupported("end an unowned script");
        }
    }

    private int ReadMemory(string binding) =>
        binding == "FairySignal"
            ? Signal
            : throw Unsupported($"read '{binding}'");

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Fairies' Woods event cannot {operation}.");
}

internal enum FairiesWoodsStage
{
    Inactive,
    StartPending,
    IntroScript,
    SearchRoom,
    HiddenWatch,
    HiddenSpawn,
    RevealScript,
    AwaitLastFairyWarp,
    ExitScript,
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

internal enum FairyScriptKind
{
    None,
    Intro,
    Reveal,
    Exit
}
