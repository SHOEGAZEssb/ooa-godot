namespace oracleofages;

/// <summary>
/// Optional observer for effects created by combat. Spawned nodes remain owned
/// by the world tree; validation may retain references for focused assertions.
/// </summary>
internal interface ICombatEffectObserver
{
    void OnClinkEffectSpawned(ClinkEffect effect);
}
