namespace oracleofages;

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
    public override bool DialogueOpen => Context.DialogueOpen;
    // bank0.s:interactionRunScript checks wLinkDeathTrigger before either counter.
    public override bool ScriptExecutionBlocked => DialogueOpen || Context.Player.IsDying;
    public override bool IsLinkedGame => Context.Rooms.SaveData.IsLinkedGame;
    public override int FrameCounter => Context.Entities.FrameCounter;
    public override ICutsceneCommandTraceSink? TraceSink => Context.CommandTraceSink;

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled)
            EventResources.UnlockInput();
        else
            EventResources.LockInput();
    }

    public override void PlaySound(int sound) => Context.Sound.PlaySound(sound);
    public override void SetGlobalFlag(int flag) => Context.Rooms.SaveData.SetGlobalFlag(flag);
}
