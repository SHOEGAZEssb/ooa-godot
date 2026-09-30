namespace oracleofages;

// Observes the shared event scheduler without substituting its transition gate.
// This proves dispatch timing, not an individual interaction script's behavior.
internal sealed class ScrollEventObserver : IRoomEvent
{
    public bool HasState { get; private set; } = true;
    public bool BlocksGameplay => false;
    internal int Updates { get; private set; }
    public void UpdateFrame() => Updates++;
    public void Cancel() => HasState = false;
}
