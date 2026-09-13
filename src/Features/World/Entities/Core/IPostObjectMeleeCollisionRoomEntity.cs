namespace oracleofages;

/// <summary>
/// Melee requests are tested after object movement, at cutscene01's collision
/// pass. The weapon parent consumes the resulting contact on its next update.
/// </summary>
internal interface IPostObjectMeleeCollisionRoomEntity
{
    // A no-op collision still ends this enemy's scan, without setting the
    // weapon's var2a contact signal.
    bool MeleeReportsContact { get; }
}
