using System;

namespace oracleofages;

/// <summary>
/// One interaction actor and its pending A-button byte. The host decides when
/// to consume that byte and owns talk lifecycle, input control, and updates.
/// </summary>
internal sealed class ScriptActorBinding<TActor>(string name) where TActor : NpcCharacter
{
    public TActor? Actor { get; private set; }
    public bool ButtonSensitive => Actor?.ScriptButtonSensitive == true;
    public bool ButtonPending { get; private set; }
    public bool Matches(CutsceneActorId actor) => actor.Value == name;

    public void Bind(TActor actor)
    {
        Clear();
        if (actor.IsRetired)
            throw new InvalidOperationException($"Cannot bind retired actor '{name}' (${actor.Record.Id:x2}:${actor.Record.SubId:x2}).");
        Actor = actor;
        actor.Retired += OnActorRetired;
        actor.SetScriptButtonSensitive(false);
    }

    private void OnActorRetired(NpcCharacter actor) => Clear();

    public TActor Require(string actor) => actor == name && Actor is not null
        ? Actor
        : throw new InvalidOperationException($"No active '{name}' binding for command actor '{actor}'.");

    public void EnableButton() => Require(name).SetScriptButtonSensitive(true);

    public bool QueueButton(NpcCharacter actor)
    {
        if (!ButtonSensitive || !ReferenceEquals(actor, Actor))
            return false;
        ButtonPending = true;
        return true;
    }

    public bool ConsumeButton()
    {
        bool pressed = ButtonPending;
        ClearPendingButton();
        return pressed;
    }

    public void ClearPendingButton() => ButtonPending = false;
    // Some native handlers overwrite pressedAButton before dispatching the
    // script, including before the script installs A-list sensitivity.
    public void SetNativeButton(bool pressed) => ButtonPending = pressed;

    public void Clear()
    {
        if (Actor is { } actor)
        {
            actor.Retired -= OnActorRetired;
            actor.SetScriptButtonSensitive(false);
        }
        Actor = null;
        ClearPendingButton();
    }
}
