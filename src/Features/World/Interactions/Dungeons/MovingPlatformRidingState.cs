namespace oracleofages;

/// <summary>Single owner selected by the ordered top-down platform interaction pass.</summary>
internal sealed class MovingPlatformRidingState
{
    private MovingPlatformRoomEntity? _owner;
    private bool _instrument;
    internal bool HasRiderOrInstrument => _owner is not null || _instrument;

    internal void BeginUpdate(int instrument)
    {
        _owner = null;
        _instrument = instrument != 0;
    }

    internal bool IsOwner(MovingPlatformRoomEntity platform) => _owner == platform;

    internal void CheckContact(MovingPlatformRoomEntity platform, bool touching)
    {
        if (_owner == platform && !touching) _owner = null;
        else if (_owner is null && !_instrument && touching) _owner = platform;
    }
}
