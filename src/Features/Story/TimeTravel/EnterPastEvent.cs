using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot first arrival in past room $1:$39.</summary>
internal sealed class EnterPastEvent :
    RoomCutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost
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
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => HasState;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);
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
        if (_runner.Counter != 0 && speed >= _record.AnimationDoubleSpeed)
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
        _context.Player.EndCutsceneControl(this);
    }

    public override RoomEventContext Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Villager";

    void ICutsceneCommandHost.SetDisabledObjects(int value)
    {
        if (value != 0x11)
        {
            throw new InvalidOperationException(
                $"villagerSubid0dScript requested unsupported wDisabledObjects ${value:x2}.");
        }
        _context.Player.BeginCutsceneControl(owner: this);
        RequireVillager(VillagerActor).SetAnimationRate(0.0f);
    }

    public override void ShowText(int textId, string message) =>
        _context.ShowDialogue(message);

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

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        RequireVillager(actor).Position =
            OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, speed, angle);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        RequireVillager(actor).SetScriptDrawOffset(new Vector2(0, zFixed >> 8));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireVillager(actor).Visible = visible;

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

    private void ResetState()
    {
        _runner.Clear();
        _precisePosition = Vector2.Zero;
    }
}
