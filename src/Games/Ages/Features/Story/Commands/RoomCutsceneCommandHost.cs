namespace Oracles;

/// <summary>
/// Room services for script hosts. The interpreter contract itself has no
/// dependency on a room context; entity-backed and validation hosts bind
/// only the operations they own.
/// </summary>
internal abstract class RoomCutsceneCommandHost : CutsceneCommandHost
{
    private RoomEventResources? _resources;
    public abstract RoomEventContext Context { get; }
    protected RoomEventResources EventResources => _resources ??= new(Context, this);
    private NativeScriptInputControl ScriptInput => EventResources.ScriptInput;
    protected bool InputControlHeld => ScriptInput.LinkDisabled || EventResources.InputLocked;
    public override bool DialogueOpen => Context.DialogueOpen;
    // bank0.s:interactionRunScript checks wLinkDeathTrigger before either
    // counter and accepts wTextIsActive=$80/$ff while gameplay still observes
    // active text. A retained panel must allow its script to replace it.
    public override bool ScriptExecutionBlocked => Context.ScriptDialogueOpen || Context.Player.IsDying;
    public override bool IsLinkedGame => Context.Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => Context.Entities.FrameCounter;
    public override ICutsceneCommandTraceSink? TraceSink => Context.CommandTraceSink;

    public override void SetInputEnabled(bool enabled) => ScriptInput.SetEnabled(Context.Player, enabled);

    public override void SetMenuEnabled(bool enabled) => ScriptInput.SetMenuEnabled(Context.Player, enabled);
    protected void SetNativeMenuDisabled(byte value) => ScriptInput.SetMenuDisabled(value);

    public override void SetDisabledObjects(int value)
    {
        if ((uint)value > 0xff) throw UnsupportedCommand($"disabled-object mask ${value:x2}");
        ScriptInput.SetDisabledObjects((byte)value);
    }

    protected void ReleaseInputControl()
    {
        EventResources.UnlockInput();
    }

    protected void RelinquishInputControl() => ScriptInput.Relinquish();

    public override void PlaySound(int sound) => Context.Sound.PlaySound(sound);
    public override void SetGlobalFlag(int flag) => Context.Rooms.SaveData.SetGlobalFlag(flag);
}
