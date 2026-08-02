using Godot;

namespace oracleofages;

/// <summary>
/// A grabbable interaction which remains attached to the Bracelet parent while
/// the assigned item button is held, rather than becoming a carried object.
/// </summary>
internal interface IBraceletPullInteractableRoomEntity
{
    bool TryBeginBraceletPull(Player player);
    bool UpdateBraceletPull(
        Player player,
        Vector2 movementInput,
        bool assignedButtonHeld);
    void CancelBraceletPull(Player player);
}
