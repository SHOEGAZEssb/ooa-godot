namespace Oracles;

public sealed partial class ValidationRoot
{
    private void AdvanceStatusBarUpdates(int updates)
    {
        // These isolated pickup/health cases omit the gameplay scheduler.
        // Publish its native frame byte as well as playtime before dispatch.
        for (int update = 0; update < updates; update++)
        {
            MainThreadClock.Advance(_saveData,_runtimeState);
            _statusBar.Update(1.0 / 60.0);
        }
    }
}
