using System;

namespace oracleofages;

/// <summary>
/// Room-local wNumTorchesLit state shared by torch creators and consumers.
/// The original counter is common to dark-room handlers and the generic
/// INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX torch pointer.
/// </summary>
internal class LightableTorchState
{
    private bool _torchTotalInitialized;

    internal int LitCount { get; private set; }
    internal int TotalTorches { get; private set; }

    internal void SetTotalTorches(int count)
    {
        if (count < 0 || _torchTotalInitialized)
        {
            throw new InvalidOperationException(
                "The room-local torch total can only be initialized once.");
        }
        TotalTorches = count;
        _torchTotalInitialized = true;
    }

    internal void IncrementLitCount()
    {
        if (LitCount >= TotalTorches)
        {
            throw new InvalidOperationException(
                "The room-local lit count exceeded its torch total.");
        }
        LitCount++;
    }
}
