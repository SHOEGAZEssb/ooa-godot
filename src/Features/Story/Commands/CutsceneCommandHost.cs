using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class CutsceneCommandHost : ICutsceneCommandHost
{
    private CutsceneCommandSource? _activeSource;

    RoomEventContext ICutsceneCommandHost.Context =>
        throw UnsupportedCommand("provide a room-event context");
    public virtual bool DialogueOpen =>
        ((ICutsceneCommandHost)this).Context.DialogueOpen;
    public virtual bool IsLinkedGame =>
        ((ICutsceneCommandHost)this).Context.Rooms.SaveData.IsLinkedGame;
    public virtual int FrameCounter =>
        ((ICutsceneCommandHost)this).Context.Entities.FrameCounter;
    public virtual ICutsceneCommandTraceSink? TraceSink =>
        ((ICutsceneCommandHost)this).Context.CommandTraceSink;

    public void SetActiveCommandSource(CutsceneCommandSource? source) =>
        _activeSource = source;

    public virtual bool HasActorBinding(CutsceneActorId actor) => false;
    public virtual void SetInputEnabled(bool enabled)
    {
        Player player = ((ICutsceneCommandHost)this).Context.Player;
        if (enabled)
            player.EndCutsceneControl();
        else
            player.BeginCutsceneControl();
    }
    public virtual void SetMenuEnabled(bool enabled) =>
        throw UnsupportedCommand($"set menu enabled={enabled}");
    public virtual void SetDisabledObjects(int value) =>
        throw UnsupportedCommand($"set disabled objects ${value:x2}");
    public virtual bool GateOpen(string gate) =>
        throw UnsupportedCommand($"read gate '{gate}'");
    public virtual bool MemoryEquals(string binding, int value) =>
        throw UnsupportedCommand($"compare '{binding}' with ${value:x2}");
    public virtual int ReadMemory(string binding) =>
        throw UnsupportedCommand($"read '{binding}'");
    public virtual bool RoomFlagSet(int flag) =>
        throw UnsupportedCommand($"read room flag ${flag:x2}");
    public virtual bool TradeItemEquals(int value) =>
        throw UnsupportedCommand($"compare trade item ${value:x2}");
    public virtual bool TextOptionEquals(int value) =>
        throw UnsupportedCommand($"read text option ${value:x2}");
    public virtual bool TryConsumeActorButton(CutsceneActorId actor) =>
        throw UnsupportedCommand($"consume A for actor '{actor}'");
    public virtual void ShowText(int textId, string message) =>
        throw UnsupportedCommand($"show text ${textId:x4}");
    public virtual void ShowText(
        int textId,
        string message,
        int? textboxPosition) =>
        ((ICutsceneCommandHost)this).ShowText(textId, message);
    public virtual void ShowLoadedText() =>
        throw UnsupportedCommand("show the loaded text");
    public virtual void SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        throw UnsupportedCommand($"set actor '{actor}' animation ${animation:x2}");
    public virtual void SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation) =>
        throw UnsupportedCommand($"set actor '{actor}' movement animation ${angle:x2}");
    public virtual void SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        throw UnsupportedCommand($"set actor '{actor}' collision radii");
    public virtual void SetActorButtonSensitive(string actor) =>
        throw UnsupportedCommand($"set actor '{actor}' A-button sensitivity");
    public virtual void MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw UnsupportedCommand($"move actor '{actor}'");
    public virtual void SetActorZ(string actor, int zFixed) =>
        throw UnsupportedCommand($"set actor '{actor}' Z");
    public virtual void SetActorVisible(string actor, bool visible) =>
        throw UnsupportedCommand($"set actor '{actor}' visible={visible}");
    public virtual void WriteObjectByte(string actor, int address, int value) =>
        throw UnsupportedCommand($"write actor '{actor}'.${address:x2}=${value:x2}");
    public virtual Vector2 GetActorPosition(CutsceneActorId actor) =>
        throw UnsupportedCommand($"read actor '{actor}' position");
    public virtual void SetActorPosition(
        CutsceneActorId actor,
        Vector2 position,
        Vector2 facingDelta,
        Vector2 movement) =>
        throw UnsupportedCommand($"set actor '{actor}' position");
    public virtual void CompleteActorTranslation(CutsceneActorId actor) =>
        throw UnsupportedCommand($"complete actor '{actor}' translation");
    public virtual void DeleteActor(CutsceneActorId actor) =>
        throw UnsupportedCommand($"delete actor '{actor}'");
    public virtual void WriteMemory(string binding, int value) =>
        throw UnsupportedCommand($"write '{binding}'=${value:x2}");
    public virtual void GiveItem(int treasureId, int parameter) =>
        throw UnsupportedCommand($"give treasure ${treasureId:x2}:${parameter:x2}");
    public virtual void PlaySound(int sound) =>
        ((ICutsceneCommandHost)this).Context.Sound.PlaySound(sound);
    public virtual void SetMusic(int music) =>
        throw UnsupportedCommand($"set music ${music:x2}");
    public virtual void SetGlobalFlag(int flag) =>
        ((ICutsceneCommandHost)this).Context.Rooms.SaveData.SetGlobalFlag(flag);
    public virtual void OrRoomFlag(int flag) =>
        throw UnsupportedCommand($"OR room flag ${flag:x2}");
    public virtual void RunNativeHandler(string handler) =>
        throw UnsupportedCommand($"run native handler '{handler}'");
    public virtual bool UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload) =>
        throw UnsupportedCommand($"update native handler '{handler}'");
    public virtual void ScriptEnded() => throw UnsupportedCommand("end the script");

    protected InvalidOperationException UnsupportedCommand(string operation)
    {
        CutsceneCommandSchemaEntry? schema = _activeSource is { } source
            ? CutsceneCommandSchema.FindOpcode(source.Opcode)
            : null;
        string capabilities = schema is null
            ? string.Empty
            : $" requiring [{string.Join(", ", schema.Capabilities)}]";
        return new InvalidOperationException(
            $"{GetType().Name} cannot {operation}{capabilities} at " +
            (_activeSource?.ToString() ?? "an unknown cutscene command"));
    }
}

internal abstract class InteractiveCutsceneCommandHost : CutsceneCommandHost
{
    protected abstract RoomEventContext InputContext { get; }
    protected bool InputLeaseHeld { get; private set; }

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled == !InputLeaseHeld)
            return;
        InputLeaseHeld = !enabled;
        if (enabled)
            InputContext.Player.EndCutsceneControl();
        else
            InputContext.Player.BeginCutsceneControl();
    }

    protected void ReleaseInputControl()
    {
        if (InputLeaseHeld)
            SetInputEnabled(enabled: true);
    }
}

/// <summary>
/// Common lifecycle for a single-actor interactionRunScript that returns to
/// checkabutton forever. The concrete event retains every script-specific
/// predicate, dialogue, reward, animation validation, and native operation.
/// </summary>
internal abstract class InteractiveInfiniteScriptHost<TActor> :
    InteractiveCutsceneCommandHost, IRoomEvent, ICutsceneCommandHost
    where TActor : NpcCharacter
{
    private readonly RoomEventContext _context;
    private readonly string _actorName;
    private readonly CutsceneCommandRunner _runner;
    private TActor? _actor;
    private bool _buttonSensitive;
    private bool _buttonPressed;

    protected InteractiveInfiniteScriptHost(
        RoomEventContext context,
        string actorName)
    {
        _context = context;
        _actorName = actorName;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputLeaseHeld;
    public RoomEventContext Context => _context;
    protected override RoomEventContext InputContext => _context;
    protected TActor? ScriptActor => _actor;
    protected bool PendingActorButton => _buttonPressed;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _buttonSensitive;

    public abstract void UpdateFrame();

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_runner.Active || !_buttonSensitive || InputLeaseHeld ||
            !ReferenceEquals(npc, _actor))
        {
            return false;
        }

        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        if (_actor is not null)
        {
            _actor.SetScriptButtonSensitive(false);
            _actor.SetAnimationRate(1.0f);
        }

        _actor = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _runner.Clear();
        ResetEventState();
    }

    public sealed override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value == _actorName;

    public sealed override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequireScriptActor(actor.Value);
        if (!_buttonPressed)
            return false;

        _buttonPressed = false;
        return true;
    }

    public sealed override void SetActorButtonSensitive(string actor)
    {
        RequireScriptActor(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    public sealed override void SetActorVisible(string actor, bool visible) =>
        RequireScriptActor(actor).Visible = visible;

    protected void StartInfiniteScript(
        TActor actor,
        IReadOnlyList<CutsceneCommand> commands,
        int initialScriptUpdates = 0)
    {
        _runner.Clear();
        _actor = actor;
        _buttonSensitive = false;
        _buttonPressed = false;
        ReleaseInputControl();
        _runner.Start(commands);

        for (int update = 0; update < initialScriptUpdates; update++)
            _runner.AdvanceFrame();
    }

    protected void AdvanceInfiniteScript() => _runner.AdvanceFrame();

    protected void ClearPendingActorButton() => _buttonPressed = false;

    protected TActor RequireScriptActor(string actor)
    {
        if (actor != _actorName || _actor is null)
        {
            throw new InvalidOperationException(
                $"Unknown {_actorName} command actor '{actor}'.");
        }

        return _actor;
    }

    protected virtual void ResetEventState()
    {
    }
}
