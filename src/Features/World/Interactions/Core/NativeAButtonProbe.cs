
namespace Oracles;

// bank0.linkInteractWithAButtonSensitiveObjects probes a world-space point
// in front of Link. Presentation and object solidity do not select the target.
internal static class NativeAButtonProbe
{
    internal static bool Contains(Player player, Vector2 position, float radiusY, float radiusX, int offset = 10)
    {
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 target = OracleObjectMath.ToPixelPosition(position);
        Vector2 point = link + (Vector2)player.FacingVector * offset;
        return Mathf.Abs(target.Y - point.Y) < radiusY && Mathf.Abs(target.X - point.X) < radiusX;
    }
}
