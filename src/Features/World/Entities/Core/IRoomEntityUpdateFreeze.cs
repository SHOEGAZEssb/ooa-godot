namespace oracleofages;

/// <summary>
/// A source owner for a non-modal wDisabledObjects-style room-object freeze.
/// The owner continues updating so it can eventually release the freeze.
/// </summary>
internal interface IRoomEntityUpdateFreeze
{
    bool FreezesRoomEntities { get; }
}
