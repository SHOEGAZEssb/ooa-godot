namespace oracleofages;

/// <summary>
/// Explicit counterpart to the original interaction always-update bit for
/// presentation objects that must continue fixed updates during room scrolling.
/// </summary>
internal interface IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    void UpdateDuringScreenTransition();
}
