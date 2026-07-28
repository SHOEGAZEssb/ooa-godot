namespace oracleofages;

/// <summary>
/// Optional operation observer. Production audio owns no request history;
/// validation may attach a sink when it needs to assert request order.
/// </summary>
internal interface IOracleSoundRequestObserver
{
    void OnSoundRequested(int soundId);
}
