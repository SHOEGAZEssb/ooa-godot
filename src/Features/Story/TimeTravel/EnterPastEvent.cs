using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot first arrival in past room $1:$39.</summary>
internal sealed class EnterPastEvent : IRoomEntryEvent, ICutsceneCommandHost
{

    private const string VillagerActor = "Villager";
    private readonly RoomEventContext _context;
    private readonly EnterPastEventDatabase _database = new();
    private readonly EnterPastEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _villager;
    private Vector2 _precisePosition;

    public EnterPastEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
        if (_record.GlobalFlag != OracleSaveData.GlobalFlagEnterPastCutsceneDone)
        {
            throw new InvalidOperationException(
                $"Enter-past event uses global flag ${_record.GlobalFlag:x2}, expected $41.");
        }
        if (_record.JumpSound != OracleSoundEngine.SndJump)
        {
            throw new InvalidOperationException(
                $"Enter-past event uses jump sound ${_record.JumpSound:x2}, expected $53.");
        }
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => HasState;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);
    internal EnterPastEventEventStage Stage => ResolveStage();
    internal int Counter => _runner.Counter;
    internal int ZFixed => _runner.ZFixed;
    internal int CurrentCommandIndex => _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int CurrentCommandUpdates => _runner.CurrentCommandUpdates;
    internal EnterPastEventRecord Record => _record;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start()
    {
        ResetState();
        _villager = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MALE_VILLAGER");

        // The leading jumpifglobalflagset redirects to stubScript, whose
        // scriptend makes @runSubid0d delete the interaction on re-entry.
        if (Completed)
        {
            _villager.SetActive(false);
            return;
        }

        _precisePosition = _villager.Position;
        _runner.Start(_database.Commands);
    }

    void IRoomEntryEvent.Start(OracleRoomData _) => Start();

    public void UpdateFrame()
    {
        if (_villager is null || !_runner.Active)
            return;

        _runner.AdvanceFrame();
        if (!_runner.Active)
            return;

        // @runSubid0d calls interactionAnimateBasedOnSpeed after its script.
        // It always advances once, plus once more at SPEED_100 while
        // counter2 is nonzero.
        int animationUpdates = 1;
        int speed = _runner.ActorSpeed(VillagerActor);
        if (_runner.Counter != 0 && speed >= _record.FastSpeed)
            animationUpdates++;
        _villager.AdvanceAnimationUpdates(animationUpdates);
        _villager.PreventPlayerPassing(_context.Player);
        _villager.UpdateDrawPriority(_context.Player.Position);
    }

    public void Cancel()
    {
        if (_villager is not null)
        {
            _villager.SetScriptDrawOffset(Vector2.Zero);
            _villager.SetAnimationRate(1.0f);
        }
        _villager = null;
        ResetState();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Villager";

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"villagerSubid0dScript does not support setting menu enabled={enabled}.");

    void ICutsceneCommandHost.SetDisabledObjects(int value)
    {
        if (value != 0x11)
        {
            throw new InvalidOperationException(
                $"villagerSubid0dScript requested unsupported wDisabledObjects ${value:x2}.");
        }
        _context.Player.BeginCutsceneControl();
        RequireVillager(VillagerActor).SetAnimationRate(0.0f);
    }

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"villagerSubid0dScript does not support gate '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"villagerSubid0dScript cannot read '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId != _record.TextId)
        {
            throw new InvalidOperationException(
                $"Enter-past command stream requested TX_{textId:x4}, " +
                $"expected TX_{_record.TextId:x4}.");
        }
        _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        RequireVillager(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        RequireVillager(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireVillager(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw new InvalidOperationException(
            $"Enter-past command actor '{actor}' cannot become A-button sensitive.");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
        _precisePosition += direction * (speed / 40.0f);
        RequireVillager(actor).Position =
            OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        RequireVillager(actor).SetScriptDrawOffset(new Vector2(0, zFixed >> 8));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireVillager(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw new InvalidOperationException(
            $"villagerSubid0dScript cannot write '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"villagerSubid0dScript cannot OR room flag ${flag:x2}.");

    void ICutsceneCommandHost.RunNativeHandler(string handler) =>
        throw new InvalidOperationException(
            $"Unknown first-past-arrival native script handler '{handler}'.");

    void ICutsceneCommandHost.ScriptEnded()
    {
        _villager!.SetScriptDrawOffset(Vector2.Zero);
        _villager.SetActive(false);
    }

    private NpcCharacter RequireVillager(string actor)
    {
        if (actor != VillagerActor || _villager is null)
            throw new InvalidOperationException($"Unknown enter-past command actor '{actor}'.");
        return _villager;
    }

    private EnterPastEventEventStage ResolveStage()
    {
        if (!_runner.Active)
            return EnterPastEventEventStage.Inactive;
        if (_context.DialogueOpen)
            return EnterPastEventEventStage.Dialogue;

        int updates = _runner.CurrentCommandUpdates;
        return _runner.CurrentCommand?.Source.CommandIndex switch
        {
            0 => EnterPastEventEventStage.Begin,
            1 => updates == 0 ? EnterPastEventEventStage.InstallIntroWait : EnterPastEventEventStage.IntroWait,
            2 or 3 => EnterPastEventEventStage.PreJumpWait,
            4 => updates <= 1 ? EnterPastEventEventStage.BeginJump : EnterPastEventEventStage.Jump,
            5 => updates == 0 ? EnterPastEventEventStage.InstallPostJumpWait : EnterPastEventEventStage.PostJumpWait,
            6 => EnterPastEventEventStage.Dialogue,
            7 => EnterPastEventEventStage.PostTextWait,
            8 => EnterPastEventEventStage.StartFirstDown,
            9 => updates == 0 ? EnterPastEventEventStage.StartFirstDown : EnterPastEventEventStage.FirstDown,
            10 => updates == 0 ? EnterPastEventEventStage.FirstDown : EnterPastEventEventStage.Right,
            11 => updates == 0 ? EnterPastEventEventStage.Right : EnterPastEventEventStage.SecondDown,
            12 or 13 => updates == 0 ? EnterPastEventEventStage.StartSlowDown : EnterPastEventEventStage.SlowDown,
            14 or 15 => updates == 0 ? EnterPastEventEventStage.StartFinalDown : EnterPastEventEventStage.FinalDown,
            16 or 17 or 18 => EnterPastEventEventStage.FinalDown,
            _ => EnterPastEventEventStage.Inactive
        };
    }

    private void ResetState()
    {
        _runner.Clear();
        _precisePosition = Vector2.Zero;
    }
}
