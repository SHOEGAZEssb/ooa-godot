using System;
using System.Collections.Generic;

namespace oracleofages;

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
    protected int ScriptActorSpeed => _runner.ActorSpeed(new(_actorName));
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

    /// <summary>
    /// Registers the actor when native state-0 code installs A-button
    /// sensitivity before entering the imported script stream.
    /// </summary>
    protected void SetInitialActorButtonSensitive()
    {
        TActor actor = _actor ?? throw new InvalidOperationException(
            $"{_actorName} has no actor for native A-button registration.");
        actor.SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

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
