namespace oracleofages;

/// <summary>
/// Optional observer for successful graphics-cache operations. Cache contents
/// remain the production source of truth; observation history belongs to the
/// attached consumer.
/// </summary>
internal interface IOracleGraphicsCacheObserver
{
    void OnGraphicsCacheOperation(OracleGraphicsCacheObservation observation);
}

internal readonly record struct OracleGraphicsCacheObservation(
    OracleGraphicsCacheOperation Operation,
    string Key);

internal enum OracleGraphicsCacheOperation
{
    SourceLoad,
    SourceHit,
    CompositeBuild,
    CompositeHit,
    OamFrameBuild,
    OamFrameHit,
    OamCellBuild,
    OamCellHit,
    AnimationBuild,
    AnimationHit
}
