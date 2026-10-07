using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private int ApproachTileWall(bool batch = false)
    {
        int update=0;
        for (; !_player.FacesTileWallForInteraction && update<20; update++)
            StepGameplayUpdates(1, (Vector2)_player.FacingVector, batched:batch);
        FailIf(!_player.FacesTileWallForInteraction,
            $"Tile interaction requires both original front wall probes; Link={_player.Position}, facing={_player.FacingVector}.");
        return update;
    }
}
