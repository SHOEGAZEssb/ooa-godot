using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

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
    private readonly NativeScriptInputControl _input;
    private bool InputControlHeld => _input.LinkDisabled;
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
        _input = new(entities.RuntimeState);
        Dialogue = dialogue;
        Commands = commands;
        _runner = new CutsceneCommandRunner(this);
    }

    protected RoomSession Rooms { get; }
    protected string AssetRoot => Rooms.AssetRoot;
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
    public override bool ScriptExecutionBlocked =>
        Dialogue.IsOpen && !Dialogue.AllowsNativeScriptCommands || _player?.IsDying == true;
    public override bool IsLinkedGame => Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => Entities.FrameCounter;
    public override ICutsceneCommandTraceSink? TraceSink => _traceSink;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    internal int NativeCounter1 => _runner.NativeCounter1;
    internal bool InputDisabled => InputControlHeld;
    protected int ActorMovementCounter(CutsceneActorId actor) => _runner.MovementCounter(actor);
    protected bool ObjectButtonPending => _binding.ButtonPending;
    protected void ClearObjectButton() => _binding.ClearPendingButton();
    protected void SetNativeObjectButton(bool pressed) => _binding.SetNativeButton(pressed);
    protected void SetNativeActorSpeed(CutsceneActorId actor,int speed)=>_runner.SetNativeActorSpeed(actor,speed);
    protected void ReplaceObjectScript(IReadOnlyList<CutsceneCommand> commands) => _runner.Start(commands);
    protected void RelinquishInputControl() => _input.Relinquish();
    internal void SetTraceSink(ICutsceneCommandTraceSink? traceSink) =>
        _traceSink = traceSink;

    public virtual bool TryInteract(
        NpcInteractionTarget target,
        Player player)
    {
        NpcCharacter npc = target.Npc;
        if (!MatchesAndPrepare(npc))
            return false;

        if (_runner.Active && !ReferenceEquals(_binding.Actor, npc))
            Cancel();
        if (!_runner.Active)
            Start(target, player);
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
        if (!_runner.Active || ScriptExecutionBlocked)
            return;

        BeforeAdvanceFrame();
        _runner.AdvanceFrame();
    }

    public void Cancel()
    {
        ResetHostState();
        _input.Cancel();
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
        bool pressed = _binding.ConsumeButton();
        // scripting.s:scriptCmd_checkAButton calls func_0c_4177 when the
        // retained byte is consumed, after Link's earlier selection grace.
        if (pressed) ScriptPlayer.ApplyInteractionInvincibility(0x80);
        return pressed;
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
        _input.SetEnabled(ScriptPlayer, enabled);
        if (enabled) EndTalkLifecycle();
    }

    public override void SetDisabledObjects(int value)
    {
        if ((uint)value > 0xff) throw UnsupportedCommand($"disabled-object mask ${value:x2}");
        _input.SetDisabledObjects((byte)value);
    }

    public override void SetMenuEnabled(bool enabled) => _input.SetMenuEnabled(ScriptPlayer, enabled);

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
    public override void SetActorTextId(string actor, int textId, string message)
    {
        NpcCharacter npc = RequireActor(actor);
        npc.SetDialogue(textId, message, npc.Record.CanFace);
    }

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

    // Entity-owned lanes bind at room loading and advance only in their object
    // slot. The other conversation hosts retain their immediate talk boundary.
    protected void BindObject(NpcCharacter npc)
    {
        Cancel();
        _binding.Bind(npc);
        _runner.Start(Commands);
    }

    protected bool QueueObjectInteraction(NpcInteractionTarget target, Player player)
    {
        if (!_runner.Active || InputControlHeld || !_binding.QueueButton(target.Npc))
            return false;
        _interactionTarget = target;
        _player = player;
        target.Begin();
        return true;
    }

    protected void AdvanceObject(Player player)
    {
        if (!_runner.Active) return;
        _player = player;
        if (ScriptExecutionBlocked) return;
        AdvanceFrame();
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

    private void Start(NpcInteractionTarget target, Player player)
    {
        NpcCharacter npc = target.Npc;
        _binding.Bind(npc);
        _interactionTarget = target;
        _player = player;
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
