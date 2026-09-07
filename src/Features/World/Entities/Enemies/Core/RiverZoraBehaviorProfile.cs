namespace oracleofages;

internal readonly record struct RiverZoraBehaviorProfile(
    int SpawnXLimit, int SpawnYMask, int SurfacingFrames,
    int HiddenCounterMask, int HiddenCounterBase, int WaterTileBase,
    int WaterTileCount);
