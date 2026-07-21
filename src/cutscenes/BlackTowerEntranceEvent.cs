using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs hardhat worker $58:$02 and stage 0 of the Black Tower explanation in
/// room $1:$86. Room bits $40/$80 select the return and completed phases.
/// </summary>
internal sealed class BlackTowerEntranceEvent : IRoomEntryEvent, ICutsceneCommandHost
{
    internal enum EventStage
    {
        Inactive,
        FirstScript,
        ExplanationFadeIn,
        ExplanationIntroWait,
        ExplanationDialogue,
        ExplanationPostWait,
        Aftermath
    }

    private const int FadeFrames = 32;
    private readonly RoomEventContext _context;
    private readonly BlackTowerEntranceEventDatabase _database = new();
    private readonly BlackTowerEntranceEventDatabase.EventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _guard;
    private BlackTowerExplanationScreen? _screen;
    private EventStage _stage;
    private Vector2 _guardPrecisePosition;
    private int _storedPackedPosition;
    private int _storedDirection;
    private int _phaseCounter;
    private int _fadeFrame;
    private int _flashCounter;
    private int _simulatedDirectFrames;
    private int _simulatedTailPhase;
    private int _simulatedTailFrames;
    private Vector2 _fadeOriginalSize;
    private Vector2 _fadeOriginalPosition;
    private int _fadeOriginalZIndex;
    private bool _explanationPending;

    public BlackTowerEntranceEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _stage != EventStage.Inactive;
    public bool BlocksGameplay => HasState;
    internal EventStage Stage => _stage;
    internal BlackTowerExplanationScreen? Screen => _screen;
    internal int PhaseCounter => _phaseCounter;
    internal int FlashCounter => _flashCounter;
    internal BlackTowerEntranceEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        (_context.Rooms.SaveData.ReadWramByte(0xc6bf) & _record.EssenceMask) == 0;

    public void Start(OracleRoomData _)
    {
        Cancel();
        _guard = _context.RequireNpc(
            _record.Group, _record.Room, _record.GuardId, _record.GuardSubId,
            "INTERAC_HARDHAT_WORKER");
        _guardPrecisePosition = _guard.Position;

        OracleSaveData save = _context.Rooms.SaveData;
        if (save.HasRoomFlag(_record.Group, _record.Room, (byte)_record.CompleteFlag))
            return;
        if (save.HasRoomFlag(_record.Group, _record.Room, (byte)_record.AftermathFlag))
            StartAftermath();
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_guard is null || !ReferenceEquals(npc, _guard) || HasState)
            return false;
        OracleSaveData save = _context.Rooms.SaveData;
        if (save.HasRoomFlag(_record.Group, _record.Room, (byte)_record.CompleteFlag))
            return false;
        if (save.HasRoomFlag(_record.Group, _record.Room, (byte)_record.AftermathFlag))
        {
            StartAftermath();
            return true;
        }

        _stage = EventStage.FirstScript;
        _runner.Start(_database.First);
        return true;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case EventStage.FirstScript:
                if (_explanationPending)
                {
                    _explanationPending = false;
                    BeginExplanation();
                }
                else
                {
                    _runner.AdvanceFrame();
                }
                break;
            case EventStage.ExplanationFadeIn:
                UpdateExplanationEffects();
                _fadeFrame++;
                SetWhiteFade(1.0f - _fadeFrame / (float)FadeFrames);
                if (_fadeFrame >= FadeFrames)
                {
                    _phaseCounter--;
                    _stage = EventStage.ExplanationIntroWait;
                }
                break;
            case EventStage.ExplanationIntroWait:
                UpdateExplanationEffects();
                if (--_phaseCounter <= 0)
                {
                    _context.ShowDialogue(_record.ExplanationText);
                    _stage = EventStage.ExplanationDialogue;
                }
                break;
            case EventStage.ExplanationDialogue:
                UpdateExplanationEffects();
                if (!_context.DialogueOpen)
                {
                    _phaseCounter = _record.PostWait;
                    _stage = EventStage.ExplanationPostWait;
                }
                break;
            case EventStage.ExplanationPostWait:
                UpdateExplanationEffects();
                if (--_phaseCounter <= 0)
                    ReturnToRoom();
                break;
            case EventStage.Aftermath:
                AdvanceSimulatedInput();
                _runner.AdvanceFrame();
                break;
        }
    }

    public void Cancel()
    {
        _runner.Clear();
        bool ownedPresentation = _screen is not null;
        RemoveExplanationScreen();
        if (ownedPresentation && !_context.Transitions.IsTransitioning)
            SetWhiteFade(0.0f);
        _guard = null;
        _stage = EventStage.Inactive;
        _phaseCounter = 0;
        _fadeFrame = 0;
        _flashCounter = 0;
        _simulatedDirectFrames = 0;
        _simulatedTailPhase = 0;
        _simulatedTailFrames = 0;
        _explanationPending = false;
    }

    private void StartAftermath()
    {
        _context.Player.BeginCutsceneControl();
        _stage = EventStage.Aftermath;
        _guardPrecisePosition = _guard!.Position;
        _runner.Start(_database.Aftermath);
    }

    private void BeginExplanation()
    {
        if (_screen is not null)
            throw new InvalidOperationException("Black Tower explanation is already active.");
        _screen = new BlackTowerExplanationScreen(_database);
        _context.InterfaceLayer.AddChild(_screen);
        _fadeOriginalSize = _context.Fade.Size;
        _fadeOriginalPosition = _context.Fade.Position;
        _fadeOriginalZIndex = _context.Fade.ZIndex;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = _context.Hud.ZIndex + 1;
        _context.Hud.Visible = false;
        _context.Sound.PlaySound(OracleSoundEngine.MusDisaster);
        _fadeFrame = 0;
        _phaseCounter = _record.IntroWait;
        _flashCounter = 0;
        SetWhiteFade(1.0f);
        _stage = EventStage.ExplanationFadeIn;
    }

    private void UpdateExplanationEffects()
    {
        if (_flashCounter != 0)
        {
            bool white = _flashCounter is 1 or 2 or 5 or 6 or 9 or 10;
            _screen!.SetFlashWhite(white);
            _flashCounter++;
            if (_flashCounter > 13)
            {
                _flashCounter = 0;
                _screen.SetFlashWhite(false);
            }
            return;
        }
        if ((_context.Entities.FrameCounter & 0x1f) != 0)
            return;
        if ((_context.Entities.NextRandomValue() & 0x07) != 0)
            return;
        // func_6f0b only arms wTmpcbb9; flashScreen begins next update.
        _flashCounter = 1;
        _context.Sound.PlaySound(OracleSoundEngine.SndLightning);
    }

    private void ReturnToRoom()
    {
        RemoveExplanationScreen();
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
        var warp = new WarpDatabase.Warp(
            _record.Group,
            _record.Room,
            -1,
            0,
            _record.SourceTransition,
            _record.Group,
            _record.Room,
            _storedPackedPosition,
            _storedDirection,
            _record.DestinationTransition);
        // ApplyWarp reparses room $86 synchronously. RoomEventController then
        // sees bit $40 and starts the aftermath lane on the new guard object.
        _context.Transitions.ApplyWarp(_context.Player, warp);
    }

    private void RemoveExplanationScreen()
    {
        if (_screen is null)
            return;
        if (_screen.GetParent() == _context.InterfaceLayer)
            _context.InterfaceLayer.RemoveChild(_screen);
        _screen.QueueFree();
        _screen = null;
        _context.Hud.Visible = true;
        _context.Fade.Position = _fadeOriginalPosition;
        _context.Fade.Size = _fadeOriginalSize;
        _context.Fade.ZIndex = _fadeOriginalZIndex;
    }

    private void AdvanceSimulatedInput()
    {
        if (_simulatedDirectFrames > 0)
        {
            _context.Player.AdvanceCutsceneInput(Vector2I.Down);
            _simulatedDirectFrames--;
            if (_simulatedDirectFrames == 0)
            {
                _simulatedTailPhase = 1;
                _simulatedTailFrames = 10;
            }
            return;
        }
        if (_simulatedTailPhase == 1)
        {
            _context.Player.AdvanceCutsceneInput(Vector2I.Zero);
            if (--_simulatedTailFrames <= 0)
            {
                _simulatedTailPhase = 2;
                _simulatedTailFrames = 1;
            }
            return;
        }
        if (_simulatedTailPhase == 2)
        {
            _context.Player.AdvanceCutsceneInput(Vector2I.Up);
            if (--_simulatedTailFrames <= 0)
                _simulatedTailPhase = 0;
        }
    }

    private void SetWhiteFade(float alpha) =>
        _context.Fade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));

    private static Vector2I CardinalDirection(Vector2 origin, Vector2 target)
    {
        int angle = (OracleObjectMath.AngleToward(origin, target) + 4) & 0x18;
        return angle switch
        {
            0x00 => Vector2I.Up,
            0x08 => Vector2I.Right,
            0x10 => Vector2I.Down,
            _ => Vector2I.Left
        };
    }

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0 :
        direction == Vector2I.Right ? 1 :
        direction == Vector2I.Down ? 2 : 3;

    bool ICutsceneCommandHost.DialogueOpen => _context.DialogueOpen;
    bool ICutsceneCommandHost.IsLinkedGame => _context.Rooms.SaveData.IsLinkedGame;
    int ICutsceneCommandHost.FrameCounter => _context.Entities.FrameCounter;
    ICutsceneCommandTraceSink? ICutsceneCommandHost.TraceSink =>
        _context.CommandTraceSink;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Guard";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
            _context.Player.EndCutsceneControl();
        else
            _context.Player.BeginCutsceneControl();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");
    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects=${value:x2}");
    bool ICutsceneCommandHost.GateOpen(string gate) =>
        gate == "palette-fade-done"
            ? !_context.Transitions.IsTransitioning
            : throw Unsupported($"read gate '{gate}'");
    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw Unsupported($"read '{binding}'=${value:x2}");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is not (0x1003 or 0x1006))
            throw Unsupported($"show TX_{textId:x4}");
        _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        throw Unsupported($"set {actor} animation ${animation:x2}");

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation)
    {
        RequireGuard(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        throw Unsupported($"set {actor} collision radii");
    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw Unsupported($"set {actor} A-button sensitivity");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        _guardPrecisePosition += OracleObjectMath.StrictCardinalVector(angle) *
            (speed / 40.0f);
        RequireGuard(actor).Position =
            OracleObjectMath.ToPixelPosition(_guardPrecisePosition);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set {actor} Z");
    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireGuard(actor).Visible = visible;

    void ICutsceneCommandHost.WriteObjectByte(
        string actor, int address, int value)
    {
        if (address != 0x38 || value is not (0 or 1))
            throw Unsupported($"write {actor}.${address:x2}=${value:x2}");
        NpcCharacter guard = RequireGuard(actor);
        if (value == 1)
        {
            guard.SetDialogue(guard.TextId, guard.Message, canFace: false);
        }
        else
        {
            guard.SetDialogue(guard.TextId, guard.Message, canFace: true);
            guard.SetFacingDirection(CardinalDirection(
                guard.Position, _context.Player.Position));
        }
    }

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "CutsceneStage" when value == 0:
                return;
            case "CutsceneTrigger" when value == 0x08:
                // The global cutscene handler runs before updateAllObjects;
                // this interaction write is therefore observed next update.
                _explanationPending = true;
                return;
            case "SimulatedInput" when value == 0:
                _simulatedDirectFrames = 0;
                _simulatedTailPhase = 0;
                _simulatedTailFrames = 0;
                return;
            default:
                throw Unsupported($"write '{binding}'=${value:x2}");
        }
    }

    void ICutsceneCommandHost.PlaySound(int sound) =>
        _context.Sound.PlaySound(sound);
    void ICutsceneCommandHost.SetGlobalFlag(int flag) =>
        throw Unsupported($"set global flag ${flag:x2}");

    void ICutsceneCommandHost.OrRoomFlag(int flag)
    {
        if (flag is not (OracleSaveData.RoomFlag40 or OracleSaveData.RoomFlag80))
            throw Unsupported($"OR room flag ${flag:x2}");
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "StoreLink":
                _storedPackedPosition = _context.Rooms.CurrentRoom.GetPackedPosition(
                    _context.Player.Position);
                _storedDirection = DirectionIndex(_context.Player.FacingVector);
                break;
            case "TurnToFaceLink":
                _guard!.SetFacingDirection(CardinalDirection(
                    _guard.Position, _context.Player.Position));
                break;
            case "MoveLinkAway":
                if (DirectionIndex(CardinalDirection(
                    _guard!.Position, _context.Player.Position)) == 1)
                {
                    _simulatedDirectFrames = unchecked((byte)(
                        0x48 - Mathf.FloorToInt(_context.Player.Position.Y)));
                    if (_simulatedDirectFrames == 0)
                    {
                        _simulatedTailPhase = 1;
                        _simulatedTailFrames = 10;
                    }
                }
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
        string payload) => throw Unsupported(
            $"update native handler '{handler}'");

    void ICutsceneCommandHost.ScriptEnded()
    {
        if (_stage == EventStage.Aftermath)
            _stage = EventStage.Inactive;
        else if (_stage == EventStage.FirstScript && !_explanationPending)
            _stage = EventStage.Inactive;
    }

    private NpcCharacter RequireGuard(string actor)
    {
        if (actor != "Guard" || _guard is null)
            throw Unsupported($"resolve actor '{actor}'");
        return _guard;
    }

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Room 1:86 hardhat script cannot {operation}.");
}
