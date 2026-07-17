using Godot;
using System;

namespace oracleofages;

/// <summary>Runs Ralph's one-shot portal departure in room $0:$39.</summary>
internal sealed class RalphPortalEvent : IRoomEvent, ICutsceneCommandHost
{
    private readonly RoomEventContext _context;
    private readonly RalphPortalEventDatabase _database = new();
    private readonly RalphPortalEventDatabase.RalphPortalEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _ralph;
    private bool _waitingForScroll;
    private bool _flickering;

    public RalphPortalEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
        if (_record.GlobalFlag != OracleSaveData.GlobalFlagRalphEnteredPortal)
        {
            throw new InvalidOperationException(
                $"Ralph portal event uses global flag ${_record.GlobalFlag:x2}, expected $40.");
        }
    }

    public bool HasState => _waitingForScroll || _runner.Active;
    public bool BlocksGameplay => HasState;
    internal bool WaitingForScroll => _waitingForScroll;
    internal bool Flickering => _flickering;
    internal int Counter => _runner.Counter;
    internal bool Completed => _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start()
    {
        _runner.Clear();
        _waitingForScroll = false;
        _flickering = false;
        _ralph = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_RALPH");

        // @initSubid0d deletes Ralph unless wScreenTransitionDirection is
        // DIR_RIGHT ($01). It also deletes him after the one-shot flag is set.
        Vector2I requiredDirection = DirectionFromOriginalValue(_record.EntryDirection);
        if (Completed || !_context.Transitions.ScrollActive ||
            _context.Transitions.ScrollDirection != requiredDirection)
        {
            _ralph.SetActive(false);
            return;
        }

        _waitingForScroll = true;
    }

    public void UpdateFrame()
    {
        if (_waitingForScroll)
        {
            // Destination objects do not update during a screen scroll. On
            // the first object update afterward, the script disables input
            // and installs its 40-frame counter.
            _waitingForScroll = false;
            _runner.Start(_database.Commands);
            _runner.AdvanceFrame();
            return;
        }

        _runner.AdvanceFrame();
    }

    public void Cancel()
    {
        _ralph = null;
        _runner.Clear();
        _waitingForScroll = false;
        _flickering = false;
    }

    bool ICutsceneCommandHost.DialogueOpen => _context.DialogueOpen;
    bool ICutsceneCommandHost.IsLinkedGame =>
        _context.Rooms.SaveData.IsLinkedGame;
    int ICutsceneCommandHost.FrameCounter => _context.Entities.FrameCounter;
    ICutsceneCommandTraceSink? ICutsceneCommandHost.TraceSink =>
        _context.CommandTraceSink;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Ralph";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
            _context.Player.EndCutsceneControl();
        else
            _context.Player.BeginCutsceneControl();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"Ralph's command stream does not support setting menu enabled={enabled}.");

    void ICutsceneCommandHost.SetDisabledObjects(int value)
    {
        throw new InvalidOperationException(
            $"Ralph's command stream does not support setdisabledobjects ${value:x2}.");
    }

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"Ralph's command stream does not support gate '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"Ralph's command stream cannot read '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId != _record.TextId)
        {
            throw new InvalidOperationException(
                $"Ralph command stream requested TX_{textId:x4}, expected TX_{_record.TextId:x4}.");
        }
        _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        RequireRalph(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        RequireRalph(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireRalph(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw new InvalidOperationException(
            $"Ralph's command actor '{actor}' cannot become A-button sensitive.");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        if (speed != 0x28)
        {
            throw new InvalidOperationException(
                $"Unsupported Ralph object speed ${speed:x2}; expected SPEED_100 ($28).");
        }
        RequireRalph(actor).Position += OracleObjectMath.StrictCardinalVector(angle);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        RequireRalph(actor).SetScriptDrawOffset(new Vector2(0, zFixed >> 8));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible)
    {
        _flickering = true;
        RequireRalph(actor).Visible = visible;
    }

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw new InvalidOperationException(
            $"Ralph's command stream cannot write '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.PlaySound(int sound) =>
        _context.Sound.PlaySound(sound);

    void ICutsceneCommandHost.SetGlobalFlag(int flag) =>
        _context.Rooms.SaveData.SetGlobalFlag(flag);

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"Ralph's command stream cannot OR room flag ${flag:x2}.");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        if (handler != "ralph_restoreMusic")
            throw new InvalidOperationException($"Unknown Ralph native script handler '{handler}'.");
        // scriptHelp.ralph_restoreMusic writes MUS_OVERWORLD to both active
        // music slots and restarts it before enableinput and scriptend.
        _context.Sound.PlaySound(OracleSoundEngine.MusOverworld);
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        _ralph!.SetActive(false);
        _waitingForScroll = false;
        _flickering = false;
    }

    private NpcCharacter RequireRalph(string actor)
    {
        if (actor != "Ralph" || _ralph is null)
            throw new InvalidOperationException($"Unknown Ralph command actor '{actor}'.");
        return _ralph;
    }

    private static Vector2I DirectionFromOriginalValue(int direction) => direction switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported wScreenTransitionDirection value ${direction:x2}.")
    };
}
