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
    private readonly ScriptActorBinding<TActor> _binding;
    private readonly CutsceneCommandRunner _runner;
    private readonly CutsceneActorId _actorId;

    protected InteractiveInfiniteScriptHost(
        RoomEventContext context,
        string actorName)
    {
        _context = context;
        _binding = new(actorName);
        _actorId = new(actorName);
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputControlHeld;
    public override RoomEventContext Context => _context;
    protected TActor? ScriptActor => _binding.Actor;
    protected bool PendingActorButton => _binding.ButtonPending;
    protected int ScriptActorSpeed => _runner.ActorSpeed(_actorId);
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _binding.ButtonSensitive;

    public abstract void UpdateFrame();

    public bool TryInteractNpc(NpcCharacter npc) =>
        _runner.Active && !InputControlHeld && _binding.QueueButton(npc);

    public void Cancel()
    {
        ReleaseInputControl();
        if (_binding.Actor is { } actor)
        {
            ReleaseScriptActor(actor);
            actor.SetAnimationRate(1.0f);
        }

        _binding.Clear();
        _runner.Clear();
        ResetEventState();
    }

    public sealed override bool HasActorBinding(CutsceneActorId actor) =>
        _binding.Matches(actor);

    public sealed override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequireScriptActor(actor.Value);
        return _binding.ConsumeButton();
    }

    public sealed override void SetActorButtonSensitive(string actor)
    {
        _ = RequireScriptActor(actor);
        _binding.EnableButton();
    }

    public sealed override void SetActorVisible(string actor, bool visible) =>
        RequireScriptActor(actor).Visible = visible;

    protected void StartInfiniteScript(
        TActor actor,
        IReadOnlyList<CutsceneCommand> commands,
        int initialScriptUpdates = 0)
    {
        _runner.Clear();
        _binding.Bind(actor);
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
    protected void SetInitialActorButtonSensitive() => _binding.EnableButton();

    protected void ClearPendingActorButton() => _binding.ClearPendingButton();

    protected TActor RequireScriptActor(string actor) => _binding.Require(actor);

    protected virtual void ResetEventState()
    {
    }

    protected virtual void ReleaseScriptActor(TActor actor)
    {
    }
}
