namespace oracleofages;

internal abstract class InteractiveCutsceneCommandHost : CutsceneCommandHost
{
    protected abstract RoomEventContext InputContext { get; }
    protected bool InputLeaseHeld => InputLocked;

    public override void SetInputEnabled(bool enabled)
    {
        if (enabled == !InputLeaseHeld)
            return;
        InputLocked = !enabled;
        if (enabled)
            InputContext.Player.EndCutsceneControl();
        else
            InputContext.Player.BeginCutsceneControl();
    }

    protected void ReleaseInputControl()
    {
        if (InputLeaseHeld)
            SetInputEnabled(enabled: true);
    }
}
