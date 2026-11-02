
namespace Oracles;

internal interface IOracleMenuLifecycleClient
{
    string MenuName { get; }
    bool CompletesClosingAtWhite => false;
    int ClosingFadeUpdates => OracleMenuLifecycle.FastFadeUpdates;
    void OpenAtWhite();
    void CloseAtWhite();
    void LifecycleClosed();
}
