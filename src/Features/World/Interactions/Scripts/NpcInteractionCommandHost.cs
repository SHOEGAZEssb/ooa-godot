using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common lifecycle for an entity-backed infinite interactionRunScript lane.
/// The owning host supplies only its source-specific dialogue, native helpers,
/// and reward handoffs.
/// </summary>
internal abstract class NpcInteractionCommandHost : CutsceneCommandHost
{
    private readonly ScriptActorBinding<NpcCharacter> _binding;
    private readonly CutsceneCommandRunner _runner;
    private NpcInteractionTarget? _interactionTarget;
    private Player? _player;
    private bool InputControlHeld => _player?.IsCutsceneControlOwner(this) == true;
    private ICutsceneCommandTraceSink? _traceSink;

    protected NpcInteractionCommandHost(
        string actorName,
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands)
    {
        _binding = new(actorName);
        Rooms = rooms;
        Entities = entities;
        Dialogue = dialogue;
        Commands = commands;
        _runner = new CutsceneCommandRunner(this);
    }

    protected RoomSession Rooms { get; }
    protected RoomEntityManager Entities { get; }
    protected DialogueBox Dialogue { get; }
    protected IReadOnlyList<CutsceneCommand> Commands { get; }
    protected NpcCharacter ScriptActor => _binding.Actor ??
        throw new InvalidOperationException($"{GetType().Name} has no active actor.");
    protected Player ScriptPlayer => _player ??
        throw new InvalidOperationException(
            $"{GetType().Name} has no active Link binding.");

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputControlHeld;
    public override bool DialogueOpen => Dialogue.IsOpen;
    public override bool ScriptExecutionBlocked => DialogueOpen || _player?.IsDying == true;
    public override bool IsLinkedGame => Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => Entities.FrameCounter;
    public override ICutsceneCommandTraceSink? TraceSink => _traceSink;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int CurrentCommandUpdates => _runner.CurrentCommandUpdates;
    internal int Counter => _runner.Counter;
    internal bool InputDisabled => InputControlHeld;
    internal void SetTraceSink(ICutsceneCommandTraceSink? traceSink) =>
        _traceSink = traceSink;

    public bool TryInteract(
        NpcInteractionTarget target,
        Player player)
    {
        NpcCharacter npc = target.Npc;
        if (!MatchesAndPrepare(npc))
            return false;

        if (_runner.Active && !ReferenceEquals(_binding.Actor, npc))
            Cancel();
        if (!_runner.Active)
            Start(target);
        if (InputControlHeld || !_binding.QueueButton(npc))
        {
            return false;
        }

        _player = player;
        // Link's A-button probe and interactionRunScript belong to the same
        // original update. Consume the queued press at that boundary.
        _runner.AdvanceFrame();
        return true;
    }

    public void AdvanceFrame()
    {
        if (!_runner.Active || Dialogue.IsOpen)
            return;

        BeforeAdvanceFrame();
        _runner.AdvanceFrame();
    }

    public void Cancel()
    {
        ResetHostState();
        if (InputControlHeld && _player is not null)
            _player.EndCutsceneControl(this);
        _interactionTarget?.Cancel();
        _binding.Clear();
        _interactionTarget = null;
        _player = null;
        _runner.Clear();
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        _binding.Matches(actor);

    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        RequireActor(actor.Value);
        return _binding.ConsumeButton();
    }

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireActor(actor).InitializeCollisionRadii();

    public override void SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireActor(actor).SetCollisionRadii(radiusY, radiusX);

    public override void SetActorButtonSensitive(string actor)
    {
        _ = RequireActor(actor);
        _binding.EnableButton();
    }

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled)
        {
            if (InputControlHeld)
            {
                ScriptPlayer.EndCutsceneControl(this);
            }
            EndTalkLifecycle();
            return;
        }

        if (InputControlHeld)
            return;
        ScriptPlayer.BeginCutsceneControl(owner: this);
    }

    public override bool TextOptionEquals(int value)
    {
        if (!Dialogue.TryTakeChoiceResult(out int choice))
        {
            throw new InvalidOperationException(
                $"{GetType().Name} has no completed text choice to compare " +
                $"with ${value:x2}.");
        }
        OnTextOptionConsumed(choice);
        return choice == value;
    }

    protected abstract bool MatchesAndPrepare(NpcCharacter npc);

    protected virtual void BeforeAdvanceFrame()
    {
    }

    protected virtual void OnTextOptionConsumed(int choice)
    {
    }

    protected virtual void ResetHostState()
    {
    }

    protected NpcCharacter RequireActor(string actor) => _binding.Require(actor);

    protected void ShowDialogue(string message, bool choice, int initialChoice = 0)
    {
        float linkY = Entities.WorldToScreen(ScriptPlayer.Position).Y;
        if (choice)
        {
            Dialogue.ShowGameplayChoiceMessage(
                message,
                linkY,
                initialChoice,
                ScriptActor.TextPosition);
        }
        else
        {
            Dialogue.ShowGameplayMessage(
                message,
                linkY,
                ScriptActor.TextPosition);
        }
    }

    private void Start(NpcInteractionTarget target)
    {
        NpcCharacter npc = target.Npc;
        _binding.Bind(npc);
        _interactionTarget = target;
        _player = null;
        target.Begin();
        _runner.Start(Commands);
        // Run initialization through the first checkabutton exactly once.
        _runner.AdvanceFrame();
    }

    private void EndTalkLifecycle()
    {
        _interactionTarget?.End();
    }
}
