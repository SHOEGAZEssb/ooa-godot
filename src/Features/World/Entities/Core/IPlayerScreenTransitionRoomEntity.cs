using Godot;

namespace oracleofages;

/// <summary>
/// Owns the position tested and moved by screenTransitionState2/5 while Link's
/// main object is not w1Link. The minecart's SPECIALOBJECT_MINECART companion
/// is the supported source owner.
/// </summary>
internal interface IPlayerScreenTransitionRoomEntity
{
    bool ControlsPlayerScreenTransition { get; }
    Vector2 ScreenTransitionPosition { get; }

    void SetScreenTransitionBoundaryCoordinate(
        bool horizontal,
        int coordinate,
        Player player);

    void BeginScreenTransition(OracleRoomData destination);

    void SetScreenTransitionPosition(
        Vector2 position,
        Vector2 screenOffset,
        Player player);

    void FinishScreenTransition(Vector2 position, Player player);
}
