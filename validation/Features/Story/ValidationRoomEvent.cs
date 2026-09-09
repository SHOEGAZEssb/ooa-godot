namespace oracleofages;

internal sealed class ValidationRoomEvent(IRoomEvent? child = null) : IRoomEvent
{
    public bool HasState { get; private set; } = true;
    public bool BlocksGameplay => HasState;
    public bool OwnsUpdatesOf(IRoomEvent other) => ReferenceEquals(child, other);
    public void UpdateFrame() { }
    public void Cancel() => HasState = false;
}
