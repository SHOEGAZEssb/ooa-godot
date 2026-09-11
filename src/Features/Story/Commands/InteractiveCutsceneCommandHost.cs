namespace oracleofages;

internal abstract class InteractiveCutsceneCommandHost : RoomCutsceneCommandHost
{
    protected bool InputControlHeld => EventResources.InputLocked;

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled)
            EventResources.UnlockInput();
        else
            EventResources.LockInput(onlyIfUnlocked: true);
    }

    protected void ReleaseInputControl() => EventResources.UnlockInput();
}
