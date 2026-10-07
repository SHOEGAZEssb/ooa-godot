using System;

namespace oracleofages;

/// <summary>
/// Room-local wNumTorchesLit state shared by torch creators and consumers.
/// The original counter is common to dark-room handlers and the generic
/// INTERAC_CREATE_OBJECT_AT_EACH_TILEINDEX torch pointer.
/// </summary>
internal class LightableTorchState
{
    internal int LitCount { get; private set; }
    internal int TotalTorches { get; private set; }

    internal void SetTotalTorches(int count)
    {
        if (count < 0)
        {
            throw new InvalidOperationException(
                "The room-local torch total cannot be negative.");
        }
        TotalTorches = count;
    }

    internal void IncrementLitCount()
    {
        // partCode06 increments a shared byte. Recreating permanent torches
        // (miscPuzzles_subid07) does not reset it or impose a room-total bound.
        LitCount = (LitCount + 1) & 0xff;
    }
}
