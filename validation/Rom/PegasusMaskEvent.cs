namespace oracleofages;

// Declared native mask publication through the room-event owner, without a
// cutscene or dialogue replacing the ordinary gameplay object pass.
internal sealed class PegasusMaskEvent : IRoomEvent
{
    internal int Mask { get; set; }
    public bool HasState => false;
    public bool BlocksGameplay => false;
    public bool DisablesLink => (Mask & 1) != 0;
    public bool FreezesNonInteractionObjects => (Mask & 0x80) != 0;
    public void UpdateFrame() { }
    public void Cancel() => Mask = 0;
}
