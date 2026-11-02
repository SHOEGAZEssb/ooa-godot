
namespace Oracles;

public sealed partial class ValidationRoot
{
    // Component fixtures declare the retained object angle from their facing;
    // application-loop fixtures instead exercise its real movement producer.
    private bool TryStartTerrainJump(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartTerrainJump(player,from,movement,CarriedObjectMotion.DirectionIndex(player.FacingVector) * 8);
}
