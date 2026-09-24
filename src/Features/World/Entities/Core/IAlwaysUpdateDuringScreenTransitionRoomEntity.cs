namespace oracleofages;

/// <summary>
/// Explicit source eligibility for fixed updates during room scrolling, such
/// as the interaction always-update bit or an enemy still in state zero.
/// Implementations must stop advancing when their source eligibility ends.
/// </summary>
internal interface IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    void UpdateDuringScreenTransition(RoomEntityFrame frame);
}
