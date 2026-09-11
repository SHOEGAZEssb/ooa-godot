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
        Actor = actor;
        actor.SetScriptButtonSensitive(false);
    }

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

    public void Clear()
    {
        Actor?.SetScriptButtonSensitive(false);
        Actor = null;
        ClearPendingButton();
    }
}
