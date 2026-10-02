namespace oracleofages;

/// <summary>Shared rider publication consumed by Link before the next interaction pass.</summary>
internal sealed class MovingPlatformRidingState
{
    private IPlayerRideableRoomEntity? _owner;
    private bool _instrument;
    internal bool HasRiderOrInstrument => _owner is not null || _instrument;

    internal void BeginUpdate(int instrument)
    {
        _owner = null;
        _instrument = instrument != 0;
    }

    internal bool IsOwner(MovingPlatformRoomEntity platform) => _owner == platform;

    // sidescrollPlatform_checkLinkOnPlatform always publishes a successful
    // claim; its local var34 can survive a paused interaction pass even when
    // updateSpecialObjects has cleared the shared wLinkRidingObject signal.
    internal void PublishSideScroll(IPlayerRideableRoomEntity platform, bool riding)
    {
        if (riding) _owner = platform;
    }

    internal void CheckContact(MovingPlatformRoomEntity platform, bool touching)
    {
        if (_owner == platform && !touching) _owner = null;
        else if (_owner is null && !_instrument && touching) _owner = platform;
    }
}
