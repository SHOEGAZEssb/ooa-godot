namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void AdvanceStatusBarUpdates(int updates)
    {
        // These isolated pickup/health cases omit the gameplay scheduler.
        // Supply its live playtime advance before each status-bar dispatch.
        for (int update = 0; update < updates; update++)
        {
            _saveData.AdvancePlaytime();
            _statusBar.Update(1.0 / 60.0);
        }
    }
}
