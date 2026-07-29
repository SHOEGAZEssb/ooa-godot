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
    private readonly string _actorName;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _actor;
    private NpcInteractionTarget? _interactionTarget;
    private Player? _player;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private bool _inputLeaseHeld;
    private ICutsceneCommandTraceSink? _traceSink;

    protected NpcInteractionCommandHost(
        string actorName,
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands)
    {
        _actorName = actorName;
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
    protected NpcCharacter ScriptActor => _actor ??
        throw new InvalidOperationException(
            $"{GetType().Name} has no active {_actorName} actor.");
    protected Player ScriptPlayer => _player ??
        throw new InvalidOperationException(
            $"{GetType().Name} has no active Link binding.");

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => _inputLeaseHeld;
    public override bool DialogueOpen => Dialogue.IsOpen;
    public override bool IsLinkedGame => Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => Entities.FrameCounter;
    public override ICutsceneCommandTraceSink? TraceSink => _traceSink;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int CurrentCommandUpdates => _runner.CurrentCommandUpdates;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _buttonSensitive;
    internal bool InputDisabled => _inputLeaseHeld;
    internal void SetTraceSink(ICutsceneCommandTraceSink? traceSink) =>
        _traceSink = traceSink;

    public bool TryInteract(
        NpcInteractionTarget target,
        Player player)
    {
        NpcCharacter npc = target.Npc;
        if (!MatchesAndPrepare(npc))
            return false;

        if (_runner.Active && !ReferenceEquals(_actor, npc))
            Cancel();
        if (!_runner.Active)
            Start(target);
        if (!_buttonSensitive || _inputLeaseHeld ||
            !ReferenceEquals(_actor, npc))
        {
            return false;
        }

        _player = player;
        _buttonPressed = true;
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
        if (_inputLeaseHeld && _player is not null)
            _player.EndCutsceneControl();
        _interactionTarget?.Cancel();
        if (_actor is not null)
            _actor.SetScriptButtonSensitive(false);
        _actor = null;
        _interactionTarget = null;
        _player = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputLeaseHeld = false;
        _runner.Clear();
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value == _actorName;

    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        RequireActor(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    public override void SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireActor(actor).SetCollisionRadii(radiusY, radiusX);

    public override void SetActorButtonSensitive(string actor)
    {
        RequireActor(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_inputLeaseHeld)
            {
                ScriptPlayer.EndCutsceneControl();
                _inputLeaseHeld = false;
            }
            EndTalkLifecycle();
            return;
        }

        if (_inputLeaseHeld)
            return;
        ScriptPlayer.BeginCutsceneControl();
        _inputLeaseHeld = true;
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

    protected NpcCharacter RequireActor(string actor)
    {
        if (actor != _actorName)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} cannot bind command actor '{actor}'; " +
                $"expected '{_actorName}'.");
        }
        return ScriptActor;
    }

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
        _actor = npc;
        _interactionTarget = target;
        _player = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputLeaseHeld = false;
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
